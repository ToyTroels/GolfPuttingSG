namespace GolfSG.Core.Models;

public sealed record HoleResult(
    int HoleNumber,
    PuttingResult? Putting,
    ApproachResult? Approach,
    AroundGreenResult? AroundGreen)
{
    public bool IsPuttingCompleted => Putting is { IsCompleted: true };
    public bool IsApproachCompleted => Approach is { IsCompleted: true };
    public bool IsAroundGreenCompleted => AroundGreen is { IsCompleted: true };

    public double TotalStrokesGained =>
        (Putting?.StrokesGained ?? 0) +
        (Approach?.StrokesGained ?? 0) +
        (AroundGreen?.StrokesGained ?? 0);
}

public sealed record PuttingResult(
    double FirstPuttDistanceMeters,
    int Putts,
    double ExpectedPutts,
    double StrokesGained)
{
    public bool IsCompleted => FirstPuttDistanceMeters > 0 && Putts > 0;
}

public sealed record ApproachResult(
    double DistanceMeters,
    int Shots,
    double ExpectedStartStrokes,
    double ExpectedFinishStrokes,
    double StrokesGained,
    GolfShot? Shot)
{
    public bool IsCompleted => DistanceMeters > 0 && Shots > 0;

    public bool HasShotLevelData => Shot is not null;
}

public sealed record AroundGreenResult(
    IReadOnlyList<GolfShot> Shots,
    double StartDistanceYards,
    ShotLie StartLie,
    double StartDistanceToGreenEdgeYards,
    double EndDistance,
    DistanceUnit EndDistanceUnit,
    ShotLie EndLie,
    int PenaltyStrokes,
    bool Holed,
    double ExpectedStartStrokes,
    double ExpectedFinishStrokes,
    double StrokesGained)
{
    public bool IsCompleted => ShotCount > 0;

    public int ShotCount => Shots.Count > 0 ? Shots.Count : StartDistanceYards > 0 ? 1 : 0;
}
