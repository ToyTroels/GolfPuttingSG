using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class LieReferenceTests
{
    [TestMethod]
    public void ApproachReferenceMatchesCalculatorForEveryStoredPoint()
    {
        var putting = new StrokesGainedPuttingService();
        var service = new StrokesGainedApproachService(putting, new StrokesGainedAroundGreenService(putting));
        CollectionAssert.AreEquivalent(
            new[] { ShotLie.Tee, ShotLie.Fairway, ShotLie.Rough, ShotLie.Sand, ShotLie.Recovery },
            StrokesGainedApproachService.Reference.Select(point => point.Lie).Distinct().ToArray());
        foreach (var point in StrokesGainedApproachService.Reference)
        {
            Assert.AreEqual(point.ExpectedShots,
                service.GetApproachExpectedStrokes(point.DistanceYards, DistanceUnit.Yards, point.Lie), 0.000001);
            Assert.AreEqual(point.ExpectedShots,
                service.GetApproachExpectedStrokes(point.DistanceYards * 3, DistanceUnit.Feet, point.Lie), 0.000001);
        }
    }

    [TestMethod]
    public void AroundGreenReferenceMatchesCalculatorForEveryStoredPoint()
    {
        var service = new StrokesGainedAroundGreenService(new StrokesGainedPuttingService());
        CollectionAssert.AreEquivalent(
            new[] { ShotLie.FairwayCut, ShotLie.Rough, ShotLie.Sand, ShotLie.Recovery },
            StrokesGainedAroundGreenService.Reference.Select(point => point.Lie).Distinct().ToArray());
        foreach (var point in StrokesGainedAroundGreenService.Reference)
        {
            Assert.AreEqual(point.ExpectedShots,
                service.GetAroundGreenExpectedStrokes(point.DistanceYards, DistanceUnit.Yards, point.Lie), 0.000001);
            Assert.AreEqual(point.ExpectedShots,
                service.GetAroundGreenExpectedStrokes(point.DistanceYards * 3, DistanceUnit.Feet, point.Lie), 0.000001);
        }
    }
}
