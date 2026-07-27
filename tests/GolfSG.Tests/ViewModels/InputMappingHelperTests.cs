using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class InputMappingHelperTests
{
    [TestMethod]
    public void DistanceInputParserAcceptsCommaAndDotDecimals()
    {
        Assert.IsTrue(DistanceInputParser.TryParse("2,6", out var commaDistance));
        Assert.IsTrue(DistanceInputParser.TryParse("2.6", out var dotDistance));
        Assert.AreEqual(2.6, commaDistance, 0.001);
        Assert.AreEqual(2.6, dotDistance, 0.001);
    }

    [TestMethod]
    public void DistanceInputParserTreatsInvalidAndNegativeDraftsAsZero()
    {
        Assert.AreEqual(0, DistanceInputParser.ParseOrZero("not a number"));
        Assert.AreEqual(0, DistanceInputParser.ParseOrZero("-2,5"));
        Assert.AreEqual(0, DistanceInputParser.ParseOrZero(null));
    }

    [TestMethod]
    public void MetricPuttingQuickPicksIncludeCommonIntermediateDistances()
    {
        var distances = SgDistanceInputPresets.GetPuttingQuickPickMeters(
            PuttingDistanceUnitPreference.Meters);

        CollectionAssert.Contains(distances.ToList(), 2.5);
        CollectionAssert.Contains(distances.ToList(), 3.5);
        CollectionAssert.Contains(distances.ToList(), 4.5);
        CollectionAssert.Contains(distances.ToList(), 5.5);
        CollectionAssert.Contains(distances.ToList(), 7.0);
        Assert.IsTrue(distances.SequenceEqual(distances.OrderBy(distance => distance)));
    }

    [TestMethod]
    public void FeetPuttingQuickPicksKeepReferenceDistances()
    {
        var distances = SgDistanceInputPresets.GetPuttingQuickPickMeters(
            PuttingDistanceUnitPreference.Feet);

        CollectionAssert.AreEqual(
            SgDistanceInputPresets.PuttingMeters.ToList(),
            distances.ToList());
    }

    [TestMethod]
    public void ShotLieLabelsRoundTripDanishLabels()
    {
        Assert.AreEqual(ShotLie.FairwayCut, ShotLieLabels.Parse("Kortklippet"));
        Assert.AreEqual(ShotLie.Recovery, ShotLieLabels.Parse("Problemlie"));
        Assert.AreEqual(ShotLie.Holed, ShotLieLabels.Parse("I hul"));
        Assert.AreEqual("Kortklippet", ShotLieLabels.Format(ShotLie.FairwayCut));
        Assert.AreEqual("Problemlie", ShotLieLabels.Format(ShotLie.Recovery));
        Assert.AreEqual("I hul", ShotLieLabels.Format(ShotLie.Holed));
    }

    [TestMethod]
    public void ShotInputMapperConvertsGreenFinishToFeet()
    {
        var shot = ShotInputMapper.BuildApproachShot(new ApproachShotInput(
            HoleNumber: 1,
            Par: 4,
            IsTeeShot: false,
            StartDistanceMeters: 100,
            StartLie: ShotLie.Fairway,
            EndDistanceMeters: 3,
            EndLie: ShotLie.Green,
            PenaltyStrokes: 0,
            Holed: false));

        Assert.AreEqual(DistanceUnit.Yards, shot.StartDistanceUnit);
        Assert.AreEqual(DistanceConversions.MetersToYards(100), shot.StartDistanceToPin, 0.001);
        Assert.AreEqual(DistanceUnit.Feet, shot.EndDistanceUnit);
        Assert.AreEqual(DistanceConversions.MetersToFeet(3), shot.EndDistanceToPin, 0.001);
    }
}
