using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class HoleResultMapperTests
{
    [TestMethod]
    public void ToResultSplitsLegacyHoleIntoCategoryResults()
    {
        var hole = CreateCompleteHole();

        var result = HoleResultMapper.ToResult(hole);

        Assert.AreEqual(7, result.HoleNumber);
        Assert.IsTrue(result.IsPuttingCompleted);
        Assert.IsTrue(result.IsApproachCompleted);
        Assert.IsTrue(result.IsAroundGreenCompleted);
        Assert.AreEqual(3.2, result.Putting!.FirstPuttDistanceMeters);
        Assert.AreEqual(2, result.Putting.Putts);
        Assert.AreEqual(0.4, result.Putting.StrokesGained);
        Assert.AreEqual(128, result.Approach!.Shot!.StartDistanceToPin);
        Assert.AreEqual(ShotLie.Rough, result.Approach.Shot.StartLie);
        Assert.AreEqual(12, result.AroundGreen!.StartDistanceYards);
        Assert.AreEqual(1, result.AroundGreen.ShotCount);
    }

    [TestMethod]
    public void ToLegacyDataRoundTripsCompleteHole()
    {
        var hole = CreateCompleteHole();

        var roundTripped = HoleResultMapper.ToLegacyData(HoleResultMapper.ToResult(hole));

        Assert.AreEqual(hole, roundTripped);
    }

    [TestMethod]
    public void ToLegacyDataRoundTripsOldStyleApproachWithoutShotLevelData()
    {
        var hole = new HolePuttingData(
            HoleNumber: 3,
            FirstPuttDistanceMeters: 0,
            Putts: 0,
            ExpectedPutts: 0,
            StrokesGainedPutting: 0,
            ApproachDistanceMeters: 95.5,
            ApproachShots: 2,
            ExpectedApproachShots: 2.9,
            StrokesGainedApproach: -0.1);

        var result = HoleResultMapper.ToResult(hole);
        var roundTripped = HoleResultMapper.ToLegacyData(result);

        Assert.IsNull(result.Putting);
        Assert.IsNotNull(result.Approach);
        Assert.IsNull(result.Approach!.Shot);
        Assert.AreEqual(hole, roundTripped);
    }

    [TestMethod]
    public void ToResultPreservesMultipleAroundGreenShots()
    {
        var shots = new[]
        {
            new GolfShot
            {
                HoleNumber = 5,
                ShotNumber = 1,
                StartDistanceToPin = 20,
                StartDistanceUnit = DistanceUnit.Yards,
                StartLie = ShotLie.Rough,
                EndDistanceToPin = 8,
                EndDistanceUnit = DistanceUnit.Yards,
                EndLie = ShotLie.Rough
            },
            new GolfShot
            {
                HoleNumber = 5,
                ShotNumber = 2,
                StartDistanceToPin = 8,
                StartDistanceUnit = DistanceUnit.Yards,
                StartLie = ShotLie.Rough,
                EndDistanceToPin = 4,
                EndDistanceUnit = DistanceUnit.Feet,
                EndLie = ShotLie.Green
            }
        };

        var hole = new HolePuttingData(
            HoleNumber: 5,
            FirstPuttDistanceMeters: 1.2,
            Putts: 1,
            ExpectedPutts: 1.1,
            StrokesGainedPutting: 0.1,
            AroundGreenStartDistanceYards: 20,
            AroundGreenStartLie: ShotLie.Rough,
            AroundGreenEndDistance: 4,
            AroundGreenEndDistanceUnit: DistanceUnit.Feet,
            AroundGreenEndLie: ShotLie.Green,
            ExpectedAroundGreenStartStrokes: 2.5,
            ExpectedAroundGreenFinishStrokes: 1.1,
            StrokesGainedAroundGreen: -0.4,
            AroundGreenShots: shots);

        var result = HoleResultMapper.ToResult(hole);
        var roundTripped = HoleResultMapper.ToLegacyData(result);

        Assert.AreEqual(2, result.AroundGreen!.ShotCount);
        CollectionAssert.AreEqual(shots, result.AroundGreen.Shots.ToArray());
        Assert.AreEqual(hole, roundTripped);
    }

    [TestMethod]
    public void EmptyLegacyHoleMapsToEmptyResultWithoutCreatingCategoryData()
    {
        var hole = new HolePuttingData(1, 0, 0, 0, 0);

        var result = HoleResultMapper.ToResult(hole);
        var roundTripped = HoleResultMapper.ToLegacyData(result);

        Assert.IsNull(result.Putting);
        Assert.IsNull(result.Approach);
        Assert.IsNull(result.AroundGreen);
        Assert.AreEqual(hole, roundTripped);
    }

    private static HolePuttingData CreateCompleteHole()
    {
        var aroundGreenShot = new GolfShot
        {
            HoleNumber = 7,
            ShotNumber = 2,
            StartDistanceToPin = 12,
            StartDistanceUnit = DistanceUnit.Yards,
            StartLie = ShotLie.Sand,
            StartDistanceToGreenEdgeYards = 4,
            EndDistanceToPin = 6,
            EndDistanceUnit = DistanceUnit.Feet,
            EndLie = ShotLie.Green,
            PenaltyStrokes = 1
        };

        return new HolePuttingData(
            HoleNumber: 7,
            FirstPuttDistanceMeters: 3.2,
            Putts: 2,
            ExpectedPutts: 1.6,
            StrokesGainedPutting: 0.4,
            ApproachDistanceMeters: 117.04,
            ApproachShots: 1,
            ExpectedApproachShots: 3.1,
            StrokesGainedApproach: 0.2,
            ApproachStartDistanceYards: 128,
            ApproachStartLie: ShotLie.Rough,
            ApproachDistanceToGreenEdgeYards: 14,
            ApproachEndDistance: 12,
            ApproachEndDistanceUnit: DistanceUnit.Yards,
            ApproachEndLie: ShotLie.Sand,
            ApproachEndDistanceToGreenEdgeYards: 4,
            ApproachPenaltyStrokes: 1,
            ApproachHoled: false,
            ApproachPar: 5,
            ApproachIsTeeShot: true,
            ExpectedApproachFinishStrokes: 2.4,
            AroundGreenStartDistanceYards: 12,
            AroundGreenStartLie: ShotLie.Sand,
            AroundGreenDistanceToGreenEdgeYards: 4,
            AroundGreenEndDistance: 6,
            AroundGreenEndDistanceUnit: DistanceUnit.Feet,
            AroundGreenEndLie: ShotLie.Green,
            AroundGreenPenaltyStrokes: 1,
            AroundGreenHoled: false,
            ExpectedAroundGreenStartStrokes: 2.2,
            ExpectedAroundGreenFinishStrokes: 1.3,
            StrokesGainedAroundGreen: -0.1,
            AroundGreenShots: [aroundGreenShot]);
    }
}
