namespace GolfSG.Core.Models;

public static class HoleResultMapper
{
    public static HoleResult ToResult(HolePuttingData hole)
    {
        ArgumentNullException.ThrowIfNull(hole);

        return new HoleResult(
            hole.HoleNumber,
            HasPuttingData(hole) ? ToPuttingResult(hole) : null,
            HasApproachData(hole) ? ToApproachResult(hole) : null,
            HasAroundGreenData(hole) ? ToAroundGreenResult(hole) : null);
    }

    public static IReadOnlyList<HoleResult> ToResults(IEnumerable<HolePuttingData> holes)
    {
        ArgumentNullException.ThrowIfNull(holes);

        return holes.Select(ToResult).ToList();
    }

    public static HolePuttingData ToLegacyData(HoleResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var putting = result.Putting;
        var approach = result.Approach;
        var approachShot = approach?.Shot;
        var aroundGreen = result.AroundGreen;

        return new HolePuttingData(
            HoleNumber: result.HoleNumber,
            FirstPuttDistanceMeters: putting?.FirstPuttDistanceMeters ?? 0,
            Putts: putting?.Putts ?? 0,
            ExpectedPutts: putting?.ExpectedPutts ?? 0,
            StrokesGainedPutting: putting?.StrokesGained ?? 0,
            ApproachDistanceMeters: approach?.DistanceMeters ?? 0,
            ApproachShots: approach?.Shots ?? 0,
            ExpectedApproachShots: approach?.ExpectedStartStrokes ?? 0,
            StrokesGainedApproach: approach?.StrokesGained ?? 0,
            ApproachStartDistanceYards: approachShot?.StartDistanceToPin ?? 0,
            ApproachStartLie: approachShot?.StartLie ?? ShotLie.Fairway,
            ApproachDistanceToGreenEdgeYards: approachShot?.StartDistanceToGreenEdgeYards ?? 0,
            ApproachEndDistance: approachShot?.EndDistanceToPin ?? 0,
            ApproachEndDistanceUnit: approachShot?.EndDistanceUnit ?? DistanceUnit.Feet,
            ApproachEndLie: approachShot?.EndLie ?? ShotLie.Green,
            ApproachEndDistanceToGreenEdgeYards: approachShot?.EndDistanceToGreenEdgeYards ?? 0,
            ApproachPenaltyStrokes: approachShot?.PenaltyStrokes ?? 0,
            ApproachHoled: approachShot?.Holed ?? false,
            ApproachPar: approachShot?.Par ?? 4,
            ApproachIsTeeShot: approachShot?.IsTeeShot ?? false,
            ExpectedApproachFinishStrokes: approach?.ExpectedFinishStrokes ?? 0,
            AroundGreenStartDistanceYards: aroundGreen?.StartDistanceYards ?? 0,
            AroundGreenStartLie: aroundGreen?.StartLie ?? ShotLie.Rough,
            AroundGreenDistanceToGreenEdgeYards: aroundGreen?.StartDistanceToGreenEdgeYards ?? 0,
            AroundGreenEndDistance: aroundGreen?.EndDistance ?? 0,
            AroundGreenEndDistanceUnit: aroundGreen?.EndDistanceUnit ?? DistanceUnit.Feet,
            AroundGreenEndLie: aroundGreen?.EndLie ?? ShotLie.Green,
            AroundGreenPenaltyStrokes: aroundGreen?.PenaltyStrokes ?? 0,
            AroundGreenHoled: aroundGreen?.Holed ?? false,
            ExpectedAroundGreenStartStrokes: aroundGreen?.ExpectedStartStrokes ?? 0,
            ExpectedAroundGreenFinishStrokes: aroundGreen?.ExpectedFinishStrokes ?? 0,
            StrokesGainedAroundGreen: aroundGreen?.StrokesGained ?? 0,
            AroundGreenShots: aroundGreen?.Shots.Count > 0 ? aroundGreen.Shots : null);
    }

    private static PuttingResult ToPuttingResult(HolePuttingData hole) =>
        new(
            hole.FirstPuttDistanceMeters,
            hole.Putts,
            hole.ExpectedPutts,
            hole.StrokesGainedPutting);

