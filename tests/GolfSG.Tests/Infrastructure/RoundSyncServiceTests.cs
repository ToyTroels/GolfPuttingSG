using GolfSG.Application.Products;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Infrastructure.Rounds;

namespace GolfSG.Tests.Infrastructure;

[TestClass]
public sealed class RoundSyncServiceTests
{
    [TestMethod]
    public async Task OfflineSavingAndHistoryWorkAndRetryUploadsOnce()
    {
        using var setup = new Setup();
        var round = CreateRound();
        setup.Api.Offline = true;
        await setup.Repository.SaveRoundAsync(round);
        var history = new RoundHistoryViewModel(setup.Repository, syncService: setup.Service);
        await history.LoadAsync();
        await history.SyncAsync();
        Assert.HasCount(1, history.Rounds);
        Assert.Contains("offline", history.SyncMessage);
        Assert.IsFalse(history.IsBusy);

        setup.Api.Offline = false;
        await history.SyncAsync();
        Assert.Contains("1 sendt", history.SyncMessage);
        Assert.HasCount(1, setup.Api.Rounds);
        Assert.AreEqual(new RoundSyncResult(0, 0, 0), await setup.Restart().Service.SyncAsync());
        Assert.AreEqual(1, setup.Api.Writes);
    }

    [TestMethod]
    public async Task DownloadsUnseenRoundAndLaterServerEdit()
    {
        using var setup = new Setup();
        var round = CreateRound();
        setup.Api.Set(round);
        Assert.AreEqual(new RoundSyncResult(0, 1, 0), await setup.Service.SyncAsync());
        var changed = round with { Date = round.Date.AddDays(1) };
        setup.Api.Set(changed);
        Assert.AreEqual(new RoundSyncResult(0, 1, 0), await setup.Restart().Service.SyncAsync());
        Assert.AreEqual(changed.Date, (await setup.Repository.GetRoundAsync(round.Id))!.Date);
    }

