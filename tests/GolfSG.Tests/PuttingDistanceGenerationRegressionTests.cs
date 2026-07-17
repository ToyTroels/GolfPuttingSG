using GolfSG.Core;

namespace GolfSG.Tests;

[TestClass]
public sealed class PuttingDistanceGenerationRegressionTests
{
    [TestMethod]
    public void FixedDistanceTrainingKeepsRequestedAttemptCount()
    {
        var distances = PuttingGame.BuildTrainingDistancesMeters(
            5,
            2.5,
            2.5,
            PuttingTrainingDistanceDistribution.BellCurve);

        Assert.HasCount(5, distances);
        Assert.IsTrue(distances.All(distance => Math.Abs(distance - 2.5) < 0.001));
    }

    [TestMethod]
    public void SingleAttemptUsesMidpointOfConfiguredRange()
    {
        var distances = PuttingGame.BuildTrainingDistancesMeters(
            1,
            2,
            6,
            PuttingTrainingDistanceDistribution.BellCurve);

        Assert.HasCount(1, distances);
        Assert.AreEqual(4, distances[0], 0.001);
    }

    [TestMethod]
    public void TrainingRejectsInvalidAttemptCount()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PuttingGame.BuildTrainingDistancesMeters(
                0,
                1,
                2,
                PuttingTrainingDistanceDistribution.BellCurve));
    }

    [TestMethod]
    public void TrainingRejectsReversedDistanceRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PuttingGame.BuildTrainingDistancesMeters(
                5,
                3,
                2,
                PuttingTrainingDistanceDistribution.BellCurve));
    }
}
