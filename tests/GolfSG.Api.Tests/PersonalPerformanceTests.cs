using GolfSG.Application.Server;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class PersonalPerformanceTests
{
    [TestMethod]
    public void TrendIncludesEachTrackedCategoryAndPreservesMissingResults()
    {
        var hole = StrokesGainedCalculator.BuildHole(1, 3, 2) with
        {
            ApproachDistanceMeters = 100, ApproachShots = 1, StrokesGainedApproach = -.7,
            AroundGreenStartDistanceYards = 20, StrokesGainedAroundGreen = .3
        };
        var full = new Round("full", DateTime.Today, [hole], new(true, true, true));
        var puttingOnly = new Round("putting", DateTime.Today.AddDays(-1), [hole], RoundTrackingOptions.PuttingOnly);
        var trend = PersonalPerformanceMapper.Overview([full, puttingOnly]).Trend;
        Assert.IsNull(trend[0].ApproachSg);
        Assert.IsNull(trend[0].AroundGreenSg);
        Assert.AreEqual(-.7, trend[1].ApproachSg!.Value, 1e-10);
        Assert.AreEqual(.3, trend[1].AroundGreenSg!.Value, 1e-10);
        Assert.AreEqual(hole.StrokesGainedPutting, trend[1].PuttingSg!.Value, 1e-10);
        Assert.AreEqual(trend[1].TotalSg, trend[1].PuttingSg!.Value + trend[1].ApproachSg!.Value + trend[1].AroundGreenSg!.Value, 1e-10);
    }

    [TestMethod]
    public void PracticeNeverChangesNormalRoundTrendOrCategoryAverages()
    {
        var normal = new Round("normal", new DateTime(2026, 10, 15), [StrokesGainedCalculator.BuildHole(1, 3, 2)], ConfiguredHoleCount: 9);
        var practice = new Round("practice", new DateTime(2026, 10, 16), [StrokesGainedCalculator.BuildHole(1, 3, 1)], RoundTrackingOptions.PuttingGame, 1);
        var overview = PersonalPerformanceMapper.Overview([normal, practice]);
        Assert.AreEqual(1, overview.NormalRounds);
        Assert.AreEqual(1, overview.PracticeRounds);
        Assert.HasCount(1, overview.Trend);
        Assert.AreEqual("normal", overview.Trend[0].Id);
        Assert.AreEqual(RoundStatisticsService.CalculateStrokesGained(normal, StrokesGainedCategory.Total), overview.TotalSg, 1e-10);
        Assert.HasCount(1, overview.Categories);
        Assert.AreEqual(1, overview.Categories[0].Rounds);
        Assert.IsTrue(PersonalPerformanceMapper.Detail(practice).Summary.Practice);
    }

    [TestMethod]
    public void DetailKeepsUntrackedCategoriesOutAndDoesNotInventMissingResults()
    {
        var hole = StrokesGainedCalculator.BuildHole(1, 3, 2) with { ApproachDistanceMeters = 100, ApproachShots = 1, StrokesGainedApproach = 99 };
        var round = new Round("r", DateTime.Today, [hole, StrokesGainedCalculator.BuildHole(2, 0, 0)], RoundTrackingOptions.PuttingOnly, 18);
        var detail = PersonalPerformanceMapper.Detail(round);
        Assert.AreEqual(1, detail.Summary.Completed);
        Assert.IsNull(detail.Rows[0].ApproachSg);
        Assert.IsNull(detail.Summary.ApproachSg);
        Assert.IsNull(detail.Summary.AroundGreenSg);
        Assert.AreEqual(hole.StrokesGainedPutting, detail.Summary.PuttingSg!.Value, 1e-10);
        Assert.AreEqual(hole.StrokesGainedPutting, detail.Summary.TotalSg, 1e-10);
        Assert.IsNull(detail.Rows[1].PuttingSg);
    }

    [TestMethod]
    public void EmptyOverviewHasNoInventedAverageOrTrend()
    {
        var overview = PersonalPerformanceMapper.Overview([], 2);
        Assert.IsNull(overview.AverageSg);
        Assert.IsEmpty(overview.Trend);
        Assert.IsEmpty(overview.Categories);
        Assert.AreEqual(2, overview.Unavailable);
    }

    [TestMethod]
    public void CoursePracticeRetainsRepeatedAttemptsAndPenalties()
    {
        var shot = new CoursePracticeShot("s1", "Short putt", new(0, 0), new(1, 0), ShotLie.Green, PracticeShotCategory.Putting, 3, 2);
        var course = new PracticeCourse("course", "Practice green", "local.png", 1, 10, "Measured", [shot]);
        var results = new CourseShotResult[]
        {
            new(new(0, ShotLie.Holed, 0, true), 1.8, 0, .8, "s1"),
            new(new(1, ShotLie.Green, 1, false), 1.8, 1.2, -1.4, "s1")
        };
        var round = new Round("practice", DateTime.Today, [], CoursePractice: new(course, results));
        var detail = PersonalPerformanceMapper.Detail(round);
        Assert.AreEqual(2, detail.Summary.Completed);
        Assert.AreEqual(2, detail.Summary.Planned);
        Assert.AreEqual(3, detail.Summary.Strokes);
        Assert.AreEqual(-.6, detail.Summary.TotalSg, 1e-10);
        Assert.AreEqual("Short putt · attempt 2", detail.Rows[1].Label);
        Assert.AreEqual(1, detail.Rows[1].Penalties);
        Assert.AreEqual(0, PersonalPerformanceMapper.Overview([round]).NormalRounds);
    }
}
