using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class RoundHistoryViewModelTests
{
    [TestMethod]
    public async Task HistoryTypeFilterSeparatesRoundsGamesAndBenchmarks()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(
            CreateRound("round", today.AddDays(-2)),
            CreateRound(
                "game",
                today.AddDays(-1),
                trackingOptions: new RoundTrackingOptions(true, false, false, true, PuttingGame.LadderMode),
                gameInfo: PuttingGame.CreateRoundGameInfo(PuttingGame.GetDefinition(PuttingGame.LadderMode))),
            CreateRound(
                "benchmark",
                today,
                trackingOptions: new RoundTrackingOptions(true, false, false, true, PuttingGame.NormalBenchmark),
                gameInfo: PuttingGame.CreateRoundGameInfo(PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalBenchmark))));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistoryType = "Runder";
        CollectionAssert.AreEqual(new[] { "round" }, viewModel.Rounds.Select(round => round.Id).ToArray());

        viewModel.SelectedHistoryType = "Putting-spil";
        CollectionAssert.AreEqual(new[] { "game" }, viewModel.Rounds.Select(round => round.Id).ToArray());

        viewModel.SelectedHistoryType = "Benchmarks";
        CollectionAssert.AreEqual(new[] { "benchmark" }, viewModel.Rounds.Select(round => round.Id).ToArray());
        Assert.AreEqual("Viser 1 af 3 gemte runder og spil", viewModel.HistorySummaryText);
    }

    [TestMethod]
    public async Task LegacyBenchmarkModeIsShownAsBenchmark()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(
            CreateRound(
                "legacy-benchmark",
                today,
                trackingOptions: new RoundTrackingOptions(true, false, false, true, PuttingGame.NormalBenchmark),
                gameInfo: null));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistoryType = "Benchmarks";

        Assert.HasCount(1, viewModel.Rounds);
        Assert.AreEqual("legacy-benchmark", viewModel.Rounds[0].Id);
        Assert.AreEqual("Benchmark", viewModel.Rounds[0].HistoryTypeText);
    }

    [TestMethod]
    public async Task HistoryPeriodFilterShowsRecentRoundsOnly()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(
            CreateRound("old", today.AddDays(-40)),
            CreateRound("recent", today.AddDays(-5)),
            CreateRound("today", today));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistoryPeriod = "Sidste 30 dage";

        CollectionAssert.AreEqual(
            new[] { "today", "recent" },
            viewModel.Rounds.Select(round => round.Id).ToArray());
    }

    [TestMethod]
    public async Task HistorySortCanShowBestAndWorstSg()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(
            CreateRound("middle", today.AddDays(-2), puttingSg: 0.5),
            CreateRound("best", today.AddDays(-1), puttingSg: 2.0),
            CreateRound("worst", today, puttingSg: -1.0));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistorySort = "Bedste SG";
        CollectionAssert.AreEqual(
            new[] { "best", "middle", "worst" },
            viewModel.Rounds.Select(round => round.Id).ToArray());

        viewModel.SelectedHistorySort = "V\u00e6rste SG";
        CollectionAssert.AreEqual(
            new[] { "worst", "middle", "best" },
            viewModel.Rounds.Select(round => round.Id).ToArray());
    }

    [TestMethod]
    public async Task HistoryCategoryFilterShowsOnlyTrackedCategoryAndSortsByIt()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(
            CreateRound("putting-only", today.AddDays(-2), puttingSg: 4),
            CreateRound(
                "approach-weak",
                today.AddDays(-1),
                puttingSg: 3,
                approachSg: -1,
                trackingOptions: new RoundTrackingOptions(true, true)),
            CreateRound(
                "approach-best",
                today,
                puttingSg: -3,
                approachSg: 2,
                trackingOptions: new RoundTrackingOptions(true, true)));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistoryCategory = "Approach";
        viewModel.SelectedHistorySort = "Bedste SG";

        CollectionAssert.AreEqual(
            new[] { "approach-best", "approach-weak" },
            viewModel.Rounds.Select(round => round.Id).ToArray());
        Assert.AreEqual("+2,00", viewModel.Rounds[0].DisplaySgText);
        Assert.AreEqual("SG approach", viewModel.Rounds[0].DisplaySgCaption);
    }

    [TestMethod]
    public async Task HistoryCategoryFilterCanSwitchToPutting()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(
            CreateRound("putting-best", today.AddDays(-2), puttingSg: 2),
            CreateRound("putting-worst", today.AddDays(-1), puttingSg: -1),
            CreateRound(
                "approach-only",
                today,
                puttingSg: 5,
                approachSg: 1,
                trackingOptions: new RoundTrackingOptions(false, true)));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistoryCategory = "Putting";
        viewModel.SelectedHistorySort = "Bedste SG";

        CollectionAssert.AreEqual(
            new[] { "putting-best", "putting-worst" },
            viewModel.Rounds.Select(round => round.Id).ToArray());
        Assert.AreEqual("+2,00", viewModel.Rounds[0].DisplaySgText);
        Assert.AreEqual("SG putting", viewModel.Rounds[0].DisplaySgCaption);
    }

    [TestMethod]
    public async Task HistoryFiltersIgnoreInvalidPickerValues()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(CreateRound("round", today));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistoryCategory = "Putting";
        viewModel.SelectedHistoryCategory = null!;
        viewModel.SelectedHistorySort = "Ikke en sortering";

        Assert.AreEqual("Putting", viewModel.SelectedHistoryCategory);
        Assert.AreEqual("Nyeste f\u00f8rst", viewModel.SelectedHistorySort);
        Assert.HasCount(1, viewModel.Rounds);
    }

    [TestMethod]
    public async Task HistoryComparisonUsesSelectedCategoryAverageBestAndWorst()
    {
        var today = DateTime.Today;
        var repository = new InMemoryRoundRepository(
            CreateRound(
                "worst",
                today.AddDays(-2),
                approachSg: -1,
                trackingOptions: new RoundTrackingOptions(true, true)),
            CreateRound(
                "middle",
                today.AddDays(-1),
                approachSg: 0.5,
                trackingOptions: new RoundTrackingOptions(true, true)),
            CreateRound(
                "best",
                today,
                approachSg: 2,
                trackingOptions: new RoundTrackingOptions(true, true)));
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();

        viewModel.SelectedHistoryCategory = "Approach";

        Assert.IsTrue(viewModel.HasHistoryComparison);
        StringAssert.Contains(viewModel.HistoryComparisonText, "SG approach: snit +0,50");
        StringAssert.Contains(viewModel.HistoryComparisonText, "bedst");
        StringAssert.Contains(viewModel.HistoryComparisonText, "+2,00");
        StringAssert.Contains(viewModel.HistoryComparisonText, "svagest");
        StringAssert.Contains(viewModel.HistoryComparisonText, "-1,00");

        var best = viewModel.Rounds.Single(round => round.Id == "best");
        Assert.IsTrue(best.HasComparisonText);
        StringAssert.Contains(best.ComparisonText, "+1,50");
    }

    [TestMethod]
    public async Task DeleteRoundUpdatesFilteredHistory()
    {
        var today = DateTime.Today;
        var benchmark = CreateRound(
            "benchmark",
            today,
            trackingOptions: new RoundTrackingOptions(true, false, false, true, PuttingGame.NormalBenchmark),
            gameInfo: PuttingGame.CreateRoundGameInfo(PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalBenchmark)));
        var repository = new InMemoryRoundRepository(
            CreateRound("round", today.AddDays(-1)),
            benchmark);
        var viewModel = new RoundHistoryViewModel(repository);
        await viewModel.LoadAsync();
        viewModel.SelectedHistoryType = "Benchmarks";
        var item = viewModel.Rounds.Single();

        var deleted = await viewModel.DeleteRoundAsync(item);

        Assert.IsTrue(deleted);
        Assert.HasCount(0, viewModel.Rounds);
        Assert.AreEqual("Viser 0 af 1 gemte runder og spil", viewModel.HistorySummaryText);
    }

    private static Round CreateRound(
        string id,
        DateTime date,
        double puttingSg = 0,
        double approachSg = 0,
        double aroundGreenSg = 0,
        RoundTrackingOptions? trackingOptions = null,
        RoundGameInfo? gameInfo = null) => new(
        id,
        date,
        [new HolePuttingData(
            1,
            2,
            2,
            2 + puttingSg,
            puttingSg,
            ApproachDistanceMeters: approachSg == 0 ? 0 : 100,
            ApproachShots: approachSg == 0 ? 0 : 1,
            StrokesGainedApproach: approachSg,
            AroundGreenStartDistanceYards: aroundGreenSg == 0 ? 0 : 10,
            StrokesGainedAroundGreen: aroundGreenSg)],
        trackingOptions ?? RoundTrackingOptions.PuttingOnly,
        ConfiguredHoleCount: 1,
        EndedEarly: false,
        GameInfo: gameInfo);

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
}
