using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class StrokesGainedAroundGreenServiceTests
{
    [TestMethod]
    public void RoughTwentyYardsToEightFeetOnGreenUsesPuttingService()
    {
        var service = CreateService(1.50);
        var shot = CreateShot(
            startDistanceToPin: 20,
            startLie: ShotLie.Rough,
            endDistanceToPin: 8,
            endDistanceUnit: DistanceUnit.Feet,
            endLie: ShotLie.Green);

        var strokesGained = service.CalculateShotSgAroundGreen(shot);

        Assert.AreEqual(0.08, strokesGained, 0.001);
    }

    [TestMethod]
    public void TenYardSandShotHoledGainsOnePointFourFive()
    {
        var service = CreateService();
        var shot = CreateShot(
            startDistanceToPin: 10,
            startLie: ShotLie.Sand,
            endDistanceToPin: 0,
            endLie: ShotLie.Holed,
            holed: true);

        var strokesGained = service.CalculateShotSgAroundGreen(shot);

        Assert.AreEqual(1.45, strokesGained, 0.001);
    }

    [TestMethod]
    public void FiveYardRoughToThirtyFootGreenUsesPuttingService()
    {
        var service = CreateService(1.98);
        var shot = CreateShot(
            startDistanceToPin: 5,
            startLie: ShotLie.Rough,
            endDistanceToPin: 30,
            endDistanceUnit: DistanceUnit.Feet,
            endLie: ShotLie.Green);

        var strokesGained = service.CalculateShotSgAroundGreen(shot);

        Assert.AreEqual(-0.83, strokesGained, 0.001);
    }

    [TestMethod]
    public void ShotStartingOnGreenReturnsZero()
    {
        var service = CreateService();
        var shot = CreateShot(startLie: ShotLie.Green);

        var strokesGained = service.CalculateShotSgAroundGreen(shot);

        Assert.AreEqual(0, strokesGained, 0.001);
    }

    [TestMethod]
    public void TeeShotReturnsZero()
    {
        var service = CreateService();
        var shot = CreateShot(isTeeShot: true);

        var strokesGained = service.CalculateShotSgAroundGreen(shot);

        Assert.AreEqual(0, strokesGained, 0.001);
    }

    [TestMethod]
    public void ShotMoreThanThirtyYardsFromGreenEdgeReturnsZero()
    {
        var service = CreateService();
        var shot = CreateShot(startDistanceToGreenEdgeYards: 30.1);

        var strokesGained = service.CalculateShotSgAroundGreen(shot);

        Assert.AreEqual(0, strokesGained, 0.001);
    }

    [TestMethod]
    public void AroundGreenExpectedStrokesInterpolateLinearly()
    {
        var service = CreateService();

        var expectedStrokes = service.GetAroundGreenExpectedStrokes(12.5, DistanceUnit.Yards, ShotLie.Rough);

        Assert.AreEqual(2.40, expectedStrokes, 0.001);
    }

    [TestMethod]
    public void PenaltyStrokesReduceAroundGreenStrokesGained()
    {
        var service = CreateService();
        var shot = CreateShot(
            startDistanceToPin: 10,
            startLie: ShotLie.Sand,
            endDistanceToPin: 0,
            endLie: ShotLie.Holed,
            holed: true,
            penaltyStrokes: 1);

        var strokesGained = service.CalculateShotSgAroundGreen(shot);

        Assert.AreEqual(0.45, strokesGained, 0.001);
    }

    private static StrokesGainedAroundGreenService CreateService(double expectedPutts = 1.50)
    {
        return new StrokesGainedAroundGreenService(new StubPuttingService(expectedPutts));
    }

    private static GolfShot CreateShot(
        double startDistanceToPin = 20,
        DistanceUnit startDistanceUnit = DistanceUnit.Yards,
        ShotLie startLie = ShotLie.Rough,
        double startDistanceToGreenEdgeYards = 10,
        double endDistanceToPin = 10,
        DistanceUnit endDistanceUnit = DistanceUnit.Yards,
        ShotLie endLie = ShotLie.Rough,
        int penaltyStrokes = 0,
        bool holed = false,
        bool isTeeShot = false)
    {
        return new GolfShot
        {
            HoleNumber = 1,
            Par = 4,
            ShotNumber = 2,
            StartDistanceToPin = startDistanceToPin,
            StartDistanceUnit = startDistanceUnit,
            StartLie = startLie,
            StartDistanceToGreenEdgeYards = startDistanceToGreenEdgeYards,
            EndDistanceToPin = endDistanceToPin,
            EndDistanceUnit = endDistanceUnit,
            EndLie = endLie,
            PenaltyStrokes = penaltyStrokes,
            Holed = holed,
            IsTeeShot = isTeeShot
        };
    }

    private sealed class StubPuttingService : IStrokesGainedPuttingService
    {
        private readonly double expectedPutts;

        public StubPuttingService(double expectedPutts)
        {
            this.expectedPutts = expectedPutts;
        }

        public double GetExpectedPutts(double distance, DistanceUnit unit)
        {
            return expectedPutts;
        }
    }
}
