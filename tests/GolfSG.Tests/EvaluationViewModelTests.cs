using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Services;
using GolfSG.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class EvaluationViewModelTests
{
    [TestMethod]
    public async Task LoadShowsLastThirtyDaysTotalByDefault()
    {
        var today = DateTime.Today;
        var viewModel = new EvaluationViewModel(new InMemoryRoundRepository(
            CreatePuttingRound("included", today.AddDays(-2), 1.4),
            CreatePuttingRound("old", today.AddDays(-40), 9.9)));

        await viewModel.LoadAsync();

        Assert.AreEqual("Sidste 30 dage", viewModel.SelectedPeriod);
        Assert.AreEqual("Total", viewModel.SelectedCategory);
        Assert.AreEqual("1 runde inkluderet", viewModel.IncludedRoundsText);
        Assert.AreEqual(UiFormat.Sg(1.4), viewModel.TotalSgText);
        Assert.AreEqual(UiFormat.Sg(1.4), viewModel.AverageSgText);
        Assert.IsTrue(viewModel.HasTrackedRounds);
    }

    [TestMethod]
    public async Task CategorySelectionRecalculatesSummary()
    {
        var today = DateTime.Today;
        var viewModel = new EvaluationViewModel(new InMemoryRoundRepository(
            CreatePuttingRound("putting", today, 1.4),
            CreateApproachRound("approach", today, 2.2)));

        await viewModel.LoadAsync();
        viewModel.SelectedCategory = "Approach";

        Assert.AreEqual("1 runde inkluderet", viewModel.IncludedRoundsText);
        Assert.AreEqual(UiFormat.Sg(2.2), viewModel.TotalSgText);
        Assert.AreEqual("Approach", viewModel.SelectedCategory);
        Assert.IsFalse(viewModel.ShowPuttingBuckets);
    }

    [TestMethod]
    public async Task AllRoundsPeriodIncludesOlderRounds()
    {
        var today = DateTime.Today;
        var viewModel = new EvaluationViewModel(new InMemoryRoundRepository(
            CreatePuttingRound("recent", today, 1.0),
            CreatePuttingRound("old", today.AddDays(-80), 2.0)));

        await viewModel.LoadAsync();
        viewModel.SelectedPeriod = "Alle runder";

        Assert.AreEqual("2 runder inkluderet", viewModel.IncludedRoundsText);
        Assert.AreEqual(UiFormat.Sg(3.0), viewModel.TotalSgText);
    }

    [TestMethod]
    public async Task LoadFailureSetsError()
    {
        var viewModel = new EvaluationViewModel(new FailingRoundRepository());

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, "Evalueringen");
        Assert.IsFalse(viewModel.IsBusy);
    }

    [TestMethod]
    public async Task PuttingBucketsShowOnlyBucketsWithAttempts()
    {
        var today = DateTime.Today;
        var viewModel = new EvaluationViewModel(new InMemoryRoundRepository(new Round(
            "round",
            today,
            [
                new HolePuttingData(1, DistanceConversions.FeetToMeters(2), 1, 0, 0.2),
                new HolePuttingData(2, DistanceConversions.FeetToMeters(4), 2, 0, -0.1)
            ],
            RoundTrackingOptions.PuttingOnly,
            ConfiguredHoleCount: 2)));

        await viewModel.LoadAsync();

        Assert.HasCount(2, viewModel.PuttingBuckets);
        Assert.AreEqual("Inside 3 ft", viewModel.PuttingBuckets[0].Name);
        Assert.AreEqual("1 fors\u00f8g | 1 i | 100%", viewModel.PuttingBuckets[0].DetailText);
        Assert.AreEqual("3-5 ft", viewModel.PuttingBuckets[1].Name);
    }

    private static Round CreatePuttingRound(string id, DateTime date, double strokesGained) =>
        new(
            id,
            date,
            [new HolePuttingData(1, 2, 1, 0, strokesGained)],
            RoundTrackingOptions.PuttingOnly,
            ConfiguredHoleCount: 1);

    private static Round CreateApproachRound(string id, DateTime date, double strokesGained) =>
        new(
            id,
            date,
            [new HolePuttingData(
                HoleNumber: 1,
                FirstPuttDistanceMeters: 0,
                Putts: 0,
                ExpectedPutts: 0,
                StrokesGainedPutting: 0,
                ApproachDistanceMeters: 100,
                ApproachShots: 1,
                ExpectedApproachShots: 0,
                StrokesGainedApproach: strokesGained)],
            new RoundTrackingOptions(
                TrackPutting: false,
                TrackApproach: true),
            ConfiguredHoleCount: 1);

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
