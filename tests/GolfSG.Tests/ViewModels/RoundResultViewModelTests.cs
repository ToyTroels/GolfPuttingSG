using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class RoundResultViewModelTests
{
    [TestMethod]
    public async Task StatsUseOnlyThreePuttHolesAndRankLossByTotalInsteadOfPerHole()
    {
        var round = new Round("stats", DateTime.Today,
        [
            StrokesGainedCalculator.BuildHole(1, 1, 3),
            StrokesGainedCalculator.BuildHole(2, 3, 3),
            StrokesGainedCalculator.BuildHole(3, 3, 3),
            StrokesGainedCalculator.BuildHole(4, 3, 4),
            StrokesGainedCalculator.BuildHole(5, 0, 0)
        ], RoundTrackingOptions.PuttingOnly, 5);
        var model = new RoundResultViewModel(new TestRoundRepository([round]));
        await model.LoadAsync(round.Id);
        Assert.AreEqual($"{UiFormat.PuttingDistance(7d / 3, PuttingDistanceUnitPreference.Meters)} · 3 3-putts",
            model.ThreePuttStartingDistanceText);
        StringAssert.StartsWith(model.PuttingOpportunityText, "Mellemlange putts");
        StringAssert.Contains(model.PuttingOpportunityText, "3 registreringer");
    }

    [TestMethod]
    public async Task StatsShowNoThreePuttsOrLossForPositiveRoundAndRespectFeet()
    {
        var round = new Round("positive", DateTime.Today,
            [StrokesGainedCalculator.BuildHole(1, 1, 1)], RoundTrackingOptions.PuttingOnly, 1);
        var threePuttRound = round with { Id = "three", Holes = [StrokesGainedCalculator.BuildHole(1, DistanceConversions.FeetToMeters(30), 3)] };
        var model = new RoundResultViewModel(new TestRoundRepository([round, threePuttRound]),
            new FixedDistanceUnitSettings(PuttingDistanceUnitPreference.Feet));
        await model.LoadAsync(round.Id);
        Assert.AreEqual("Ingen 3-putts registreret", model.ThreePuttStartingDistanceText);
        Assert.AreEqual("Ingen afstandsgruppe har tabte slag mod PGA-reference.", model.PuttingOpportunityText);
        await model.LoadAsync(threePuttRound.Id);
        StringAssert.StartsWith(model.ThreePuttStartingDistanceText, "30,0 ft");
        StringAssert.StartsWith(model.PuttingOpportunityText, "Lange putts");
    }

    [TestMethod]
    public async Task DistanceRecapGroupsBoundaryDistancesAndPreservesPuttingTotals()
    {
        var holes = new[]
        {
            StrokesGainedCalculator.BuildHole(1, DistanceConversions.FeetToMeters(4), 1),
            StrokesGainedCalculator.BuildHole(2, DistanceConversions.FeetToMeters(5), 2),
            StrokesGainedCalculator.BuildHole(3, DistanceConversions.FeetToMeters(25), 3),
            StrokesGainedCalculator.BuildHole(4, DistanceConversions.FeetToMeters(26), 2),
            StrokesGainedCalculator.BuildHole(5, 0, 0)
        };
        var round = new Round("buckets", DateTime.Today, holes, RoundTrackingOptions.PuttingOnly, 5);
        var viewModel = new RoundResultViewModel(new TestRoundRepository([round]));
        await viewModel.LoadAsync(round.Id);

        Assert.HasCount(6, viewModel.PuttingDistanceBuckets);
        StringAssert.StartsWith(viewModel.PuttingDistanceBuckets[0].DetailText, "1 ");
        StringAssert.StartsWith(viewModel.PuttingDistanceBuckets[1].DetailText, "1 ");
        StringAssert.StartsWith(viewModel.PuttingDistanceBuckets[4].DetailText, "1 ");
        StringAssert.StartsWith(viewModel.PuttingDistanceBuckets[5].DetailText, "1 ");
        Assert.AreEqual(4, viewModel.PuttingDistanceBuckets.Sum(bucket => bucket.Attempts));
        Assert.AreEqual(holes.Sum(hole => hole.StrokesGainedPutting),
            viewModel.PuttingDistanceBuckets.Sum(bucket => bucket.TotalStrokesGained), 0.000001);
        Assert.IsTrue(viewModel.PuttingDistanceBuckets[1].IsLoss);
        Assert.AreEqual(UiFormat.Sg(holes[1].StrokesGainedPutting),
            viewModel.PuttingDistanceBuckets[1].StrokesGainedText);

        var empty = new PuttingDistanceBucketItemViewModel(new PuttingDistanceBucketSummary("Empty", null, 1, 0, 0, 0));
        Assert.AreEqual("—", empty.StrokesGainedText);
        Assert.AreEqual("Ingen data", empty.OutcomeText);
        Assert.IsFalse(empty.IsLoss);
    }

    [TestMethod]
    public async Task LoadPartialRoundPopulatesProgressResultsBucketsAndAnalysis()
    {
        var round = new Round(
            "partial",
            new DateTime(2026, 7, 16),
            [
                StrokesGainedCalculator.BuildHole(1, DistanceConversions.FeetToMeters(3), 1),
                StrokesGainedCalculator.BuildHole(2, DistanceConversions.FeetToMeters(30), 3),
                StrokesGainedCalculator.BuildHole(3, 0, 0)
            ],
            RoundTrackingOptions.PuttingOnly,
            9,
            EndedEarly: true);
        var viewModel = new RoundResultViewModel(new TestRoundRepository([round]));

        await viewModel.LoadAsync(round.Id);

        Assert.AreEqual(round.Id, viewModel.RoundId);
        Assert.AreEqual("Resultat", viewModel.ResultTitle);
        Assert.IsTrue(viewModel.TrackPutting);
        Assert.IsFalse(viewModel.IsPuttingGame);
        Assert.AreEqual("2 / 9 huller registreret", viewModel.RoundProgressText);
        Assert.AreEqual("Runden blev afsluttet tidligt.", viewModel.RoundCompletionText);
        Assert.AreEqual("50%", viewModel.ThreePuttRateText);
        var expectedPutts = round.Holes.Where(hole => hole.IsCompleted).Sum(hole => hole.ExpectedPutts)
            .ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));
        Assert.AreEqual(expectedPutts, viewModel.ExpectedPuttsText);
        Assert.IsFalse(viewModel.ShowTotalSg);
        Assert.HasCount(2, viewModel.HoleResults);
        Assert.IsNotEmpty(viewModel.PuttingDistanceBuckets);
        Assert.IsEmpty(viewModel.Analysis);
        Assert.IsFalse(viewModel.HasAnalysis);
    }

    [TestMethod]
    public async Task LoadCompletedRoundShowsCompletedText()
    {
        var round = new Round(
            "complete",
            new DateTime(2026, 7, 16),
            [
                StrokesGainedCalculator.BuildHole(1, 2, 2),
                StrokesGainedCalculator.BuildHole(2, 3, 2)
            ],
            RoundTrackingOptions.PuttingOnly,
            2,
            EndedEarly: false);
        var viewModel = new RoundResultViewModel(new TestRoundRepository([round]));

        await viewModel.LoadAsync(round.Id);

        Assert.AreEqual("Runden blev fuldført.", viewModel.RoundCompletionText);
        Assert.AreEqual("2 / 2 huller registreret", viewModel.RoundProgressText);
    }

    [TestMethod]
    public async Task PuttingGameUsesSavedMetadataAndPuttLabels()
    {
        var definition = PuttingGame.GetBenchmarkDefinition(PuttingGame.ShortLadderBenchmark);
        var holes = definition.DistancesMeters
            .Take(2)
            .Select((distance, index) => PuttingGame.BuildPutt(index + 1, distance, index + 1))
            .ToList();
        var round = new Round(
            "benchmark",
            new DateTime(2026, 7, 16),
            holes,
            new RoundTrackingOptions(true, false, false, true, PuttingGame.LadderMode),
            holes.Count,
            false,
            PuttingGame.CreateRoundGameInfo(definition));
        var viewModel = new RoundResultViewModel(new TestRoundRepository([round]));

        await viewModel.LoadAsync(round.Id);

        Assert.IsTrue(viewModel.IsPuttingGame);
        Assert.AreEqual(definition.DisplayName, viewModel.ResultTitle);
        Assert.AreEqual(holes.Sum(hole => hole.ExpectedPutts).ToString("0.0"), viewModel.TargetPuttsText);
        Assert.IsTrue(viewModel.HoleResults.All(item => item.Title.StartsWith("Putt ", StringComparison.Ordinal)));
        StringAssert.StartsWith(viewModel.BestHoleText, "Putt ");
    }

    [TestMethod]
    public async Task FeetPreferenceFormatsResultDistancesInFeet()
    {
        var round = new Round(
            "feet",
            new DateTime(2026, 7, 16),
            [StrokesGainedCalculator.BuildHole(1, DistanceConversions.FeetToMeters(3), 2)],
            RoundTrackingOptions.PuttingOnly,
            1);
        var settings = new FixedDistanceUnitSettings(PuttingDistanceUnitPreference.Feet);
        var viewModel = new RoundResultViewModel(new TestRoundRepository([round]), settings);

        await viewModel.LoadAsync(round.Id);

        Assert.AreEqual("3,0 ft", viewModel.AverageDistanceText);
        StringAssert.Contains(viewModel.HoleResults[0].Detail, "3,0 ft");
        Assert.IsTrue(viewModel.PuttingDistanceBuckets.All(bucket =>
            bucket.Name.Contains("ft", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ReloadReplacesCollectionsInsteadOfAppendingStaleResults()
    {
        var first = new Round(
            "first",
            DateTime.Today,
            [
                StrokesGainedCalculator.BuildHole(1, 1, 1),
                StrokesGainedCalculator.BuildHole(2, 2, 2)
            ],
            RoundTrackingOptions.PuttingOnly,
            2);
        var second = new Round(
            "second",
            DateTime.Today,
            [StrokesGainedCalculator.BuildHole(1, 4, 2)],
            RoundTrackingOptions.PuttingOnly,
            1);
        var repository = new TestRoundRepository([first, second]);
        var viewModel = new RoundResultViewModel(repository);

        await viewModel.LoadAsync(first.Id);
        Assert.HasCount(2, viewModel.HoleResults);

        await viewModel.LoadAsync(second.Id);

        Assert.AreEqual(second.Id, viewModel.RoundId);
        Assert.HasCount(1, viewModel.HoleResults);
        Assert.AreEqual("Hul 1", viewModel.HoleResults[0].Title);
    }

    [TestMethod]
    public async Task MissingRoundKeepsEmptyResultState()
    {
        var viewModel = new RoundResultViewModel(new TestRoundRepository());

        await viewModel.LoadAsync("missing");

        Assert.AreEqual(string.Empty, viewModel.RoundId);
        Assert.AreEqual("0 / 0 huller", viewModel.RoundProgressText);
        Assert.IsEmpty(viewModel.HoleResults);
        Assert.IsEmpty(viewModel.Analysis);
    }

    [TestMethod]
    public async Task RepositoryFailurePropagatesToPageErrorBoundary()
    {
        var repository = new TestRoundRepository
        {
            GetRoundException = new IOException("Read failed.")
        };
        var viewModel = new RoundResultViewModel(repository);

        await Assert.ThrowsExactlyAsync<IOException>(() => viewModel.LoadAsync("round"));
    }

    [TestMethod]
    public async Task CombinedTrackingBuildsAllCategoryDetails()
    {
        var approach = new GolfShot
        {
            HoleNumber = 1,
            ShotNumber = 1,
            StartDistanceToPin = 100,
            StartDistanceUnit = DistanceUnit.Yards,
            StartLie = ShotLie.Fairway,
            StartDistanceToGreenEdgeYards = 60,
            EndDistanceToPin = 20,
            EndDistanceUnit = DistanceUnit.Yards,
            EndLie = ShotLie.Rough,
            Par = 4
        };
        var aroundGreen = new GolfShot
        {
            HoleNumber = 1,
            ShotNumber = 2,
            StartDistanceToPin = 20,
            StartDistanceUnit = DistanceUnit.Yards,
            StartLie = ShotLie.Rough,
            StartDistanceToGreenEdgeYards = 10,
            EndDistanceToPin = 6,
            EndDistanceUnit = DistanceUnit.Feet,
            EndLie = ShotLie.Green
        };
        var hole = StrokesGainedCalculator.BuildHole(
            1,
            2,
            2,
            approachShot: approach,
            aroundGreenShot: aroundGreen);
        var round = new Round(
            "combined",
            DateTime.Today,
            [hole],
            new RoundTrackingOptions(true, true, true),
            1);
        var viewModel = new RoundResultViewModel(new TestRoundRepository([round]));

        await viewModel.LoadAsync(round.Id);

        Assert.IsTrue(viewModel.TrackPutting);
        Assert.IsTrue(viewModel.TrackApproach);
        Assert.IsTrue(viewModel.TrackAroundGreen);
        Assert.IsTrue(viewModel.ShowTotalSg);
        StringAssert.Contains(viewModel.HoleResults[0].Detail, "Indspil");
        StringAssert.Contains(viewModel.HoleResults[0].Detail, "Omkring green");
        Assert.IsNotEmpty(viewModel.Analysis);
    }
}
