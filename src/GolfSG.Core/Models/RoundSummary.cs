namespace GolfSG.Core.Models;

public sealed record RoundSummary(
    PuttingRoundSummary Putting,
    ApproachRoundSummary Approach,
    AroundGreenRoundSummary AroundGreen)
{
    public double TotalStrokesGainedPutting => Putting.TotalStrokesGained;
    public int TotalPutts => Putting.TotalPutts;
    public int OnePutts => Putting.OnePutts;
    public int TwoPutts => Putting.TwoPutts;
    public int ThreePuttsOrWorse => Putting.ThreePuttsOrWorse;
    public double AverageFirstPuttDistance => Putting.AverageFirstPuttDistance;
    public HolePuttingData? BestHole => Putting.BestHole;
    public HolePuttingData? WorstHole => Putting.WorstHole;
    public IReadOnlyList<PuttingDistanceBucketSummary> PuttingDistanceBuckets => Putting.DistanceBuckets;
    public double TotalStrokesGainedApproach => Approach.TotalStrokesGained;
    public int TotalApproachShots => Approach.TotalShots;
    public double AverageApproachDistance => Approach.AverageDistance;
    public HolePuttingData? BestApproachHole => Approach.BestHole;
    public HolePuttingData? WorstApproachHole => Approach.WorstHole;
    public double TotalStrokesGainedAroundGreen => AroundGreen.TotalStrokesGained;
    public int TotalAroundGreenShots => AroundGreen.TotalShots;
    public double AverageAroundGreenDistance => AroundGreen.AverageDistanceYards;
    public HolePuttingData? BestAroundGreenHole => AroundGreen.BestHole;
    public HolePuttingData? WorstAroundGreenHole => AroundGreen.WorstHole;
    public double TotalStrokesGained => TotalStrokesGainedPutting + TotalStrokesGainedApproach + TotalStrokesGainedAroundGreen;
}

public sealed record PuttingRoundSummary(
    double TotalStrokesGained,
    int TotalPutts,
    int OnePutts,
    int TwoPutts,
    int ThreePuttsOrWorse,
    double AverageFirstPuttDistance,
    HolePuttingData? BestHole,
    HolePuttingData? WorstHole,
    IReadOnlyList<PuttingDistanceBucketSummary> DistanceBuckets);

public sealed record PuttingDistanceBucketSummary(
    string Name,
    double? MinimumDistanceMeters,
    double? MaximumDistanceMeters,
    int Attempts,
    int TotalPutts,
    double TotalStrokesGained)
{
    public double AverageStrokesGained => Attempts == 0 ? 0 : TotalStrokesGained / Attempts;
}

public sealed record ApproachRoundSummary(
    double TotalStrokesGained,
    int TotalShots,
    double AverageDistance,
    HolePuttingData? BestHole,
    HolePuttingData? WorstHole);

public sealed record AroundGreenRoundSummary(
    double TotalStrokesGained,
    int TotalShots,
    double AverageDistanceYards,
    HolePuttingData? BestHole,
    HolePuttingData? WorstHole);
