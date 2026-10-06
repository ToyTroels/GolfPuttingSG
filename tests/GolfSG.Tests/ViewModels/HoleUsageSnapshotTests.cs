using GolfSG.Application.Diagnostics;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class HoleUsageSnapshotTests
{
    [TestMethod]
    public void IncompleteVisitsDoNotCountAndRepeatedCompletionIsNotACorrection()
    {
        var snapshot = new HoleUsageSnapshot();
        var hole = StrokesGainedCalculator.BuildHole(1, 3, 2);
        Assert.AreEqual(UsageVisitResult.None, snapshot.Finish(hole, false));
        Assert.AreEqual(UsageVisitResult.FirstCompletion, snapshot.Finish(hole, true));
        Assert.AreEqual(UsageVisitResult.None, snapshot.Finish(StrokesGainedCalculator.BuildHole(1, 3, 2), true));
        Assert.AreEqual(UsageVisitResult.Correction, snapshot.Finish(StrokesGainedCalculator.BuildHole(1, 3, 3), true));
        Assert.AreEqual(UsageVisitResult.None, snapshot.Finish(StrokesGainedCalculator.BuildHole(1, 3, 3), true));
    }

    [TestMethod]
    public void ReopenedSavedHoleCountsAsCorrectionOnlyIfChanged()
    {
        var snapshot = new HoleUsageSnapshot();
        var hole = StrokesGainedCalculator.BuildHole(1, 3, 2);
        snapshot.MarkExisting(hole);
        Assert.AreEqual(UsageVisitResult.None, snapshot.Finish(hole, true));
        Assert.AreEqual(UsageVisitResult.Correction, snapshot.Finish(hole with { Putts = 3 }, true));
    }

    [TestMethod]
    public void EquivalentShotListsAreNotCorrections()
    {
        var snapshot = new HoleUsageSnapshot();
        var shot = new GolfShot { HoleNumber = 1, EndLie = ShotLie.Green };
        var hole = StrokesGainedCalculator.BuildHole(1, 3, 2) with { AroundGreenShots = new[] { shot } };
        snapshot.MarkExisting(hole);
        Assert.AreEqual(UsageVisitResult.None, snapshot.Finish(hole with
        {
            AroundGreenShots = new[] { new GolfShot { HoleNumber = 1, EndLie = ShotLie.Green } }
        }, true));
    }
}
