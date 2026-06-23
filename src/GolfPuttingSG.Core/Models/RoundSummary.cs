namespace GolfPuttingSG.Core.Models;

public sealed record RoundSummary(
    double TotalStrokesGainedPutting,
    int TotalPutts,
    int OnePutts,
    int TwoPutts,
    int ThreePuttsOrWorse,
    double AverageFirstPuttDistance,
    HolePuttingData? BestHole,
    HolePuttingData? WorstHole,
    double TotalStrokesGainedApproach,
    int TotalApproachShots,
    double AverageApproachDistance,
    HolePuttingData? BestApproachHole,
    HolePuttingData? WorstApproachHole)
{
    public double TotalStrokesGained => TotalStrokesGainedPutting + TotalStrokesGainedApproach;
}
