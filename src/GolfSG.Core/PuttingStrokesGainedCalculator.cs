using GolfSG.Core.Models;

namespace GolfSG.Core;

public static class PuttingStrokesGainedCalculator
{
    private const double MetersPerFoot = 0.3048;
    private const double ThreeFeetMeters = 3 * MetersPerFoot;
    private const double TenFeetMeters = 10 * MetersPerFoot;
    private const double FifteenFeetMeters = 15 * MetersPerFoot;
    private const double TwentyFeetMeters = 20 * MetersPerFoot;
    private const double TwentyFiveFeetMeters = 25 * MetersPerFoot;

    public const double ShortPuttMaximumMeters = 5 * MetersPerFoot;

    private static readonly StrokesGainedReferencePoint[] PgaExpectedPuttsBaseline =
    [
        new(0.3, 1.00),
        new(0.6, 1.01),
        new(0.9, 1.05),
        new(1.2, 1.10),
        new(1.5, 1.24),
        new(1.8, 1.34),
        new(2.4, 1.53),
        new(3.0, 1.61),
        new(4.6, 1.78),
        new(6.1, 1.87),
        new(9.1, 1.98),
        new(12.2, 2.06),
        new(15.2, 2.14),
        new(18.3, 2.21),
        new(27.4, 2.36)
    ];

    public static IReadOnlyList<StrokesGainedReferencePoint> Reference => PgaExpectedPuttsBaseline;

    public static double GetExpectedPutts(double distanceMeters)
    {
        return StrokesGainedMath.InterpolateExpectedShots(distanceMeters, PgaExpectedPuttsBaseline);
    }

    public static HolePuttingData BuildHole(int holeNumber, double distanceMeters, int putts)
    {
        var expectedPutts = distanceMeters > 0 && putts > 0 ? GetExpectedPutts(distanceMeters) : 0;

        return new HolePuttingData(
            holeNumber,
            distanceMeters,
            putts,
            expectedPutts,
            StrokesGainedMath.Calculate(expectedPutts, putts));
    }

    public static PuttingRoundSummary CalculateSummary(IReadOnlyList<HolePuttingData> holes)
    {
        var completedHoles = holes.Where(hole => hole.IsCompleted).ToList();

        return new PuttingRoundSummary(
            completedHoles.Sum(hole => hole.StrokesGainedPutting),
            completedHoles.Sum(hole => hole.Putts),
            completedHoles.Count(hole => hole.Putts == 1),
            completedHoles.Count(hole => hole.Putts == 2),
            completedHoles.Count(hole => hole.Putts >= 3),
            completedHoles.Count == 0 ? 0 : completedHoles.Average(hole => hole.FirstPuttDistanceMeters),
            completedHoles.MaxBy(hole => hole.StrokesGainedPutting),
            completedHoles.MinBy(hole => hole.StrokesGainedPutting),
            BuildDistanceBuckets(completedHoles));
    }

    private static IReadOnlyList<PuttingDistanceBucketSummary> BuildDistanceBuckets(IReadOnlyList<HolePuttingData> holes)
    {
        return
        [
            BuildBucket("Inside 3 ft", holes, null, ThreeFeetMeters),
            BuildBucket("3-5 ft", holes, ThreeFeetMeters, ShortPuttMaximumMeters),
            BuildBucket("5-10 ft", holes, ShortPuttMaximumMeters, TenFeetMeters),
            BuildBucket("10-15 ft", holes, TenFeetMeters, FifteenFeetMeters),
            BuildBucket("15-20 ft", holes, FifteenFeetMeters, TwentyFeetMeters),
            BuildBucket("20-25 ft", holes, TwentyFeetMeters, TwentyFiveFeetMeters, includeMaximumDistance: true),
            BuildBucket("> 25 ft", holes, TwentyFiveFeetMeters, null)
        ];
    }

    private static PuttingDistanceBucketSummary BuildBucket(
        string name,
        IReadOnlyList<HolePuttingData> holes,
        double? minimumDistanceMeters,
        double? maximumDistanceMeters,
        bool includeMaximumDistance = false)
    {
        var bucketHoles = holes
            .Where(hole => IsInDistanceBucket(
                hole.FirstPuttDistanceMeters,
                minimumDistanceMeters,
                maximumDistanceMeters,
                includeMaximumDistance))
            .ToList();

        return new PuttingDistanceBucketSummary(
            name,
            minimumDistanceMeters,
            maximumDistanceMeters,
            bucketHoles.Count,
            bucketHoles.Sum(hole => hole.Putts),
            bucketHoles.Sum(hole => hole.StrokesGainedPutting));
    }

    private static bool IsInDistanceBucket(
        double distanceMeters,
        double? minimumDistanceMeters,
        double? maximumDistanceMeters,
        bool includeMaximumDistance)
    {
        if (minimumDistanceMeters is null && maximumDistanceMeters is not null)
        {
            return distanceMeters < maximumDistanceMeters;
        }

        if (minimumDistanceMeters is not null && maximumDistanceMeters is not null)
        {
            return distanceMeters >= minimumDistanceMeters &&
                (includeMaximumDistance
                    ? distanceMeters <= maximumDistanceMeters
                    : distanceMeters < maximumDistanceMeters);
        }

        if (minimumDistanceMeters is not null)
        {
            return distanceMeters > minimumDistanceMeters;
        }

        return false;
    }
}
