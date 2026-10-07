namespace GolfSG.Core;

/// <summary>
/// Approximate benchmark reproduced from the user's initial expected-birdies table.
/// See docs/REFERENCE_DATA.md for provenance and distance-band rules.
/// </summary>
public static class PuttingMakeProbability
{
    public static double FromMeters(double distanceMeters)
    {
        if (!double.IsFinite(distanceMeters) || distanceMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));

        var feet = DistanceConversions.MetersToFeet(distanceMeters);
        if (feet <= 1) return 1;
        if (feet <= 3) return Interpolate(feet, 1, 1, 3, 0.994);
        if (feet <= 8) return Interpolate(feet, 3, 0.994, 8, 0.529);
        if (feet <= 10) return Interpolate(feet, 8, 0.529, 10, 0.413);
        if (feet <= 15) return 0.301;
        if (feet <= 20) return 0.183;
        if (feet <= 25) return 0.1247;
        return 0.0545;
    }

    private static double Interpolate(double distance, double start, double startProbability,
        double end, double endProbability) =>
        startProbability + (distance - start) / (end - start) * (endProbability - startProbability);
}
