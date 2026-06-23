namespace GolfPuttingSG.Core.Models;

public sealed record HolePuttingData(
    int HoleNumber,
    double FirstPuttDistanceMeters,
    int Putts,
    double ExpectedPutts,
    double StrokesGainedPutting)
{
    public bool IsCompleted => FirstPuttDistanceMeters > 0 && Putts > 0;
}
