namespace GolfSG.Core;

internal static class StrokesGainedMath
{
    public static double Calculate(double expectedShots, int actualShots)
    {
        return actualShots <= 0 ? 0 : expectedShots - actualShots;
    }

    public static double InterpolateExpectedShots(
        double distanceMeters,
        IReadOnlyList<StrokesGainedReferencePoint> baseline)
    {
        if (distanceMeters <= baseline[0].DistanceMeters)
        {
            return baseline[0].ExpectedShots;
        }

        if (distanceMeters >= baseline[^1].DistanceMeters)
        {
            return baseline[^1].ExpectedShots;
        }

        for (var i = 0; i < baseline.Count - 1; i++)
        {
            var lower = baseline[i];
            var upper = baseline[i + 1];

            if (distanceMeters < lower.DistanceMeters || distanceMeters > upper.DistanceMeters)
            {
                continue;
            }

            var ratio = (distanceMeters - lower.DistanceMeters) / (upper.DistanceMeters - lower.DistanceMeters);
            return lower.ExpectedShots + ratio * (upper.ExpectedShots - lower.ExpectedShots);
        }

        return baseline[^1].ExpectedShots;
    }
}

public sealed record StrokesGainedReferencePoint(double DistanceMeters, double ExpectedShots);
