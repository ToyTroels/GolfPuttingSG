using GolfPuttingSG.Core.Models;

namespace GolfPuttingSG.Core;

public static class StrokesGainedCalculator
{
    private static readonly (double DistanceMeters, double ExpectedPutts)[] PgaExpectedPuttsBaseline =
    [
        (0.3, 1.00),
        (0.6, 1.01),
        (0.9, 1.05),
        (1.2, 1.10),
        (1.5, 1.24),
        (1.8, 1.34),
        (2.4, 1.53),
        (3.0, 1.61),
        (4.6, 1.78),
        (6.1, 1.87),
        (9.1, 1.98),
        (12.2, 2.06),
        (15.2, 2.14),
        (18.3, 2.21),
        (27.4, 2.36)
    ];

    private static readonly (double DistanceMeters, double ExpectedShots)[] PgaExpectedApproachBaseline =
    [
        (18.3, 2.35),
        (36.6, 2.55),
        (54.9, 2.68),
        (73.2, 2.80),
        (91.4, 2.92),
        (114.3, 3.04),
        (137.2, 3.15),
        (160.0, 3.29),
        (182.9, 3.45),
        (205.7, 3.62),
        (228.6, 3.80),
        (251.5, 3.96),
        (274.3, 4.10)
    ];

    public static double GetExpectedPutts(double distanceMeters)
    {
        if (distanceMeters <= PgaExpectedPuttsBaseline[0].DistanceMeters)
        {
            return PgaExpectedPuttsBaseline[0].ExpectedPutts;
        }

        // MVP choice: clamp beyond the last trusted PGA baseline point.
        // Extrapolating very long putts from this sparse table would imply precision we do not have.
        if (distanceMeters >= PgaExpectedPuttsBaseline[^1].DistanceMeters)
        {
            return PgaExpectedPuttsBaseline[^1].ExpectedPutts;
        }

        for (var i = 0; i < PgaExpectedPuttsBaseline.Length - 1; i++)
        {
            var lower = PgaExpectedPuttsBaseline[i];
            var upper = PgaExpectedPuttsBaseline[i + 1];

            if (distanceMeters < lower.DistanceMeters || distanceMeters > upper.DistanceMeters)
            {
                continue;
            }

            // Linear interpolation estimates expected putts between adjacent measured points.
            var ratio = (distanceMeters - lower.DistanceMeters) / (upper.DistanceMeters - lower.DistanceMeters);
            return lower.ExpectedPutts + ratio * (upper.ExpectedPutts - lower.ExpectedPutts);
        }

        return PgaExpectedPuttsBaseline[^1].ExpectedPutts;
    }

    public static double CalculateStrokesGained(double expectedPutts, int actualPutts)
    {
        return actualPutts <= 0 ? 0 : expectedPutts - actualPutts;
    }

    public static double GetExpectedApproachShots(double distanceMeters)
    {
        if (distanceMeters <= PgaExpectedApproachBaseline[0].DistanceMeters)
        {
            return PgaExpectedApproachBaseline[0].ExpectedShots;
        }

        if (distanceMeters >= PgaExpectedApproachBaseline[^1].DistanceMeters)
        {
            return PgaExpectedApproachBaseline[^1].ExpectedShots;
        }

        for (var i = 0; i < PgaExpectedApproachBaseline.Length - 1; i++)
        {
            var lower = PgaExpectedApproachBaseline[i];
            var upper = PgaExpectedApproachBaseline[i + 1];

            if (distanceMeters < lower.DistanceMeters || distanceMeters > upper.DistanceMeters)
            {
                continue;
            }

            var ratio = (distanceMeters - lower.DistanceMeters) / (upper.DistanceMeters - lower.DistanceMeters);
            return lower.ExpectedShots + ratio * (upper.ExpectedShots - lower.ExpectedShots);
        }

        return PgaExpectedApproachBaseline[^1].ExpectedShots;
    }

    public static HolePuttingData BuildHole(
        int holeNumber,
        double distanceMeters,
        int putts,
        double approachDistanceMeters = 0,
        int approachShots = 0)
    {
        var expectedPutts = distanceMeters > 0 && putts > 0 ? GetExpectedPutts(distanceMeters) : 0;
        var expectedApproachShots = approachDistanceMeters > 0 && approachShots > 0 ? GetExpectedApproachShots(approachDistanceMeters) : 0;

        return new HolePuttingData(
            holeNumber,
            distanceMeters,
            putts,
            expectedPutts,
            CalculateStrokesGained(expectedPutts, putts),
            approachDistanceMeters,
            approachShots,
            expectedApproachShots,
            CalculateStrokesGained(expectedApproachShots, approachShots));
    }

    public static RoundSummary CalculateRoundSummary(Round round)
    {
        var completedPuttingHoles = round.Holes.Where(hole => hole.IsCompleted).ToList();
        var completedApproachHoles = round.Holes.Where(hole => hole.IsApproachCompleted).ToList();

        return new RoundSummary(
            completedPuttingHoles.Sum(hole => hole.StrokesGainedPutting),
            completedPuttingHoles.Sum(hole => hole.Putts),
            completedPuttingHoles.Count(hole => hole.Putts == 1),
            completedPuttingHoles.Count(hole => hole.Putts == 2),
            completedPuttingHoles.Count(hole => hole.Putts >= 3),
            completedPuttingHoles.Count == 0 ? 0 : completedPuttingHoles.Average(hole => hole.FirstPuttDistanceMeters),
            completedPuttingHoles.MaxBy(hole => hole.StrokesGainedPutting),
            completedPuttingHoles.MinBy(hole => hole.StrokesGainedPutting),
            completedApproachHoles.Sum(hole => hole.StrokesGainedApproach),
            completedApproachHoles.Sum(hole => hole.ApproachShots),
            completedApproachHoles.Count == 0 ? 0 : completedApproachHoles.Average(hole => hole.ApproachDistanceMeters),
            completedApproachHoles.MaxBy(hole => hole.StrokesGainedApproach),
            completedApproachHoles.MinBy(hole => hole.StrokesGainedApproach));
    }
}
