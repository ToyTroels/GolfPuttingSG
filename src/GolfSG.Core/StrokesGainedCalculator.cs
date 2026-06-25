using GolfSG.Core.Models;

namespace GolfSG.Core;

public static class StrokesGainedCalculator
{
    public const double ShortPuttMaximumMeters = PuttingStrokesGainedCalculator.ShortPuttMaximumMeters;

    public static IReadOnlyList<StrokesGainedReferencePoint> PuttingReference =>
        PuttingStrokesGainedCalculator.Reference;

    public static IReadOnlyList<StrokesGainedReferencePoint> ApproachReference =>
        ApproachStrokesGainedCalculator.Reference;

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
        int approachShots = 0)
    {
        var hole = PuttingStrokesGainedCalculator.BuildHole(holeNumber, distanceMeters, putts);
        return ApproachStrokesGainedCalculator.AddApproach(hole, approachDistanceMeters, approachShots);
    }

    public static RoundSummary CalculateRoundSummary(Round round)
    {
        return new RoundSummary(
            PuttingStrokesGainedCalculator.CalculateSummary(round.Holes),
            ApproachStrokesGainedCalculator.CalculateSummary(round.Holes));
    }
}
