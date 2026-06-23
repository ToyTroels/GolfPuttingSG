using GolfPuttingSG.Core;
using GolfPuttingSG.Core.Models;

namespace GolfPuttingSG.Tests;

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
}
