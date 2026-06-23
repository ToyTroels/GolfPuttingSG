namespace GolfPuttingSG.Core.Models;

public sealed record HolePuttingData(
    int HoleNumber,
    double FirstPuttDistanceMeters,
    int Putts,
    double ExpectedPutts,
    double StrokesGainedPutting,
    double ApproachDistanceMeters = 0,
    int ApproachShots = 0,
    double ExpectedApproachShots = 0,
    double StrokesGainedApproach = 0)
{
    public bool IsCompleted => FirstPuttDistanceMeters > 0 && Putts > 0;
    public bool IsApproachCompleted => ApproachDistanceMeters > 0 && ApproachShots > 0;
}
