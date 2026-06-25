using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class StrokesGainedCalculatorTests
{
    [TestMethod]
    public void ExactBaselineDistanceReturnsExpectedPutts()
    {
        var expected = StrokesGainedCalculator.GetExpectedPutts(2.4);

        Assert.AreEqual(1.53, expected, 0.001);
    }

    [TestMethod]
    public void DistanceBetweenBaselinePointsUsesLinearInterpolation()
    {
        var expected = StrokesGainedCalculator.GetExpectedPutts(2.7);

        Assert.AreEqual(1.57, expected, 0.001);
    }

    [TestMethod]
    public void DistanceBelowBaselineClampsToLowestValue()
    {
        var expected = StrokesGainedCalculator.GetExpectedPutts(0.1);

        Assert.AreEqual(1.00, expected, 0.001);
    }

    [TestMethod]
    public void DistanceAboveBaselineClampsToHighestValue()
    {
        var expected = StrokesGainedCalculator.GetExpectedPutts(40);

        Assert.AreEqual(2.36, expected, 0.001);
    }

    [TestMethod]
    public void StrokesGainedIsExpectedPuttsMinusActualPutts()
    {
        var sg = StrokesGainedCalculator.CalculateStrokesGained(1.53, 2);

        Assert.AreEqual(-0.47, sg, 0.001);
    }

    [TestMethod]
    public void PuttingCalculatorBuildsPuttingOnlyHole()
    {
        var hole = PuttingStrokesGainedCalculator.BuildHole(1, 2.4, 2);

        Assert.IsTrue(hole.IsCompleted);
        Assert.IsFalse(hole.IsApproachCompleted);
        Assert.AreEqual(1.53, hole.ExpectedPutts, 0.001);
        Assert.AreEqual(-0.47, hole.StrokesGainedPutting, 0.001);
        Assert.AreEqual(0, hole.StrokesGainedApproach);
    }

    [TestMethod]
    public void ApproachCalculatorAddsApproachWithoutChangingPutting()
    {
        var puttingOnlyHole = PuttingStrokesGainedCalculator.BuildHole(1, 2.4, 2);
        var combinedHole = ApproachStrokesGainedCalculator.AddApproach(puttingOnlyHole, 91.4, 3);

        Assert.AreEqual(puttingOnlyHole.StrokesGainedPutting, combinedHole.StrokesGainedPutting);
        Assert.IsTrue(combinedHole.IsApproachCompleted);
        Assert.AreEqual(2.92, combinedHole.ExpectedApproachShots, 0.001);
        Assert.AreEqual(-0.08, combinedHole.StrokesGainedApproach, 0.001);
    }

    [TestMethod]
    public void RoundSummaryIgnoresUnfinishedHoles()
    {
        var round = new Round(
            Guid.NewGuid().ToString("N"),
            DateTime.Today,
            [
                StrokesGainedCalculator.BuildHole(1, 2.4, 1),
                StrokesGainedCalculator.BuildHole(2, 0, 0),
                StrokesGainedCalculator.BuildHole(3, 3.0, 3)
            ]);

        var summary = StrokesGainedCalculator.CalculateRoundSummary(round);

        Assert.AreEqual(-0.86, summary.TotalStrokesGainedPutting, 0.001);
        Assert.AreEqual(4, summary.TotalPutts);
        Assert.AreEqual(1, summary.OnePutts);
        Assert.AreEqual(1, summary.ThreePuttsOrWorse);
    }

    [TestMethod]
    public void RoundSummarySupportsRoundsShorterThanEighteenHoles()
    {
        var round = new Round(
            Guid.NewGuid().ToString("N"),
            DateTime.Today,
            Enumerable.Range(1, 9)
                .Select(hole => StrokesGainedCalculator.BuildHole(hole, 2.4, 2))
                .ToList());

        var summary = StrokesGainedCalculator.CalculateRoundSummary(round);

        Assert.AreEqual(18, summary.TotalPutts);
        Assert.AreEqual(9, summary.TwoPutts);
        Assert.AreEqual(-4.23, summary.TotalStrokesGainedPutting, 0.001);
    }

    [TestMethod]
    public void RoundSummaryGroupsPuttingByPgaTourDistanceBands()
    {
        const double metersPerFoot = 0.3048;
        var threeFeet = 3 * metersPerFoot;
        var fiveFeet = 5 * metersPerFoot;
        var tenFeet = 10 * metersPerFoot;
        var fifteenFeet = 15 * metersPerFoot;
        var twentyFeet = 20 * metersPerFoot;
        var twentyFiveFeet = 25 * metersPerFoot;

        var round = new Round(
            Guid.NewGuid().ToString("N"),
            DateTime.Today,
            [
                StrokesGainedCalculator.BuildHole(1, 0.8, 1),
                StrokesGainedCalculator.BuildHole(2, threeFeet, 2),
                StrokesGainedCalculator.BuildHole(3, fiveFeet, 1),
                StrokesGainedCalculator.BuildHole(4, tenFeet, 2),
                StrokesGainedCalculator.BuildHole(5, fifteenFeet, 2),
                StrokesGainedCalculator.BuildHole(6, twentyFeet, 2),
                StrokesGainedCalculator.BuildHole(7, twentyFiveFeet, 3),
                StrokesGainedCalculator.BuildHole(8, twentyFiveFeet + 0.1, 3)
            ]);

        var summary = StrokesGainedCalculator.CalculateRoundSummary(round);

        Assert.HasCount(7, summary.PuttingDistanceBuckets);

        var insideThreeFeet = summary.PuttingDistanceBuckets[0];
        Assert.AreEqual("Inside 3 ft", insideThreeFeet.Name);
        Assert.AreEqual(threeFeet, insideThreeFeet.MaximumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(1, insideThreeFeet.Attempts);

        var threeToFiveFeet = summary.PuttingDistanceBuckets[1];
        Assert.AreEqual("3-5 ft", threeToFiveFeet.Name);
        Assert.AreEqual(threeFeet, threeToFiveFeet.MinimumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(fiveFeet, threeToFiveFeet.MaximumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(1, threeToFiveFeet.Attempts);

        var fiveToTenFeet = summary.PuttingDistanceBuckets[2];
        Assert.AreEqual("5-10 ft", fiveToTenFeet.Name);
        Assert.AreEqual(fiveFeet, fiveToTenFeet.MinimumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(tenFeet, fiveToTenFeet.MaximumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(1, fiveToTenFeet.Attempts);

        var tenToFifteenFeet = summary.PuttingDistanceBuckets[3];
        Assert.AreEqual("10-15 ft", tenToFifteenFeet.Name);
        Assert.AreEqual(tenFeet, tenToFifteenFeet.MinimumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(fifteenFeet, tenToFifteenFeet.MaximumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(1, tenToFifteenFeet.Attempts);

        var fifteenToTwentyFeet = summary.PuttingDistanceBuckets[4];
        Assert.AreEqual("15-20 ft", fifteenToTwentyFeet.Name);
        Assert.AreEqual(fifteenFeet, fifteenToTwentyFeet.MinimumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(twentyFeet, fifteenToTwentyFeet.MaximumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(1, fifteenToTwentyFeet.Attempts);

        var twentyToTwentyFiveFeet = summary.PuttingDistanceBuckets[5];
        Assert.AreEqual("20-25 ft", twentyToTwentyFiveFeet.Name);
        Assert.AreEqual(twentyFeet, twentyToTwentyFiveFeet.MinimumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(twentyFiveFeet, twentyToTwentyFiveFeet.MaximumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(2, twentyToTwentyFiveFeet.Attempts);

        var outsideTwentyFiveFeet = summary.PuttingDistanceBuckets[6];
        Assert.AreEqual("> 25 ft", outsideTwentyFiveFeet.Name);
        Assert.AreEqual(twentyFiveFeet, outsideTwentyFiveFeet.MinimumDistanceMeters!.Value, 0.0001);
        Assert.AreEqual(1, outsideTwentyFiveFeet.Attempts);

        Assert.AreEqual(8, summary.PuttingDistanceBuckets.Sum(bucket => bucket.Attempts));
    }

    [TestMethod]
    public void PuttingGamePresetExpectedPuttsTotalThirty()
    {
        var expectedTotal = PuttingGame.PresetDistancesFeet.Sum(distance => PuttingGame.GetExpectedPutts(distance));

        Assert.AreEqual(30.00, expectedTotal, 0.001);
    }

    [TestMethod]
    public void PuttingGameStartsAtThreeFeet()
    {
        Assert.AreEqual(3, PuttingGame.PresetDistancesFeet[0]);
    }

    [TestMethod]
    public void PuttingGameCustomDistancesUseConfiguredInterval()
    {
        var distances = PuttingGame.BuildBellCurveDistancesMeters(24, 1, 12);

        Assert.HasCount(24, distances);
        Assert.IsTrue(distances.All(distance => distance >= 1 && distance <= 12));
    }

    [TestMethod]
    public void PuttingGameCustomDistancesFavorMiddleOfInterval()
    {
        var distances = PuttingGame.BuildBellCurveDistancesMeters(30, 1, 12);
        var middleCount = distances.Count(distance => distance >= 4 && distance <= 9);
        var edgeCount = distances.Count(distance => distance < 4 || distance > 9);

        Assert.IsGreaterThan(edgeCount, middleCount);
    }

    [TestMethod]
    public void PuttingGameBenchmarkDistancesHavePresetCounts()
    {
        Assert.HasCount(40, PuttingGame.GetBenchmarkDistancesMeters(PuttingGame.ShortBenchmark));
        Assert.HasCount(75, PuttingGame.GetBenchmarkDistancesMeters(PuttingGame.NormalBenchmark));
        Assert.HasCount(100, PuttingGame.GetBenchmarkDistancesMeters(PuttingGame.ThoroughBenchmark));
    }

    [TestMethod]
    public void PuttingGameCanOrderBenchmarkDistancesAscending()
    {
        var distances = PuttingGame.OrderDistances(
            PuttingGame.GetBenchmarkDistancesMeters(PuttingGame.ShortBenchmark),
            PuttingDistanceOrder.Ascending);

        Assert.IsTrue(distances.SequenceEqual(distances.Order()));
    }

    [TestMethod]
    public void PuttingGameCanOrderBenchmarkDistancesDescending()
    {
        var distances = PuttingGame.OrderDistances(
            PuttingGame.GetBenchmarkDistancesMeters(PuttingGame.ShortBenchmark),
            PuttingDistanceOrder.Descending);

        Assert.IsTrue(distances.SequenceEqual(distances.OrderDescending()));
    }

    [TestMethod]
    public void PuttingGameRandomBenchmarkOrderKeepsSameDistances()
    {
        var preset = PuttingGame.GetBenchmarkDistancesMeters(PuttingGame.ShortBenchmark);
        var distances = PuttingGame.OrderDistances(
            preset,
            PuttingDistanceOrder.Random,
            new Random(123));

        CollectionAssert.AreEqual(preset.Order().ToList(), distances.Order().ToList());
        Assert.IsFalse(preset.SequenceEqual(distances));
    }

    [TestMethod]
    public void PuttingGameCustomPuttUsesRawExpectedPutts()
    {
        var distance = PuttingGame.NormalBenchmarkDistancesMeters[0];
        var expectedPutts = PuttingStrokesGainedCalculator.GetExpectedPutts(distance);
        var putt = PuttingGame.BuildPutt(1, distance, 1);

        Assert.AreEqual(expectedPutts, putt.ExpectedPutts, 0.001);
    }

    [TestMethod]
    public void TourRoundGameIsNotIncreasinglyLonger()
    {
        var distances = PuttingGame.GetPresetDistances(PuttingGame.TourRoundMode);

        Assert.IsTrue(distances.Where((distance, index) => index > 0 && distance < distances[index - 1]).Any());
        Assert.AreEqual(30.00, distances.Sum(distance => PuttingGame.GetExpectedPutts(distance, PuttingGame.TourRoundMode)), 0.001);
    }

    [TestMethod]
    public void PuttingGameThirtyPuttsIsNeutralStrokesGained()
    {
        var holes = PuttingGame.PresetDistancesFeet
            .Select((distance, index) => PuttingGame.BuildPutt(index + 1, distance, index < 6 ? 1 : 2, PuttingGame.LadderMode))
            .ToList();

        var round = new Round(
            Guid.NewGuid().ToString("N"),
            DateTime.Today,
            holes,
            RoundTrackingOptions.PuttingGame);

        var summary = StrokesGainedCalculator.CalculateRoundSummary(round);

        Assert.AreEqual(30, summary.TotalPutts);
        Assert.AreEqual(0, summary.TotalStrokesGainedPutting, 0.001);
    }
}