    private static ApproachResult ToApproachResult(HolePuttingData hole) =>
        new(
            hole.ApproachDistanceMeters,
            hole.ApproachShots,
            hole.ExpectedApproachShots,
            hole.ExpectedApproachFinishStrokes,
            hole.StrokesGainedApproach,
            HasApproachShotData(hole)
                ? new GolfShot
                {
                    HoleNumber = hole.HoleNumber,
                    ShotNumber = 1,
                    StartDistanceToPin = hole.ApproachStartDistanceYards,
                    StartDistanceUnit = DistanceUnit.Yards,
                    StartLie = hole.ApproachStartLie,
                    StartDistanceToGreenEdgeYards = hole.ApproachDistanceToGreenEdgeYards,
                    EndDistanceToPin = hole.ApproachEndDistance,
                    EndDistanceUnit = hole.ApproachEndDistanceUnit,
                    EndLie = hole.ApproachEndLie,
                    EndDistanceToGreenEdgeYards = hole.ApproachEndDistanceToGreenEdgeYards,
                    PenaltyStrokes = hole.ApproachPenaltyStrokes,
                    Holed = hole.ApproachHoled,
                    Par = hole.ApproachPar,
                    IsTeeShot = hole.ApproachIsTeeShot
                }
                : null);

    private static AroundGreenResult ToAroundGreenResult(HolePuttingData hole) =>
        new(
            hole.AroundGreenShots ?? [],
            hole.AroundGreenStartDistanceYards,
            hole.AroundGreenStartLie,
            hole.AroundGreenDistanceToGreenEdgeYards,
            hole.AroundGreenEndDistance,
            hole.AroundGreenEndDistanceUnit,
            hole.AroundGreenEndLie,
            hole.AroundGreenPenaltyStrokes,
            hole.AroundGreenHoled,
            hole.ExpectedAroundGreenStartStrokes,
            hole.ExpectedAroundGreenFinishStrokes,
            hole.StrokesGainedAroundGreen);

    private static bool HasPuttingData(HolePuttingData hole) =>
        hole.FirstPuttDistanceMeters > 0 ||
        hole.Putts > 0 ||
        hole.ExpectedPutts != 0 ||
        hole.StrokesGainedPutting != 0;

    private static bool HasApproachData(HolePuttingData hole) =>
        hole.ApproachDistanceMeters > 0 ||
        hole.ApproachShots > 0 ||
        hole.ExpectedApproachShots != 0 ||
        hole.StrokesGainedApproach != 0 ||
        hole.ExpectedApproachFinishStrokes != 0 ||
        HasApproachShotData(hole);

    private static bool HasApproachShotData(HolePuttingData hole) =>
        hole.ApproachStartDistanceYards > 0 ||
        hole.ApproachDistanceToGreenEdgeYards > 0 ||
        hole.ApproachEndDistance > 0 ||
        hole.ApproachEndDistanceToGreenEdgeYards > 0 ||
        hole.ApproachPenaltyStrokes > 0 ||
        hole.ApproachHoled ||
        hole.ApproachIsTeeShot ||
        hole.ApproachStartLie != ShotLie.Fairway ||
        hole.ApproachEndLie != ShotLie.Green ||
        hole.ApproachEndDistanceUnit != DistanceUnit.Feet ||
        hole.ApproachPar != 4;

    private static bool HasAroundGreenData(HolePuttingData hole) =>
        hole.AroundGreenShotCount > 0 ||
        hole.AroundGreenDistanceToGreenEdgeYards > 0 ||
        hole.AroundGreenEndDistance > 0 ||
        hole.AroundGreenPenaltyStrokes > 0 ||
        hole.AroundGreenHoled ||
        hole.ExpectedAroundGreenStartStrokes != 0 ||
        hole.ExpectedAroundGreenFinishStrokes != 0 ||
        hole.StrokesGainedAroundGreen != 0 ||
        hole.AroundGreenStartLie != ShotLie.Rough ||
        hole.AroundGreenEndLie != ShotLie.Green ||
        hole.AroundGreenEndDistanceUnit != DistanceUnit.Feet;
}
