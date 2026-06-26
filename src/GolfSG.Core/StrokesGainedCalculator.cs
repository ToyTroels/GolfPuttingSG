using GolfSG.Core.Models;

namespace GolfSG.Core;

public static class StrokesGainedCalculator
{
    public const double ShortPuttMaximumMeters = PuttingStrokesGainedCalculator.ShortPuttMaximumMeters;

    public static IReadOnlyList<StrokesGainedReferencePoint> PuttingReference =>
        PuttingStrokesGainedCalculator.Reference;

    public static IReadOnlyList<StrokesGainedReferencePoint> ApproachReference =>
        ApproachStrokesGainedCalculator.Reference;

    public static IStrokesGainedAroundGreenService AroundGreenService { get; } =
        new StrokesGainedAroundGreenService(new StrokesGainedPuttingService());

    public static IStrokesGainedApproachService ApproachService { get; } =
        new StrokesGainedApproachService(new StrokesGainedPuttingService(), AroundGreenService);

    public static double GetExpectedPutts(double distanceMeters)
    {
        return PuttingStrokesGainedCalculator.GetExpectedPutts(distanceMeters);
    }

    public static double GetExpectedApproachShots(double distanceMeters)
    {
        return ApproachStrokesGainedCalculator.GetExpectedShots(distanceMeters);
    }

    public static double CalculateStrokesGained(double expectedShots, int actualShots)
    {
        return StrokesGainedMath.Calculate(expectedShots, actualShots);
    }

    public static HolePuttingData BuildHole(
        int holeNumber,
        double distanceMeters,
        int putts,
        double approachDistanceMeters = 0,
        int approachShots = 0,
        GolfShot? approachShot = null,
        GolfShot? aroundGreenShot = null)
    {
        var hole = PuttingStrokesGainedCalculator.BuildHole(holeNumber, distanceMeters, putts);
        hole = approachShot is null
            ? ApproachStrokesGainedCalculator.AddApproach(hole, approachDistanceMeters, approachShots)
            : AddApproach(hole, approachShot);
        return aroundGreenShot is null ? hole : AddAroundGreen(hole, aroundGreenShot);
    }

    public static HolePuttingData AddApproach(HolePuttingData hole, GolfShot shot)
    {
        if (shot.StartDistanceToPin <= 0 || !ApproachService.IsApproachShot(shot))
        {
            return hole;
        }

        var startExpectedStrokes = ApproachService.GetApproachExpectedStrokes(
            shot.StartDistanceToPin,
            shot.StartDistanceUnit,
            shot.StartLie);

        var finishExpectedStrokes = GetApproachFinishExpectedStrokes(shot);

        return hole with
        {
            ApproachDistanceMeters = ToYards(shot.StartDistanceToPin, shot.StartDistanceUnit) * 0.9144,
            ApproachShots = 1,
            ExpectedApproachShots = startExpectedStrokes,
            StrokesGainedApproach = ApproachService.CalculateShotSgApproach(shot),
            ApproachStartDistanceYards = ToYards(shot.StartDistanceToPin, shot.StartDistanceUnit),
            ApproachStartLie = shot.StartLie,
            ApproachDistanceToGreenEdgeYards = shot.StartDistanceToGreenEdgeYards,
            ApproachEndDistance = shot.EndDistanceToPin,
            ApproachEndDistanceUnit = shot.EndDistanceUnit,
            ApproachEndLie = shot.EndLie,
            ApproachEndDistanceToGreenEdgeYards = shot.EndDistanceToGreenEdgeYards,
            ApproachPenaltyStrokes = shot.PenaltyStrokes,
            ApproachHoled = shot.Holed || shot.EndLie == ShotLie.Holed,
            ApproachPar = shot.Par,
            ApproachIsTeeShot = shot.IsTeeShot,
            ExpectedApproachFinishStrokes = finishExpectedStrokes
        };
    }

