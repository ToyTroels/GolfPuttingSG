using GolfSG.Core.Models;
using GolfSG.Services;
using GolfSG.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class StartViewModelTests
{
    [TestMethod]
    public async Task LoadSortsAllRoundsNewestFirstForHistory()
    {
        var repository = new InMemoryRoundRepository(
            CreateRound("old", new DateTime(2026, 6, 1)),
            CreateRound("new", new DateTime(2026, 6, 10)),
            CreateRound("middle", new DateTime(2026, 6, 5)));
        var viewModel = new StartViewModel(repository);

        await viewModel.LoadAsync();

        CollectionAssert.AreEqual(
            new[] { "new", "middle", "old" },
            viewModel.Rounds.Select(round => round.Id).ToArray());
    }

    [TestMethod]
    public async Task LoadKeepsOnlyFiveNewestRoundsForStartPage()
    {
        var rounds = Enumerable.Range(1, 7)
            .Select(day => CreateRound($"round-{day}", new DateTime(2026, 6, day)))
            .ToArray();
        var viewModel = new StartViewModel(new InMemoryRoundRepository(rounds));

        await viewModel.LoadAsync();

        CollectionAssert.AreEqual(
            new[] { "round-7", "round-6", "round-5", "round-4", "round-3" },
            viewModel.RecentRounds.Select(round => round.Id).ToArray());
        Assert.HasCount(7, viewModel.Rounds);
    }

    [TestMethod]
    public async Task LoadFailureSetsErrorWithoutThrowing()
    {
        var viewModel = new StartViewModel(new FailingRoundRepository(loadFails: true));

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, "Historik");
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(viewModel.CanInteract);
        Assert.HasCount(0, viewModel.Rounds);
    }

    [TestMethod]
    public async Task DeleteFailureSetsErrorAndKeepsRoundInLists()
    {
        var round = CreateRound("round", new DateTime(2026, 6, 1));
        var repository = new FailingRoundRepository(round, deleteFails: true);
        var viewModel = new StartViewModel(repository);
        await viewModel.LoadAsync();
        var item = viewModel.Rounds.Single();

        var deleted = await viewModel.DeleteRoundAsync(item);

        Assert.IsFalse(deleted);
        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, "slettes");
        Assert.HasCount(1, viewModel.Rounds);
        Assert.HasCount(1, viewModel.RecentRounds);
    }

    private static Round CreateRound(string id, DateTime date) => new(
        id,
        date,
        [new HolePuttingData(1, 2, 2, 0, 0, 0, 0, 0, 0)],
        RoundTrackingOptions.PuttingOnly,
        ConfiguredHoleCount: 1,
        EndedEarly: false);

    private sealed class InMemoryRoundRepository(params Round[] rounds) : IRoundRepository
    {
        public string ActiveStoragePath => string.Empty;

        public bool WasLastReadRecoveredFromBackup => false;

        public bool WasLastReadMigratedFromLegacyStorage => false;

        public string? LastMigrationSourcePath => null;

        public bool WasLastUnreadableActiveFilePreserved => false;

        public string? LastPreservedUnreadableFilePath => null;

        public Task<IReadOnlyList<Round>> GetRoundsAsync() =>
            Task.FromResult<IReadOnlyList<Round>>(rounds);

        public Task<Round?> GetRoundAsync(string roundId) =>
            Task.FromResult(rounds.FirstOrDefault(round => round.Id == roundId));

        public Task SaveRoundAsync(Round round) => Task.CompletedTask;

        public Task DeleteRoundAsync(string roundId) => Task.CompletedTask;

        public Task ExportRoundsAsync(string destinationPath) => Task.CompletedTask;

        public Task ImportRoundsAsync(string sourcePath) => Task.CompletedTask;
    }

    private sealed class FailingRoundRepository(
        Round? round = null,
        bool loadFails = false,
        bool deleteFails = false) : IRoundRepository
    {
        private readonly List<Round> rounds = round is null ? [] : [round];

        public string ActiveStoragePath => string.Empty;

        public bool WasLastReadRecoveredFromBackup => false;

        public bool WasLastReadMigratedFromLegacyStorage => false;

        public string? LastMigrationSourcePath => null;

        public bool WasLastUnreadableActiveFilePreserved => false;

        public string? LastPreservedUnreadableFilePath => null;

        public Task<IReadOnlyList<Round>> GetRoundsAsync() => loadFails
            ? throw new IOException("Load failed.")
            : Task.FromResult<IReadOnlyList<Round>>(rounds);

        public Task<Round?> GetRoundAsync(string roundId) =>
            Task.FromResult(rounds.FirstOrDefault(existingRound => existingRound.Id == roundId));

        public Task SaveRoundAsync(Round round) => Task.CompletedTask;

        public Task DeleteRoundAsync(string roundId) => deleteFails
            ? throw new IOException("Delete failed.")
            : Task.CompletedTask;

        public Task ExportRoundsAsync(string destinationPath) => Task.CompletedTask;

        public Task ImportRoundsAsync(string sourcePath) => Task.CompletedTask;
    }
}
