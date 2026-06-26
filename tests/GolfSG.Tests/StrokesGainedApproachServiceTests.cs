using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class StrokesGainedApproachServiceTests
{
    [TestMethod]
    public void ParFourSecondShotFairwayToGreenUsesPuttingService()
    {
        var service = CreateService(expectedPutts: 1.87);
        var shot = CreateShot(
            par: 4,
            isTeeShot: false,
            startDistanceToPin: 150,
            startLie: ShotLie.Fairway,
            endDistanceToPin: 20,
            endDistanceUnit: DistanceUnit.Feet,
            endLie: ShotLie.Green);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(0.08, strokesGained, 0.001);
    }

    [TestMethod]
    public void ParThreeTeeShotCountsAsApproach()
    {
        var service = CreateService(expectedPutts: 1.98);
        var shot = CreateShot(
            par: 3,
            isTeeShot: true,
            startDistanceToPin: 175,
            startLie: ShotLie.Tee,
            endDistanceToPin: 30,
            endDistanceUnit: DistanceUnit.Feet,
            endLie: ShotLie.Green);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(0.14, strokesGained, 0.001);
    }

    [TestMethod]
    public void ParFiveApproachFinishingWithinThirtyYardsUsesAroundGreenService()
    {
        var service = CreateService(expectedAroundGreen: 2.58);
        var shot = CreateShot(
            par: 5,
            isTeeShot: false,
            startDistanceToPin: 100,
            startLie: ShotLie.Rough,
            endDistanceToPin: 20,
            endDistanceUnit: DistanceUnit.Yards,
            endLie: ShotLie.Rough,
            endDistanceToGreenEdgeYards: 10);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(-0.58, strokesGained, 0.001);
    }

    [TestMethod]
    public void FinishZoneUsesEndDistanceInsteadOfGreenEdgeDistance()
    {
        var service = CreateService(expectedAroundGreen: 2.58);
        var shot = CreateShot(
            startDistanceToPin: 200,
            startLie: ShotLie.Fairway,
            endDistanceToPin: 20,
            endDistanceUnit: DistanceUnit.Yards,
            endLie: ShotLie.Rough,
            endDistanceToGreenEdgeYards: 80);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(-0.36, strokesGained, 0.001);
    }

    [TestMethod]
    public void ParFourTeeShotDoesNotCountAsApproach()
    {
        var service = CreateService();
        var shot = CreateShot(par: 4, isTeeShot: true, startDistanceToPin: 420, startLie: ShotLie.Tee);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(0, strokesGained, 0.001);
    }

    [TestMethod]
    public void ShotStartingOnGreenDoesNotCountAsApproach()
    {
        var service = CreateService();
        var shot = CreateShot(startLie: ShotLie.Green);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(0, strokesGained, 0.001);
    }

    [TestMethod]
    public void ShotStartingInsideAroundGreenZoneDoesNotCountAsApproach()
    {
        var service = CreateService();
        var shot = CreateShot(startDistanceToPin: 12, startDistanceToGreenEdgeYards: 12, startLie: ShotLie.Rough);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(0, strokesGained, 0.001);
    }

    [TestMethod]
    public void HoledApproachUsesZeroFinishExpectedStrokes()
    {
        var service = CreateService();
        var shot = CreateShot(
            startDistanceToPin: 150,
            startLie: ShotLie.Fairway,
            endDistanceToPin: 0,
            endLie: ShotLie.Holed,
            holed: true);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(1.95, strokesGained, 0.001);
    }

    [TestMethod]
    public void PenaltyStrokeReducesApproachStrokesGained()
    {
        var service = CreateService(expectedPutts: 1.87);
        var shot = CreateShot(
            startDistanceToPin: 150,
            startLie: ShotLie.Fairway,
            endDistanceToPin: 20,
            endDistanceUnit: DistanceUnit.Feet,
            endLie: ShotLie.Green,
            penaltyStrokes: 1);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(-0.92, strokesGained, 0.001);
    }

    [TestMethod]
    public void FairwayApproachExpectedStrokesInterpolateLinearly()
    {
        var service = CreateService();

        var expectedStrokes = service.GetApproachExpectedStrokes(137.5, DistanceUnit.Yards, ShotLie.Fairway);

        Assert.AreEqual(2.90, expectedStrokes, 0.001);
    }

    [TestMethod]
    public void FinishStillInApproachZoneUsesApproachTable()
    {
        var service = CreateService();
        var shot = CreateShot(
            startDistanceToPin: 200,
            startLie: ShotLie.Fairway,
            endDistanceToPin: 100,
            endDistanceUnit: DistanceUnit.Yards,
            endLie: ShotLie.Rough,
            endDistanceToGreenEdgeYards: 80);

        var strokesGained = service.CalculateShotSgApproach(shot);

        Assert.AreEqual(-0.78, strokesGained, 0.001);
    }

    private static StrokesGainedApproachService CreateService(
        double expectedPutts = 1.87,
        double expectedAroundGreen = 2.58)
    {
        return new StrokesGainedApproachService(
            new StubPuttingService(expectedPutts),
            new StubAroundGreenService(expectedAroundGreen));
    }

    private static GolfShot CreateShot(
        int par = 4,
        bool isTeeShot = false,
        double startDistanceToPin = 150,
        DistanceUnit startDistanceUnit = DistanceUnit.Yards,
        ShotLie startLie = ShotLie.Fairway,
        double startDistanceToGreenEdgeYards = 80,
        double endDistanceToPin = 20,
        DistanceUnit endDistanceUnit = DistanceUnit.Feet,
        ShotLie endLie = ShotLie.Green,
        double endDistanceToGreenEdgeYards = 0,
        int penaltyStrokes = 0,
        bool holed = false)
    {
        return new GolfShot
        {
            HoleNumber = 1,
            Par = par,
            ShotNumber = isTeeShot ? 1 : 2,
            IsTeeShot = isTeeShot,
            StartDistanceToPin = startDistanceToPin,
            StartDistanceUnit = startDistanceUnit,
            StartLie = startLie,
            StartDistanceToGreenEdgeYards = startDistanceToGreenEdgeYards,
            EndDistanceToPin = endDistanceToPin,
            EndDistanceUnit = endDistanceUnit,
            EndLie = endLie,
            EndDistanceToGreenEdgeYards = endDistanceToGreenEdgeYards,
            PenaltyStrokes = penaltyStrokes,
            Holed = holed
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

    private sealed class StubAroundGreenService : IStrokesGainedAroundGreenService
    {
        private readonly double expectedStrokes;

        public StubAroundGreenService(double expectedStrokes)
        {
            this.expectedStrokes = expectedStrokes;
        }

        public bool IsAroundGreenShot(GolfShot shot) => false;

        public double CalculateShotSgAroundGreen(GolfShot shot) => 0;

        public double CalculateRoundSgAroundGreen(IEnumerable<GolfShot> shots) => 0;

        public double GetAroundGreenExpectedStrokes(double distance, DistanceUnit unit, ShotLie lie)
        {
            return expectedStrokes;
        }
    }
}
