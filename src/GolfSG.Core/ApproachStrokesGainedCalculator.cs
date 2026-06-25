using GolfSG.Core.Models;

namespace GolfSG.Core;

public static class ApproachStrokesGainedCalculator
{
    private static readonly StrokesGainedReferencePoint[] PgaExpectedApproachBaseline =
    [
        new(18.3, 2.35),
        new(36.6, 2.55),
        new(54.9, 2.68),
        new(73.2, 2.80),
        new(91.4, 2.92),
        new(114.3, 3.04),
        new(137.2, 3.15),
        new(160.0, 3.29),
        new(182.9, 3.45),
        new(205.7, 3.62),
        new(228.6, 3.80),
        new(251.5, 3.96),
        new(274.3, 4.10)
    ];

    public static IReadOnlyList<StrokesGainedReferencePoint> Reference => PgaExpectedApproachBaseline;

    public static double GetExpectedShots(double distanceMeters)
    {
        return StrokesGainedMath.InterpolateExpectedShots(distanceMeters, PgaExpectedApproachBaseline);
    }

    public static HolePuttingData AddApproach(
        HolePuttingData hole,
        double approachDistanceMeters,
        int approachShots)
    {
        var expectedApproachShots = approachDistanceMeters > 0 && approachShots > 0
            ? GetExpectedShots(approachDistanceMeters)
            : 0;

        return hole with
        {
            ApproachDistanceMeters = approachDistanceMeters,
            ApproachShots = approachShots,
            ExpectedApproachShots = expectedApproachShots,
            StrokesGainedApproach = StrokesGainedMath.Calculate(expectedApproachShots, approachShots)
        };
    }

    public static ApproachRoundSummary CalculateSummary(IReadOnlyList<HolePuttingData> holes)
    {
        var completedHoles = holes.Where(hole => hole.IsApproachCompleted).ToList();

        return new ApproachRoundSummary(
            completedHoles.Sum(hole => hole.StrokesGainedApproach),
            completedHoles.Sum(hole => hole.ApproachShots),
            completedHoles.Count == 0 ? 0 : completedHoles.Average(hole => hole.ApproachDistanceMeters),
            completedHoles.MaxBy(hole => hole.StrokesGainedApproach),
            completedHoles.MinBy(hole => hole.StrokesGainedApproach));
    }
}
