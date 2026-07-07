using System.Globalization;
using GolfSG.Core.Models;
using GolfSG.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class HoleInputViewModelTests
{
    [TestMethod]
    public void AddAnotherAroundGreenShotKeepsPreviousShotVisibleAsSummary()
    {
        var viewModel = new HoleInputViewModel(1);
        viewModel.SetTracking(new RoundTrackingOptions(
            TrackPutting: true,
            TrackApproach: false,
            TrackAroundGreen: true));
        viewModel.AroundGreenStartDistanceYards = 12;
        viewModel.SelectAroundGreenStartLie("Rough");
        viewModel.AroundGreenEndDistance = 7;
        viewModel.SelectAroundGreenEndLie("Rough");

        viewModel.AddAnotherAroundGreenShot();

        Assert.IsTrue(viewModel.HasCompletedAroundGreenShots);
        Assert.HasCount(1, viewModel.CompletedAroundGreenShotSummaries);
        Assert.AreEqual("Slag omkring green 1", viewModel.CompletedAroundGreenShotSummaries[0].Title);
        StringAssert.Contains(viewModel.CompletedAroundGreenShotSummaries[0].StartText, "12 m");
        StringAssert.Contains(viewModel.CompletedAroundGreenShotSummaries[0].EndText, "7,0 m");
        Assert.AreEqual("Omkring green slag 2", viewModel.AroundGreenShotTitle);
        Assert.AreEqual("7 m", viewModel.AroundGreenStartDistanceDisplayText);
    }

    [TestMethod]
    public void ApproachEndDistanceUsesWholeMetersOnly()
    {
        var viewModel = new HoleInputViewModel(1);
        viewModel.SetTracking(new RoundTrackingOptions(
            TrackPutting: true,
            TrackApproach: true,
            TrackAroundGreen: false));

        viewModel.ApproachEndDistance = 2.6;

        Assert.AreEqual("3", viewModel.ApproachEndDistanceText);
        Assert.AreEqual("3 m", viewModel.ApproachEndDistanceDisplayText);
    }

    [TestMethod]
    public void ApproachFinishOnGreenCarriesDistanceToPutting()
    {
        var viewModel = new HoleInputViewModel(1);
        viewModel.SetTracking(new RoundTrackingOptions(
            TrackPutting: true,
            TrackApproach: true,
            TrackAroundGreen: false));

        viewModel.SelectApproachEndLie("Green");
        viewModel.ApproachEndDistance = 4;

        Assert.AreEqual(4, viewModel.FirstPuttDistanceMeters, 0.001);
        Assert.AreEqual("4", viewModel.DistanceText);
    }

    [TestMethod]
    public void ApproachMissCarriesDistanceAndLieToAroundGreen()
    {
        var viewModel = new HoleInputViewModel(1);
        viewModel.SetTracking(new RoundTrackingOptions(
            TrackPutting: false,
            TrackApproach: true,
            TrackAroundGreen: true));

        viewModel.SelectApproachEndLie("Sand");
        viewModel.ApproachEndDistance = 8;

        Assert.AreEqual(8, viewModel.AroundGreenStartDistanceYards, 0.001);
        Assert.AreEqual("Sand", viewModel.AroundGreenStartLieText);
    }

    [TestMethod]
    public void AroundGreenFinishOnGreenCarriesDistanceToPutting()
    {
        var viewModel = new HoleInputViewModel(1);
        viewModel.SetTracking(new RoundTrackingOptions(
            TrackPutting: true,
            TrackApproach: false,
            TrackAroundGreen: true));

        viewModel.SelectAroundGreenEndLie("Green");
        viewModel.AroundGreenEndDistance = 2.6;

        Assert.AreEqual(2.6, viewModel.FirstPuttDistanceMeters, 0.001);
        Assert.AreEqual("2.6", viewModel.DistanceText);
    }

    [TestMethod]
    public void ManualPuttingDistanceOverridePreventsApproachCarryForward()
    {
        var viewModel = new HoleInputViewModel(1);
        viewModel.SetTracking(new RoundTrackingOptions(
            TrackPutting: true,
            TrackApproach: true,
            TrackAroundGreen: false));
        viewModel.DistanceText = "3";

        viewModel.SelectApproachEndLie("Green");
        viewModel.ApproachEndDistance = 5;

        Assert.AreEqual(3, viewModel.FirstPuttDistanceMeters, 0.001);
        Assert.AreEqual("3", viewModel.DistanceText);
    }

    [TestMethod]
    public void ToHoleBuildsShotLevelApproachInputThroughMapper()
    {
        var viewModel = new HoleInputViewModel(1);
        viewModel.SetTracking(new RoundTrackingOptions(
            TrackPutting: true,
            TrackApproach: true,
            TrackAroundGreen: false));
        viewModel.ApproachStartDistanceYards = 120;
        viewModel.SelectApproachStartLie("Fairway");
        viewModel.SelectApproachEndLie("Green");
        viewModel.ApproachEndDistance = 4;
        viewModel.ApproachPenaltyStrokes = 1;

        var hole = viewModel.ToHole();

        Assert.AreEqual(120 / 0.9144, hole.ApproachStartDistanceYards, 0.001);
        Assert.AreEqual(ShotLie.Fairway, hole.ApproachStartLie);
        Assert.AreEqual(4 / 0.3048, hole.ApproachEndDistance, 0.001);
        Assert.AreEqual(DistanceUnit.Feet, hole.ApproachEndDistanceUnit);
        Assert.AreEqual(ShotLie.Green, hole.ApproachEndLie);
        Assert.AreEqual(1, hole.ApproachPenaltyStrokes);
    }

    [TestMethod]
    [DoNotParallelize]
    public void LoadFormatsDecimalDistancesWithInvariantDecimalSeparator()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("da-DK");
            var viewModel = new HoleInputViewModel(1);

            viewModel.Load(new HolePuttingData(1, 5.5, 2, 0, 0, ApproachDistanceMeters: 7.5));

            Assert.AreEqual("5.5", viewModel.DistanceText);
            Assert.AreEqual(5.5, viewModel.FirstPuttDistanceMeters, 0.001);
            Assert.AreEqual("7.5", viewModel.ApproachDistanceText);
            Assert.AreEqual(7.5, viewModel.ApproachDistanceMeters, 0.001);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
