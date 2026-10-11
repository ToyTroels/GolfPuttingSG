using GolfSG.Application.Products;
using GolfSG.Application.Accounts;
using GolfSG.Tests.Infrastructure.TestDoubles;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Infrastructure.Rounds;
using System.Text;
using System.Text.Json;

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
    public async Task ServerChangeDuringSyncStopsLocalChangesAndUploads()
    {
        using var setup = new Setup();
        await setup.Repository.SaveRoundAsync(CreateRound());
        setup.Api.OnGet = () => setup.Settings.BaseAddress = "https://other.example/";
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        CollectionAssert.AreEqual(new[] { "https://localhost:7151/" }, setup.Api.Addresses.ToArray());
        Assert.AreEqual(0, setup.Api.Writes);
    }

    [TestMethod]
    public async Task SignedOutSyncPreservesOfflineHistoryWithoutContactingServer()
    {
        using var setup = new Setup();
        await setup.Repository.SaveRoundAsync(CreateRound());
        setup.Accounts.SignedOut = true;
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        Assert.HasCount(1, await setup.Repository.GetRoundsAsync());
        Assert.HasCount(0, setup.Api.Addresses);
    }

    [TestMethod]
    public async Task LogoutWhileDownloadingDoesNotApplyRemoteHistory()
    {
        using var setup = new Setup();
        setup.Api.Set(CreateRound());
        setup.Api.OnGet = () => setup.Accounts.SignedOut = true;
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        Assert.HasCount(0, await setup.Repository.GetRoundsAsync());
        Assert.IsFalse(File.Exists(setup.State.Path));
    }

    [TestMethod]
    public async Task LogoutWhileUploadingDoesNotAcknowledgeOrChangeLocalRound()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        setup.Api.BeforeWrite = () => { setup.Accounts.SignedOut = true; return Task.CompletedTask; };
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        Assert.IsNotNull(await setup.Repository.GetRoundAsync(round.Id));
        Assert.IsFalse(File.Exists(setup.State.Path));
        setup.Api.BeforeWrite = null;
        setup.Accounts.SignedOut = false;
        Assert.AreEqual(new RoundSyncResult(0, 0, 0), await setup.Service.SyncAsync());
    }

    [TestMethod]
    public async Task LegacyServerOnlyHashDoesNotAuthorizeNewAccountRemoteDeletion()
    {
        using var setup = new Setup();
        var round = CreateRound();
        await setup.Repository.SaveRoundAsync(round);
        await setup.Service.SyncAsync();
        var state = await File.ReadAllTextAsync(setup.State.Path);
        Assert.Contains("https://localhost:7151/|test-user", state);
        await File.WriteAllTextAsync(setup.State.Path, state.Replace("https://localhost:7151/|test-user", "https://localhost:7151/"));
        setup.Api.SetDeleted(round.Id);
        Assert.AreEqual(1, (await setup.Service.SyncAsync()).Uploaded);
        Assert.IsNotNull(await setup.Repository.GetRoundAsync(round.Id));
        Assert.IsNotNull(setup.Api.Rounds[round.Id].Round);
    }

    [TestMethod]
    public async Task ExportedRoundsImportedOfflineUploadToSignedInOwnerAndPreserveDuplicateLocalIds()
    {
        using var setup = new Setup();
        var existing = CreateRound();
        var added = CreateRound();
        await setup.Repository.SaveRoundAsync(existing);
        var export = await CreateExportAsync(setup, existing with { Date = existing.Date.AddDays(-1) }, added);
        setup.Accounts.SignedOut = true;
        var importer = new RoundImportService(setup.Repository, setup.CacheDirectory);
        await using (var input = new NonSeekableReadStream(await File.ReadAllBytesAsync(export)))
        {
            await importer.ImportAsync(input);
            Assert.IsTrue(input.CanRead, "The caller still owns the picked file stream.");
        }
        Assert.HasCount(2, await setup.Repository.GetRoundsAsync());
        Assert.AreEqual(existing.Date, (await setup.Repository.GetRoundAsync(existing.Id))!.Date);
        await Assert.ThrowsAsync<RoundSyncException>(() => setup.Service.SyncAsync());
        Assert.HasCount(0, setup.Api.Addresses);

        setup.Accounts.SignedOut = false;
        setup.Accounts.AccountId = "import-owner";
        Assert.AreEqual(new RoundSyncResult(2, 0, 0), await setup.Service.SyncAsync());
        Assert.HasCount(2, setup.Api.Rounds);
        Assert.AreEqual(existing.Date, setup.Api.Rounds[existing.Id].Round!.Date);
        Assert.IsTrue(setup.Api.AccountIds.All(id => id == "import-owner"));
        Assert.AreEqual(2, setup.Api.Writes);

        await using (var input = File.OpenRead(export)) await importer.ImportAsync(input);
        Assert.AreEqual(new RoundSyncResult(0, 0, 0), await setup.Service.SyncAsync());
        Assert.AreEqual(2, setup.Api.Writes);
        Assert.HasCount(0, Directory.GetFiles(setup.CacheDirectory, "golfsg-import-*.json"));
    }

    [TestMethod]
    public async Task ExplicitImportRestoresOnlyIncludedDeletionAndDoesNotLetServerTombstoneDeleteItAgain()
    {
        using var setup = new Setup();
        var restored = CreateRound();
        var stillDeleted = CreateRound();
        await setup.Repository.SaveRoundAsync(restored);
        await setup.Repository.SaveRoundAsync(stillDeleted);
        var export = await CreateExportAsync(setup, restored);
        await setup.Service.SyncAsync();
        await setup.Repository.DeleteRoundAsync(restored.Id);
        await setup.Repository.DeleteRoundAsync(stillDeleted.Id);
        Assert.AreEqual(2, (await setup.Service.SyncAsync()).Deleted);
        var importer = new RoundImportService(setup.Repository, setup.CacheDirectory);
        await using (var input = File.OpenRead(export)) await importer.ImportAsync(input);

        using (var state = JsonDocument.Parse(await File.ReadAllTextAsync(setup.State.Path)))
        {
            var deletions = state.RootElement.GetProperty("deletedIds").EnumerateArray().Select(id => id.GetString()).ToArray();
            CollectionAssert.AreEqual(new[] { stillDeleted.Id }, deletions);
            foreach (var server in state.RootElement.GetProperty("servers").EnumerateObject())
                Assert.IsFalse(server.Value.TryGetProperty(restored.Id, out _), "Restored imports must be new local work.");
        }
        var restarted = setup.Restart();
        Assert.AreEqual(new RoundSyncResult(1, 0, 0), await restarted.Service.SyncAsync());
        Assert.IsNotNull(setup.Api.Rounds[restored.Id].Round);
        Assert.IsNotNull(await restarted.Repository.GetRoundAsync(restored.Id));
        Assert.IsNull(setup.Api.Rounds[stillDeleted.Id].Round);
        Assert.IsNull(await restarted.Repository.GetRoundAsync(stillDeleted.Id));
        Assert.AreEqual(new RoundSyncResult(0, 0, 0), await restarted.Service.SyncAsync());
    }

    [TestMethod]
    public async Task ImportingDifferentRoundOrInvalidFilePreservesPendingDeletion()
    {
        using var setup = new Setup();
        var deleted = CreateRound();
        await setup.Repository.SaveRoundAsync(deleted);
        await setup.Service.SyncAsync();
        await setup.Repository.DeleteRoundAsync(deleted.Id);
        var stateBeforeImport = await File.ReadAllTextAsync(setup.State.Path);
        var importer = new RoundImportService(setup.Repository, setup.CacheDirectory);
        await using (var invalid = new MemoryStream(Encoding.UTF8.GetBytes("invalid rounds JSON")))
            await Assert.ThrowsAsync<InvalidDataException>(() => importer.ImportAsync(invalid));
        Assert.AreEqual(stateBeforeImport, await File.ReadAllTextAsync(setup.State.Path));
        Assert.IsNull(await setup.Repository.GetRoundAsync(deleted.Id));
        Assert.HasCount(0, Directory.GetFiles(setup.CacheDirectory, "golfsg-import-*.json"));

        var unrelated = CreateRound();
        var export = await CreateExportAsync(setup, unrelated);
        await using (var input = File.OpenRead(export)) await importer.ImportAsync(input);
        Assert.AreEqual(stateBeforeImport, await File.ReadAllTextAsync(setup.State.Path));
        Assert.AreEqual(new RoundSyncResult(1, 0, 1), await setup.Service.SyncAsync());
        Assert.IsNull(setup.Api.Rounds[deleted.Id].Round);
        Assert.IsNotNull(setup.Api.Rounds[unrelated.Id].Round);
    }

    [TestMethod]
    public async Task CorruptSyncStateRejectsImportBeforeChangingLocalHistory()
    {
        using var setup = new Setup();
        var existing = CreateRound();
        await setup.Repository.SaveRoundAsync(existing);
        var export = await CreateExportAsync(setup, CreateRound());
        await File.WriteAllTextAsync(setup.State.Path, "{}");
        var importer = new RoundImportService(setup.Repository, setup.CacheDirectory);
        await using var input = File.OpenRead(export);
        await Assert.ThrowsAsync<RoundSyncException>(() => importer.ImportAsync(input));
        Assert.AreEqual("{}", await File.ReadAllTextAsync(setup.State.Path));
        Assert.HasCount(1, await setup.Repository.GetRoundsAsync());
        Assert.IsNotNull(await setup.Repository.GetRoundAsync(existing.Id));
        Assert.HasCount(0, Directory.GetFiles(setup.CacheDirectory, "golfsg-import-*.json"));
    }

    [TestMethod]
    public async Task CanceledPickedFileImportKeepsHistoryAndCreatesNoStagedFiles()
    {
        using var setup = new Setup();
        var existing = CreateRound();
        await setup.Repository.SaveRoundAsync(existing);
        var importer = new RoundImportService(setup.Repository, setup.CacheDirectory);
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("anything"));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => importer.ImportAsync(input, canceled.Token));
        Assert.IsTrue(input.CanRead);
        Assert.IsFalse(Directory.Exists(setup.CacheDirectory));
        Assert.HasCount(1, await setup.Repository.GetRoundsAsync());
    }

    [TestMethod]
    public async Task PickedFileReadFailureCleansStagingFileAndPreservesHistoryAndPendingDeletion()
    {
        using var setup = new Setup();
        var deleted = CreateRound();
        var existing = CreateRound();
        await setup.Repository.SaveRoundAsync(deleted);
        await setup.Repository.SaveRoundAsync(existing);
        await setup.Repository.DeleteRoundAsync(deleted.Id);
        var state = await File.ReadAllTextAsync(setup.State.Path);
        var importer = new RoundImportService(setup.Repository, setup.CacheDirectory);
        await using var input = new NonSeekableReadStream([], failRead: true);
        await Assert.ThrowsAsync<IOException>(() => importer.ImportAsync(input));
        Assert.IsTrue(input.CanRead);
        Assert.HasCount(0, Directory.GetFiles(setup.CacheDirectory, "golfsg-import-*.json"));
        Assert.AreEqual(state, await File.ReadAllTextAsync(setup.State.Path));
        Assert.HasCount(1, await setup.Repository.GetRoundsAsync());
        Assert.IsNotNull(await setup.Repository.GetRoundAsync(existing.Id));
    }

    [TestMethod]
    public async Task CancellationDuringPickedFileCopyCleansStagingAndPreservesCallerStreamHistoryAndDeletions()
    {
        using var setup = new Setup();
        var deleted = CreateRound();
        var existing = CreateRound();
        await setup.Repository.SaveRoundAsync(deleted);
        await setup.Repository.SaveRoundAsync(existing);
        await setup.Repository.DeleteRoundAsync(deleted.Id);
        var state = await File.ReadAllTextAsync(setup.State.Path);
        var importer = new RoundImportService(setup.Repository, setup.CacheDirectory);
        await using var input = new CancellationBlockedReadStream();
        using var cancellation = new CancellationTokenSource();
        var importing = importer.ImportAsync(input, cancellation.Token);
        await input.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.HasCount(1, Directory.GetFiles(setup.CacheDirectory, "golfsg-import-*.json"));

        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => importing.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(input.CanRead, "Cancellation must not dispose the caller's picked file stream.");
        Assert.HasCount(0, Directory.GetFiles(setup.CacheDirectory, "golfsg-import-*.json"));
        Assert.AreEqual(state, await File.ReadAllTextAsync(setup.State.Path));
        Assert.HasCount(1, await setup.Repository.GetRoundsAsync());
        Assert.IsNotNull(await setup.Repository.GetRoundAsync(existing.Id));
        Assert.IsNull(await setup.Repository.GetRoundAsync(deleted.Id));
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

    private static async Task<string> CreateExportAsync(Setup setup, params Round[] rounds)
    {
        var source = new FileRoundRepository(Path.Combine(setup.DataDirectory, "export-source-" + Guid.NewGuid()));
        foreach (var round in rounds) await source.SaveRoundAsync(round);
        var path = Path.Combine(setup.DataDirectory, "round-export-" + Guid.NewGuid() + ".json");
        await source.ExportRoundsAsync(path);
        return path;
    }

    private sealed class NonSeekableReadStream(byte[] bytes, bool failRead = false) : Stream
    {
        private readonly MemoryStream source = new(bytes);
        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => failRead ? throw new IOException("Picked file could not be read.") : source.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => failRead ? throw new IOException("Picked file could not be read.") : source.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }

    private sealed class CancellationBlockedReadStream : Stream
    {
        private bool disposed;
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => !disposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) disposed = true; base.Dispose(disposing); }
    }

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
        public TestApiAccountService Accounts { get; }
        public string DataDirectory => directory;
        public string CacheDirectory => Path.Combine(directory, "import-cache");

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
            Accounts = new(settings);
            Service = new(Repository, State, api, settings, Accounts);
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
        public List<string> AccountIds { get; } = [];
        public void Set(Round round) => Rounds[round.Id] = new(round.Id, Guid.NewGuid(), round);
        public void SetDeleted(string id) => Rounds[id] = new(id, Guid.NewGuid(), null);

        public Task<IReadOnlyList<RemoteRound>> GetRoundsAsync(ApiSession session, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Offline) throw new RoundSyncException("offline: Dine runder er stadig gemt på enheden.");
            Addresses.Add(session.ServerAddress);
            AccountIds.Add(session.AccountId);
            OnGet?.Invoke();
            return Task.FromResult<IReadOnlyList<RemoteRound>>(Rounds.Values.ToList());
        }

        public async Task<RemoteRound> WriteAsync(ApiSession session, string id, RoundWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (BeforeWrite is not null) await BeforeWrite();
            cancellationToken.ThrowIfCancellationRequested();
            if (Offline) throw new RoundSyncException("offline");
            Addresses.Add(session.ServerAddress);
            AccountIds.Add(session.AccountId);
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
