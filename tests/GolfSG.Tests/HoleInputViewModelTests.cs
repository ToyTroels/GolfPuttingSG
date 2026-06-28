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
}
