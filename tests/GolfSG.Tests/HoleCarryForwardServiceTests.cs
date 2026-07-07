using GolfSG.Core.Models;
using GolfSG.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class HoleCarryForwardServiceTests
{
    [TestMethod]
    public void ApproachGreenFinishCarriesPuttingDistanceWithCap()
    {
        var result = HoleCarryForwardService.FromApproach(CreateApproachRequest(
            trackPutting: true,
            endDistanceMeters: 45,
            endLie: ShotLie.Green,
            maxPuttingDistanceMeters: 30));

        Assert.AreEqual(HoleCarryForwardPuttingSource.Approach, result.PuttingSource);
        Assert.AreEqual(30, result.PuttingDistanceMeters);
        Assert.IsNull(result.AroundGreenStartDistanceMeters);
    }

    [TestMethod]
    public void ApproachGreenFinishDoesNotCarryWhenPuttingIsDisabled()
    {
        var result = HoleCarryForwardService.FromApproach(CreateApproachRequest(
            trackPutting: false,
            endDistanceMeters: 5,
            endLie: ShotLie.Green));

        Assert.AreEqual(HoleCarryForwardResult.Empty, result);
    }

    [TestMethod]
    public void ApproachHoledDoesNotCarryForward()
    {
        var result = HoleCarryForwardService.FromApproach(CreateApproachRequest(
            approachFinishedHoled: true,
            endDistanceMeters: 5,
            endLie: ShotLie.Holed));

        Assert.AreEqual(HoleCarryForwardResult.Empty, result);
    }

    [TestMethod]
    public void ApproachMissCarriesAroundGreenDistanceWithCapAndLie()
    {
        var result = HoleCarryForwardService.FromApproach(CreateApproachRequest(
            trackAroundGreen: true,
            endDistanceMeters: 60,
            endLie: ShotLie.Sand,
            maxAroundGreenDistanceMeters: 50));

        Assert.AreEqual(50, result.AroundGreenStartDistanceMeters);
        Assert.AreEqual("Sand", result.AroundGreenStartLieText);
        Assert.IsNull(result.PuttingDistanceMeters);
    }

    [TestMethod]
    public void ManualAroundGreenStartOverridePreventsApproachDistanceReplacement()
    {
        var result = HoleCarryForwardService.FromApproach(CreateApproachRequest(
            trackAroundGreen: true,
            endDistanceMeters: 8,
            endLie: ShotLie.Rough,
            currentAroundGreenStartDistanceMeters: 4,
            previousCarriedAroundGreenStartDistanceMeters: null));

        Assert.IsNull(result.AroundGreenStartDistanceMeters);
        Assert.AreEqual("Rough", result.AroundGreenStartLieText);
    }

    [TestMethod]
    public void AroundGreenGreenFinishCarriesPuttingDistanceWithCap()
    {
        var result = HoleCarryForwardService.FromAroundGreen(new AroundGreenCarryForwardRequest(
            TrackAroundGreen: true,
            TrackPutting: true,
            AroundGreenFinishedHoled: false,
            EndDistanceMeters: 40,
            EndLie: ShotLie.Green,
            HasManualPuttingDistanceOverride: false,
            MaxPuttingDistanceMeters: 30));

        Assert.AreEqual(HoleCarryForwardPuttingSource.AroundGreen, result.PuttingSource);
        Assert.AreEqual(30, result.PuttingDistanceMeters);
    }

    [TestMethod]
    public void ManualPuttingOverridePreventsAroundGreenCarryForward()
    {
        var result = HoleCarryForwardService.FromAroundGreen(new AroundGreenCarryForwardRequest(
            TrackAroundGreen: true,
            TrackPutting: true,
            AroundGreenFinishedHoled: false,
            EndDistanceMeters: 4,
            EndLie: ShotLie.Green,
            HasManualPuttingDistanceOverride: true,
            MaxPuttingDistanceMeters: 30));

        Assert.AreEqual(HoleCarryForwardResult.Empty, result);
    }

    private static ApproachCarryForwardRequest CreateApproachRequest(
        bool trackPutting = true,
        bool trackAroundGreen = false,
        bool approachFinishedHoled = false,
        double endDistanceMeters = 5,
        ShotLie endLie = ShotLie.Green,
        bool hasManualPuttingDistanceOverride = false,
        double currentAroundGreenStartDistanceMeters = 0,
        double? previousCarriedAroundGreenStartDistanceMeters = null,
        string currentAroundGreenStartLieText = "Rough",
        string? previousCarriedAroundGreenStartLieText = null,
        double maxPuttingDistanceMeters = 30,
        double maxAroundGreenDistanceMeters = 50)
    {
        return new ApproachCarryForwardRequest(
            TrackApproach: true,
            TrackPutting: trackPutting,
            TrackAroundGreen: trackAroundGreen,
            ApproachFinishedHoled: approachFinishedHoled,
            EndDistanceMeters: endDistanceMeters,
            EndLie: endLie,
            HasManualPuttingDistanceOverride: hasManualPuttingDistanceOverride,
            CurrentAroundGreenStartDistanceMeters: currentAroundGreenStartDistanceMeters,
            PreviousCarriedAroundGreenStartDistanceMeters: previousCarriedAroundGreenStartDistanceMeters,
            CurrentAroundGreenStartLieText: currentAroundGreenStartLieText,
            PreviousCarriedAroundGreenStartLieText: previousCarriedAroundGreenStartLieText,
            MaxPuttingDistanceMeters: maxPuttingDistanceMeters,
            MaxAroundGreenDistanceMeters: maxAroundGreenDistanceMeters);
    }
}
