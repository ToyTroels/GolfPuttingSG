using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class RoundStatisticsServiceTests
{
    [TestMethod]
    public void FilterByPeriodIncludesBoundaryDatesAndSortsNewestFirst()
    {
        var rounds = new[]
        {
            CreatePuttingRound("before", new DateTime(2026, 6, 30), 1, 2, 0),
            CreatePuttingRound("start", new DateTime(2026, 7, 1), 1, 2, 0),
            CreatePuttingRound("middle", new DateTime(2026, 7, 5), 1, 2, 0),
            CreatePuttingRound("end", new DateTime(2026, 7, 7), 1, 2, 0),
            CreatePuttingRound("after", new DateTime(2026, 7, 8), 1, 2, 0)
        };

        var filtered = RoundStatisticsService.FilterByPeriod(
            rounds,
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 7));

        CollectionAssert.AreEqual(
            new[] { "end", "middle", "start" },
            filtered.Select(round => round.Id).ToArray());
    }

    [TestMethod]
    public void PuttingSummaryAggregatesTotalAverageBestAndWorst()
    {
        var rounds = new[]
        {
            CreatePuttingRound("good", new DateTime(2026, 7, 1), 1, 1, 1.25),
            CreatePuttingRound("bad", new DateTime(2026, 7, 2), 2, 3, -0.75),
            CreateApproachRound("approach-only", new DateTime(2026, 7, 3), 100, 1, 0.5)
        };

        var summary = RoundStatisticsService.Summarize(
            rounds,
            StrokesGainedCategory.Putting);

        Assert.AreEqual(3, summary.PeriodRoundCount);
        Assert.AreEqual(2, summary.TrackedRoundCount);
        Assert.AreEqual(0.5, summary.TotalStrokesGained, 0.001);
        Assert.AreEqual(0.25, summary.AverageStrokesGainedPerRound, 0.001);
        Assert.AreEqual("good", summary.BestRound?.Id);
        Assert.AreEqual("bad", summary.WorstRound?.Id);
    }

    [TestMethod]
    public void ApproachSummaryIgnoresRoundsThatDoNotTrackApproach()
    {
        var rounds = new[]
        {
            CreatePuttingRound("putting-only", new DateTime(2026, 7, 1), 1, 1, 1.25),
            CreateApproachRound("approach-good", new DateTime(2026, 7, 2), 100, 1, 0.8),
            CreateApproachRound("approach-bad", new DateTime(2026, 7, 3), 120, 2, -0.3)
        };

        var summary = RoundStatisticsService.Summarize(
            rounds,
            StrokesGainedCategory.Approach);

        Assert.AreEqual(3, summary.PeriodRoundCount);
        Assert.AreEqual(2, summary.TrackedRoundCount);
        Assert.AreEqual(0.5, summary.TotalStrokesGained, 0.001);
        Assert.AreEqual(0.25, summary.AverageStrokesGainedPerRound, 0.001);
        Assert.AreEqual("approach-good", summary.BestRound?.Id);
        Assert.AreEqual("approach-bad", summary.WorstRound?.Id);
        Assert.HasCount(0, summary.PuttingMadePercentageBuckets);
    }

    [TestMethod]
    public void PuttingMadePercentageBucketsCountMakesAttemptsAveragePuttsAndSg()
    {
        var rounds = new[]
        {
            CreatePuttingRound(
                "round-1",
                new DateTime(2026, 7, 1),
                new HolePuttingData(1, DistanceConversions.FeetToMeters(2), 1, 0, 0.2),
                new HolePuttingData(2, DistanceConversions.FeetToMeters(4), 2, 0, -0.1)),
            CreatePuttingRound(
                "round-2",
                new DateTime(2026, 7, 2),
                new HolePuttingData(1, DistanceConversions.FeetToMeters(4.5), 1, 0, 0.3),
                new HolePuttingData(2, DistanceConversions.FeetToMeters(30), 3, 0, -0.5))
        };

        var summary = RoundStatisticsService.Summarize(
            rounds,
            StrokesGainedCategory.Putting);

        Assert.HasCount(7, summary.PuttingMadePercentageBuckets);

        var insideThreeFeet = summary.PuttingMadePercentageBuckets[0];
        Assert.AreEqual("Inside 3 ft", insideThreeFeet.Name);
        Assert.AreEqual(1, insideThreeFeet.Attempts);
        Assert.AreEqual(1, insideThreeFeet.MadePutts);
        Assert.AreEqual(100, insideThreeFeet.MadePercentage, 0.001);
        Assert.AreEqual(1, insideThreeFeet.AveragePutts, 0.001);
        Assert.AreEqual(0.2, insideThreeFeet.TotalStrokesGained, 0.001);

        var threeToFiveFeet = summary.PuttingMadePercentageBuckets[1];
        Assert.AreEqual("3-5 ft", threeToFiveFeet.Name);
        Assert.AreEqual(2, threeToFiveFeet.Attempts);
        Assert.AreEqual(1, threeToFiveFeet.MadePutts);
        Assert.AreEqual(50, threeToFiveFeet.MadePercentage, 0.001);
        Assert.AreEqual(1.5, threeToFiveFeet.AveragePutts, 0.001);
        Assert.AreEqual(0.2, threeToFiveFeet.TotalStrokesGained, 0.001);

        var outsideTwentyFiveFeet = summary.PuttingMadePercentageBuckets[6];
        Assert.AreEqual("> 25 ft", outsideTwentyFiveFeet.Name);
        Assert.AreEqual(1, outsideTwentyFiveFeet.Attempts);
        Assert.AreEqual(0, outsideTwentyFiveFeet.MadePutts);
        Assert.AreEqual(0, outsideTwentyFiveFeet.MadePercentage, 0.001);
        Assert.AreEqual(3, outsideTwentyFiveFeet.AveragePutts, 0.001);
    }

    [TestMethod]
    public void EmptySummaryReturnsZeroesAndNoBestWorstRound()
    {
        var summary = RoundStatisticsService.Summarize(
            [],
            StrokesGainedCategory.Total,
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 7));

        Assert.AreEqual(0, summary.PeriodRoundCount);
        Assert.AreEqual(0, summary.TrackedRoundCount);
        Assert.AreEqual(0, summary.TotalStrokesGained);
        Assert.AreEqual(0, summary.AverageStrokesGainedPerRound);
        Assert.IsNull(summary.BestRound);
        Assert.IsNull(summary.WorstRound);
        Assert.HasCount(7, summary.PuttingMadePercentageBuckets);
        Assert.IsTrue(summary.PuttingMadePercentageBuckets.All(bucket => bucket.Attempts == 0));
    }

    private static Round CreatePuttingRound(
        string id,
        DateTime date,
        double firstPuttDistanceMeters,
        int putts,
        double strokesGained) =>
        CreatePuttingRound(
            id,
            date,
            new HolePuttingData(1, firstPuttDistanceMeters, putts, 0, strokesGained));

    private static Round CreatePuttingRound(
        string id,
        DateTime date,
        params HolePuttingData[] holes) =>
        new(
            id,
            date,
            holes,
            RoundTrackingOptions.PuttingOnly,
            holes.Length);

    private static Round CreateApproachRound(
        string id,
        DateTime date,
        double approachDistanceMeters,
        int approachShots,
        double strokesGained) =>
        new(
            id,
            date,
            [new HolePuttingData(
                HoleNumber: 1,
                FirstPuttDistanceMeters: 0,
                Putts: 0,
                ExpectedPutts: 0,
                StrokesGainedPutting: 0,
                ApproachDistanceMeters: approachDistanceMeters,
                ApproachShots: approachShots,
                ExpectedApproachShots: 0,
                StrokesGainedApproach: strokesGained)],
            new RoundTrackingOptions(
                TrackPutting: false,
                TrackApproach: true),
            ConfiguredHoleCount: 1);
}
