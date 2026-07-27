using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class BenchmarkHistoryViewModelTests
{
    [TestMethod]
    public async Task LoadCreatesBenchmarkItems()
    {
        var definition = PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalLadderBenchmark);
        var viewModel = new BenchmarkHistoryViewModel(new InMemoryRoundRepository(
            CreateBenchmarkRound("first", new DateTime(2026, 7, 1), definition, 1.0),
            CreateBenchmarkRound("latest", new DateTime(2026, 7, 2), definition, 2.5)));

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasBenchmarks);
        Assert.HasCount(1, viewModel.Benchmarks);
        var item = viewModel.Benchmarks[0];
        Assert.AreEqual("Normal ladder benchmark", item.Title);
        Assert.AreEqual("Ladder benchmark", item.TypeText);
        Assert.AreEqual("2 fors\u00f8g", item.AttemptsText);
        Assert.AreEqual(UiFormat.Sg(2.5), item.LatestScoreText);
        Assert.AreEqual(UiFormat.Sg(2.5), item.BestScoreText);
        Assert.AreEqual(UiFormat.Sg(1.75), item.AverageLastThreeText);
        Assert.AreEqual(UiFormat.Sg(1.5), item.ImprovementText);
        StringAssert.Contains(item.DetailText, "1,0 m-6,0 m");
    }

    [TestMethod]
    public async Task LoadUsesPreferredFeetForLadderDistances()
    {
        var definition = PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalLadderBenchmark);
        var settings = new FixedDistanceUnitSettings(PuttingDistanceUnitPreference.Feet);
        var viewModel = new BenchmarkHistoryViewModel(
            new InMemoryRoundRepository(CreateBenchmarkRound("feet", new DateTime(2026, 7, 1), definition, 1.0)),
            settings);

        await viewModel.LoadAsync();

        Assert.HasCount(1, viewModel.Benchmarks);
        StringAssert.Contains(viewModel.Benchmarks[0].DetailText, "3,3 ft-19,7 ft");
        StringAssert.Contains(viewModel.Benchmarks[0].DetailText, "3,3 ft trin");
    }

    [TestMethod]
    public async Task LoadWithNoBenchmarksShowsEmptyState()
    {
        var viewModel = new BenchmarkHistoryViewModel(new InMemoryRoundRepository());

        await viewModel.LoadAsync();

        Assert.IsFalse(viewModel.HasBenchmarks);
        Assert.HasCount(0, viewModel.Benchmarks);
        Assert.AreEqual("Ingen benchmark-fors\u00f8g endnu.", viewModel.EmptyStateText);
    }

    [TestMethod]
    public async Task LoadFailureSetsError()
    {
        var viewModel = new BenchmarkHistoryViewModel(new FailingRoundRepository());

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, "Benchmark-historik");
        Assert.IsFalse(viewModel.IsBusy);
    }

    private static Round CreateBenchmarkRound(
        string id,
        DateTime date,
        PuttingGameDefinition definition,
        double score) =>
        new(
            id,
            date,
            [new HolePuttingData(1, definition.DistancesMeters[0], 2, 0, score)],
            new RoundTrackingOptions(true, false, false, true),
            ConfiguredHoleCount: 1,
            EndedEarly: false,
            PuttingGame.CreateRoundGameInfo(definition));

    private class InMemoryRoundRepository(params Round[] rounds) : IRoundRepository
    {
        public string ActiveStoragePath => string.Empty;

        public bool WasLastReadRecoveredFromBackup => false;

        public bool WasLastReadMigratedFromLegacyStorage => false;

        public string? LastMigrationSourcePath => null;

        public bool WasLastUnreadableActiveFilePreserved => false;

        public string? LastPreservedUnreadableFilePath => null;

        public virtual Task<IReadOnlyList<Round>> GetRoundsAsync() =>
            Task.FromResult<IReadOnlyList<Round>>(rounds);

        public Task<Round?> GetRoundAsync(string roundId) =>
            Task.FromResult(rounds.FirstOrDefault(round => round.Id == roundId));

        public Task SaveRoundAsync(Round round) => Task.CompletedTask;

        public Task DeleteRoundAsync(string roundId) => Task.CompletedTask;

        public Task ExportRoundsAsync(string destinationPath) => Task.CompletedTask;

        public Task ImportRoundsAsync(string sourcePath) => Task.CompletedTask;
    }

    private sealed class FailingRoundRepository : InMemoryRoundRepository
    {
        public override Task<IReadOnlyList<Round>> GetRoundsAsync() =>
            throw new IOException("Load failed.");
    }
}
