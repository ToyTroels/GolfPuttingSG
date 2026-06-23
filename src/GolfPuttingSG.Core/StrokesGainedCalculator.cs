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

    public static HolePuttingData BuildHole(int holeNumber, double distanceMeters, int putts)
    {
        var expectedPutts = distanceMeters > 0 && putts > 0 ? GetExpectedPutts(distanceMeters) : 0;
        return new HolePuttingData(
            holeNumber,
            distanceMeters,
            putts,
            expectedPutts,
            CalculateStrokesGained(expectedPutts, putts));
    }

    public static RoundSummary CalculateRoundSummary(Round round)
    {
        var completedHoles = round.Holes.Where(hole => hole.IsCompleted).ToList();
        return new RoundSummary(
            completedHoles.Sum(hole => hole.StrokesGainedPutting),
            completedHoles.Sum(hole => hole.Putts),
            completedHoles.Count(hole => hole.Putts == 1),
            completedHoles.Count(hole => hole.Putts == 2),
            completedHoles.Count(hole => hole.Putts >= 3),
            completedHoles.Count == 0 ? 0 : completedHoles.Average(hole => hole.FirstPuttDistanceMeters),
            completedHoles.MaxBy(hole => hole.StrokesGainedPutting),
            completedHoles.MinBy(hole => hole.StrokesGainedPutting));
    }
}