    [TestMethod]
    public async Task LocalChangeWinsWhenBothCopiesChanged()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        await setup.Service.SyncAsync();
        setup.Api.Set(round with { Date = round.Date.AddDays(1) });
        var local = round with { Date = round.Date.AddDays(2) };
        await setup.Repository.SaveRoundAsync(local);
        Assert.AreEqual(1, (await setup.Service.SyncAsync()).Uploaded);
        Assert.AreEqual(local.Date, setup.Api.Rounds[round.Id].Round!.Date);
    }

    [TestMethod]
    public async Task OfflineDeletionSurvivesRestartAndNeverDownloadsDeletedRound()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        await setup.Service.SyncAsync();
        setup.Api.Offline = true;
        await setup.Repository.DeleteRoundAsync(round.Id);
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        setup.Api.Offline = false;
        var restarted = setup.Restart();
        Assert.AreEqual(1, (await restarted.Service.SyncAsync()).Deleted);
        Assert.IsNull(setup.Api.Rounds[round.Id].Round);
        Assert.IsNull(await restarted.Repository.GetRoundAsync(round.Id));
        Assert.AreEqual(new RoundSyncResult(0, 0, 0), await restarted.Service.SyncAsync());
    }

    [TestMethod]
    public async Task LostUploadResponseFollowedByDeletionDoesNotRestoreRound()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        setup.Api.LoseNextResponse = true;
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        Assert.IsNotNull(setup.Api.Rounds[round.Id].Round);
        await setup.Repository.DeleteRoundAsync(round.Id);
        await setup.Restart().Service.SyncAsync();
        Assert.IsNull(setup.Api.Rounds[round.Id].Round);
        Assert.IsNull(await setup.Repository.GetRoundAsync(round.Id));
    }

    [TestMethod]
    public async Task RetryingAfterLostResponseDoesNotCreateAnotherWrite()
    {
        using var setup = new Setup();
        await setup.Repository.SaveRoundAsync(CreateRound());
        setup.Api.LoseNextResponse = true;
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        Assert.AreEqual(new RoundSyncResult(0, 0, 0), await setup.Restart().Service.SyncAsync());
        Assert.AreEqual(1, setup.Api.Writes);
    }

    [TestMethod]
    public async Task ServerDeletionRemovesSyncedCopyButPreservesNewLocalChanges()
    {
        using var setup = new Setup();
        var first = CreateRound();
        var second = CreateRound();
        await setup.Repository.SaveRoundAsync(first);
        await setup.Repository.SaveRoundAsync(second);
        await setup.Service.SyncAsync();
        setup.Api.SetDeleted(first.Id);
        setup.Api.SetDeleted(second.Id);
        await setup.Repository.SaveRoundAsync(second with { Date = second.Date.AddDays(1) });
        var result = await setup.Service.SyncAsync();
        Assert.AreEqual(1, result.Deleted);
        Assert.AreEqual(1, result.Uploaded);
        Assert.IsNull(await setup.Repository.GetRoundAsync(first.Id));
        Assert.IsNotNull(await setup.Repository.GetRoundAsync(second.Id));
    }

    [TestMethod]
    public async Task RestoringDeletedRoundCanUploadTheSameOriginalContent()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        await setup.Service.SyncAsync();
        await setup.Repository.DeleteRoundAsync(round.Id);
        await setup.Service.SyncAsync();
        await setup.Repository.SaveRoundAsync(round);
        Assert.AreEqual(1, (await setup.Service.SyncAsync()).Uploaded);
        Assert.IsNotNull(setup.Api.Rounds[round.Id].Round);
    }

    [TestMethod]
    public async Task RoundSavingDoesNotWaitForNetworkAndItsNewerEditIsRetried()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Api.BeforeWrite = async () => { entered.SetResult(); await release.Task; };
        var syncing = setup.Service.SyncAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var edited = round with { Date = round.Date.AddDays(1) };
        await setup.Repository.SaveRoundAsync(edited).WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        await syncing;
        setup.Api.BeforeWrite = null;
        Assert.AreEqual(1, (await setup.Service.SyncAsync()).Uploaded);
        Assert.AreEqual(edited.Date, setup.Api.Rounds[round.Id].Round!.Date);
    }

    [TestMethod]
    public async Task DeletionWhileUploadIsRunningRemainsPending()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Api.BeforeWrite = async () => { entered.SetResult(); await release.Task; };
        var syncing = setup.Service.SyncAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await setup.Repository.DeleteRoundAsync(round.Id).WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        await syncing;
        setup.Api.BeforeWrite = null;
        await setup.Service.SyncAsync();
        Assert.IsNull(setup.Api.Rounds[round.Id].Round);
    }

    [TestMethod]
    public async Task CorruptSyncStateDoesNotBlockSavingOrReplaceLocalHistory()
    {
        using var setup = new Setup();
        await File.WriteAllTextAsync(setup.State.Path, "{}");
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Repository.DeleteRoundAsync(round.Id));
        Assert.IsNotNull(await setup.Repository.GetRoundAsync(round.Id));
        Assert.AreEqual("{}", await File.ReadAllTextAsync(setup.State.Path));
    }

    [TestMethod]
    public async Task KeepsServerAddressFixedDuringSync()
    {
        using var setup = new Setup();
        await setup.Repository.SaveRoundAsync(CreateRound());
        setup.Api.OnGet = () => setup.Settings.BaseAddress = "https://other.example/";
        await setup.Service.SyncAsync();
        CollectionAssert.AreEqual(new[] { "https://localhost:7151/", "https://localhost:7151/" }, setup.Api.Addresses.ToArray());
    }

    [TestMethod]
    public async Task ConcurrentServerWriteIsNotOverwrittenAndRetryKeepsLocalChange()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        await setup.Service.SyncAsync();
        var edited = round with { Date = round.Date.AddDays(1) };
        await setup.Repository.SaveRoundAsync(edited);
        var competing = round with { Date = round.Date.AddDays(2) };
        setup.Api.BeforeWrite = () => { setup.Api.Set(competing); return Task.CompletedTask; };
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        Assert.AreEqual(edited.Date, (await setup.Repository.GetRoundAsync(round.Id))!.Date);
        Assert.AreEqual(competing.Date, setup.Api.Rounds[round.Id].Round!.Date);
        setup.Api.BeforeWrite = null;
        await setup.Service.SyncAsync();
        Assert.AreEqual(edited.Date, setup.Api.Rounds[round.Id].Round!.Date);
    }

    [TestMethod]
    public async Task CancellationLeavesHistoryAvailableAndAllowsRetry()
    {
        using var setup = new Setup();
        await setup.Repository.SaveRoundAsync(CreateRound());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => setup.Service.SyncAsync(canceled.Token));
        Assert.HasCount(1, await setup.Repository.GetRoundsAsync());
        Assert.AreEqual(1, (await setup.Service.SyncAsync()).Uploaded);
    }

    private static Round CreateRound() => new(Guid.NewGuid().ToString("N"), new DateTime(2026, 10, 10),
        [StrokesGainedCalculator.BuildHole(1, 3, 2)], RoundTrackingOptions.PuttingOnly, 1);

    private sealed class Settings : IProductsApiSettings
    { public string BaseAddress { get; set; } = "https://localhost:7151/"; }

    private sealed class Setup : IDisposable
    {
        private readonly string directory;
        private readonly bool ownsDirectory;
        public FakeApi Api { get; }
        public Settings Settings { get; }
        public RoundSyncStateStore State { get; }
        public SyncingRoundRepository Repository { get; }
        public RoundSyncService Service { get; }

        public Setup() : this(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "golfsg-sync-" + Guid.NewGuid()),
            new(), new(), true) { }
        private Setup(string directory, FakeApi api, Settings settings, bool ownsDirectory)
        {
            this.directory = directory;
            this.ownsDirectory = ownsDirectory;
            Directory.CreateDirectory(directory);
            Api = api;
            Settings = settings;
            State = new(directory);
            Repository = new(new FileRoundRepository(directory), State);
            Service = new(Repository, State, api, settings);
        }
        public Setup Restart() => new(directory, Api, Settings, false);
        public void Dispose() { if (ownsDirectory) Directory.Delete(directory, recursive: true); }
    }

    private sealed class FakeApi : IRoundsApiService
    {
        public Dictionary<string, RemoteRound> Rounds { get; } = new(StringComparer.Ordinal);
        public bool Offline { get; set; }
        public bool LoseNextResponse { get; set; }
        public int Writes { get; private set; }
        public Func<Task>? BeforeWrite { get; set; }
        public Action? OnGet { get; set; }
        public List<string> Addresses { get; } = [];
        public void Set(Round round) => Rounds[round.Id] = new(round.Id, Guid.NewGuid(), round);
        public void SetDeleted(string id) => Rounds[id] = new(id, Guid.NewGuid(), null);

        public Task<IReadOnlyList<RemoteRound>> GetRoundsAsync(Uri server, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Offline) throw new RoundSyncException("offline: Dine runder er stadig gemt på enheden.");
            Addresses.Add(server.AbsoluteUri);
            OnGet?.Invoke();
            return Task.FromResult<IReadOnlyList<RemoteRound>>(Rounds.Values.ToList());
        }

        public async Task<RemoteRound> WriteAsync(Uri server, string id, RoundWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (BeforeWrite is not null) await BeforeWrite();
            cancellationToken.ThrowIfCancellationRequested();
            if (Offline) throw new RoundSyncException("offline");
            Addresses.Add(server.AbsoluteUri);
            Rounds.TryGetValue(id, out var previous);
            if (previous?.Version != request.ExpectedVersion) throw new RoundSyncException("conflict");
            var written = new RemoteRound(id, request.Version, request.Round);
            Rounds[id] = written;
            Writes++;
            if (LoseNextResponse)
            {
                LoseNextResponse = false;
                throw new RoundSyncException("lost response");
            }
            return written;
        }
    }
}
