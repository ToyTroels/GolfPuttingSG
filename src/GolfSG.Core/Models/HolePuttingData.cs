namespace GolfSG.Core.Models;

public sealed record HolePuttingData(
    int HoleNumber,
    double FirstPuttDistanceMeters,
    int Putts,
    double ExpectedPutts,
    double StrokesGainedPutting,
    double ApproachDistanceMeters = 0,
    int ApproachShots = 0,
    double ExpectedApproachShots = 0,
    double StrokesGainedApproach = 0,
    double ApproachStartDistanceYards = 0,
    ShotLie ApproachStartLie = ShotLie.Fairway,
    double ApproachDistanceToGreenEdgeYards = 0,
    double ApproachEndDistance = 0,
    DistanceUnit ApproachEndDistanceUnit = DistanceUnit.Feet,
    ShotLie ApproachEndLie = ShotLie.Green,
    double ApproachEndDistanceToGreenEdgeYards = 0,
    int ApproachPenaltyStrokes = 0,
    bool ApproachHoled = false,
    int ApproachPar = 4,
    bool ApproachIsTeeShot = false,
    double ExpectedApproachFinishStrokes = 0,
    double AroundGreenStartDistanceYards = 0,
    ShotLie AroundGreenStartLie = ShotLie.Rough,
    double AroundGreenDistanceToGreenEdgeYards = 0,
    double AroundGreenEndDistance = 0,
    DistanceUnit AroundGreenEndDistanceUnit = DistanceUnit.Feet,
    ShotLie AroundGreenEndLie = ShotLie.Green,
    int AroundGreenPenaltyStrokes = 0,
    bool AroundGreenHoled = false,
    double ExpectedAroundGreenStartStrokes = 0,
    double ExpectedAroundGreenFinishStrokes = 0,
    double StrokesGainedAroundGreen = 0,
    IReadOnlyList<GolfShot>? AroundGreenShots = null)
{
    public bool IsCompleted => FirstPuttDistanceMeters > 0 && Putts > 0;
    public bool IsApproachCompleted => ApproachDistanceMeters > 0 && ApproachShots > 0;
    public bool IsAroundGreenCompleted => AroundGreenShotCount > 0;
    public int AroundGreenShotCount => AroundGreenShots?.Count > 0 ? AroundGreenShots.Count : AroundGreenStartDistanceYards > 0 ? 1 : 0;
}