    public static RoundSummary CalculateRoundSummary(Round round)
    {
        return new RoundSummary(
            PuttingStrokesGainedCalculator.CalculateSummary(round.Holes),
            ApproachStrokesGainedCalculator.CalculateSummary(round.Holes),
            CalculateAroundGreenSummary(round.Holes));
    }

    public static HolePuttingData AddAroundGreen(HolePuttingData hole, GolfShot shot)
    {
        if (shot.StartDistanceToPin <= 0 || !AroundGreenService.IsAroundGreenShot(shot))
        {
            return hole;
        }

        var startExpectedStrokes = AroundGreenService.GetAroundGreenExpectedStrokes(
            shot.StartDistanceToPin,
            shot.StartDistanceUnit,
            shot.StartLie);

        var finishExpectedStrokes = shot.Holed || shot.EndLie == ShotLie.Holed
            ? 0
            : shot.EndLie == ShotLie.Green
                ? new StrokesGainedPuttingService().GetExpectedPutts(shot.EndDistanceToPin, shot.EndDistanceUnit)
                : AroundGreenService.GetAroundGreenExpectedStrokes(shot.EndDistanceToPin, shot.EndDistanceUnit, shot.EndLie);

        return hole with
        {
            AroundGreenStartDistanceYards = ToYards(shot.StartDistanceToPin, shot.StartDistanceUnit),
            AroundGreenStartLie = shot.StartLie,
            AroundGreenDistanceToGreenEdgeYards = shot.StartDistanceToGreenEdgeYards,
            AroundGreenEndDistance = shot.EndDistanceToPin,
            AroundGreenEndDistanceUnit = shot.EndDistanceUnit,
            AroundGreenEndLie = shot.EndLie,
            AroundGreenPenaltyStrokes = shot.PenaltyStrokes,
            AroundGreenHoled = shot.Holed,
            ExpectedAroundGreenStartStrokes = startExpectedStrokes,
            ExpectedAroundGreenFinishStrokes = finishExpectedStrokes,
            StrokesGainedAroundGreen = AroundGreenService.CalculateShotSgAroundGreen(shot)
        };
    }

    private static AroundGreenRoundSummary CalculateAroundGreenSummary(IReadOnlyList<HolePuttingData> holes)
    {
        var completedHoles = holes.Where(hole => hole.IsAroundGreenCompleted).ToList();

        return new AroundGreenRoundSummary(
            completedHoles.Sum(hole => hole.StrokesGainedAroundGreen),
            completedHoles.Count,
            completedHoles.Count == 0 ? 0 : completedHoles.Average(hole => hole.AroundGreenStartDistanceYards),
            completedHoles.MaxBy(hole => hole.StrokesGainedAroundGreen),
            completedHoles.MinBy(hole => hole.StrokesGainedAroundGreen));
    }

    private static double ToYards(double distance, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Feet => distance / 3,
            DistanceUnit.Yards => distance,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported distance unit.")
        };
    }

    private static double GetApproachFinishExpectedStrokes(GolfShot shot)
    {
        if (shot.Holed || shot.EndLie == ShotLie.Holed)
        {
            return 0;
        }

        if (shot.EndLie == ShotLie.Green)
        {
            return new StrokesGainedPuttingService().GetExpectedPutts(shot.EndDistanceToPin, shot.EndDistanceUnit);
        }

        if (ToYards(shot.EndDistanceToPin, shot.EndDistanceUnit) <= 30 && shot.EndLie != ShotLie.Tee)
        {
            var aroundGreenLie = shot.EndLie == ShotLie.Fairway ? ShotLie.FairwayCut : shot.EndLie;
            return AroundGreenService.GetAroundGreenExpectedStrokes(
                shot.EndDistanceToPin,
                shot.EndDistanceUnit,
                aroundGreenLie);
        }

        return ApproachService.GetApproachExpectedStrokes(
            shot.EndDistanceToPin,
            shot.EndDistanceUnit,
            shot.EndLie);
    }
}
