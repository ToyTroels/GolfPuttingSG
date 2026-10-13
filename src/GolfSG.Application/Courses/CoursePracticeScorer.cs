using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Courses;

public sealed class CoursePracticeScorer(IStrokesGainedPuttingService putting,
    IStrokesGainedApproachService approach, IStrokesGainedAroundGreenService around)
{
    public CourseShotResult Score(CoursePracticeShot shot, PracticeCourse course, CourseShotOutcome outcome)
    {
        CoursePracticeService.ValidateCourse(course);
        if (!double.IsFinite(outcome.EndDistanceMeters) || outcome.EndDistanceMeters is < 0 or > 1000 ||
            outcome.PenaltyStrokes is < 0 or > 10 || !Enum.IsDefined(outcome.EndLie) ||
            outcome.Holed != (outcome.EndLie == ShotLie.Holed) ||
            (outcome.Holed ? outcome.EndDistanceMeters != 0 : outcome.EndDistanceMeters <= 0))
            throw new ArgumentException("Angiv en gyldig slutafstand og lie, eller vælg I hul.");
        var start = Expected(shot.DistanceMeters(course), shot.Lie, shot.Category);
        var finish = outcome.Holed ? 0 : Expected(outcome.EndDistanceMeters, outcome.EndLie);
        return new(outcome, start, finish, start - finish - 1 - outcome.PenaltyStrokes, shot.Id);
    }

    private double Expected(double metres, ShotLie lie, PracticeShotCategory? category = null)
    {
        var yards = metres / .9144;
        if (lie == ShotLie.Green) return putting.GetExpectedPutts(yards, DistanceUnit.Yards);
        if (lie != ShotLie.Tee && (category == PracticeShotCategory.AroundGreen || category is null && yards <= 30))
            return around.GetAroundGreenExpectedStrokes(yards, DistanceUnit.Yards, lie == ShotLie.Fairway ? ShotLie.FairwayCut : lie);
        return approach.GetApproachExpectedStrokes(yards, DistanceUnit.Yards, lie);
    }
}
