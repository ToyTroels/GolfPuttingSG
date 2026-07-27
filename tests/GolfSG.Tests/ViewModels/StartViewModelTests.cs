using GolfSG.Core.Models;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;

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
    public async Task LoadBuildsStartInsightsFromRecentRounds()
    {
        var repository = new InMemoryRoundRepository(
            CreateRound("putting", new DateTime(2026, 6, 1), puttingSg: -1),
            CreateRound(
                "approach",
                new DateTime(2026, 6, 2),
                puttingSg: 1,
                approachSg: 2,
                trackingOptions: new RoundTrackingOptions(true, true)),
            CreateRound(
                "all",
                new DateTime(2026, 6, 3),
                puttingSg: 2,
                approachSg: -1,
                aroundGreenSg: 1,
                trackingOptions: new RoundTrackingOptions(true, true, true)));
        var viewModel = new StartViewModel(repository);

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasInsights);
        Assert.AreEqual("Baseret p\u00e5 seneste 3 runder/spil.", viewModel.InsightSummaryText);
        Assert.AreEqual("+1,33", viewModel.FormInsightValue);
        Assert.AreEqual("Gns. total SG", viewModel.FormInsightDetail);
        Assert.AreEqual("Omkring green", viewModel.StrengthInsightValue);
        Assert.AreEqual("+1,00 pr. registrering", viewModel.StrengthInsightDetail);
        Assert.AreEqual("Approach", viewModel.FocusInsightValue);
        Assert.AreEqual("+0,50 pr. registrering", viewModel.FocusInsightDetail);
    }

    [TestMethod]
    public async Task LoadBuildsTrendFromLatestThreeAgainstPreviousThree()
    {
        var repository = new InMemoryRoundRepository(
            CreateRound("old-1", new DateTime(2026, 6, 1), puttingSg: 0),
            CreateRound("old-2", new DateTime(2026, 6, 2), puttingSg: 0),
            CreateRound("old-3", new DateTime(2026, 6, 3), puttingSg: 0),
            CreateRound("new-1", new DateTime(2026, 6, 4), puttingSg: 1),
            CreateRound("new-2", new DateTime(2026, 6, 5), puttingSg: 1),
            CreateRound("new-3", new DateTime(2026, 6, 6), puttingSg: 1));
        var viewModel = new StartViewModel(repository);

        await viewModel.LoadAsync();

        Assert.AreEqual("+1,00", viewModel.TrendInsightValue);
        Assert.AreEqual("Seneste 3 vs forrige 3", viewModel.TrendInsightDetail);
    }

    [TestMethod]
    public async Task DeleteRoundRefreshesStartInsights()
    {
        var repository = new InMemoryRoundRepository(
            CreateRound("old", new DateTime(2026, 6, 1), puttingSg: -2),
            CreateRound("latest", new DateTime(2026, 6, 2), puttingSg: 2));
        var viewModel = new StartViewModel(repository);
        await viewModel.LoadAsync();
        Assert.AreEqual("+0,00", viewModel.FormInsightValue);
        var latest = viewModel.Rounds.First();

        var deleted = await viewModel.DeleteRoundAsync(latest);

        Assert.IsTrue(deleted);
        CollectionAssert.AreEqual(new[] { "old" }, viewModel.RecentRounds.Select(round => round.Id).ToArray());
        Assert.AreEqual("-2,00", viewModel.FormInsightValue);
        Assert.AreEqual("Baseret p\u00e5 seneste runde/spil.", viewModel.InsightSummaryText);
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

    private static Round CreateRound(
        string id,
        DateTime date,
        double puttingSg = 0,
        double approachSg = 0,
        double aroundGreenSg = 0,
        RoundTrackingOptions? trackingOptions = null) => new(
        id,
        date,
        [new HolePuttingData(
            1,
            2,
            2,
            2,
            puttingSg,
            ApproachDistanceMeters: approachSg == 0 ? 0 : 100,
            ApproachShots: approachSg == 0 ? 0 : 1,
            StrokesGainedApproach: approachSg,
            AroundGreenStartDistanceYards: aroundGreenSg == 0 ? 0 : 10,
            StrokesGainedAroundGreen: aroundGreenSg)],
        trackingOptions ?? RoundTrackingOptions.PuttingOnly,
        ConfiguredHoleCount: 1,
        EndedEarly: false);

    private sealed class InMemoryRoundRepository(params Round[] rounds) : IRoundRepository
    {
        private readonly List<Round> rounds = [.. rounds];

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

        public Task DeleteRoundAsync(string roundId)
        {
            rounds.RemoveAll(round => string.Equals(round.Id, roundId, StringComparison.Ordinal));
            return Task.CompletedTask;
        }

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
