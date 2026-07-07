using GolfSG.Core.Models;

namespace GolfSG.Core;

public static class DistanceConversions
{
    public const double MetersPerFoot = 0.3048;
    public const double MetersPerYard = 0.9144;
    public const double FeetPerYard = 3;

    public static double FeetToMeters(double distanceFeet) => distanceFeet * MetersPerFoot;

    public static double YardsToMeters(double distanceYards) => distanceYards * MetersPerYard;

    public static double MetersToFeet(double distanceMeters) => distanceMeters / MetersPerFoot;

    public static double MetersToYards(double distanceMeters) => distanceMeters / MetersPerYard;

    public static double FeetToYards(double distanceFeet) => distanceFeet / FeetPerYard;

    public static double YardsToFeet(double distanceYards) => distanceYards * FeetPerYard;

    public static double ToMeters(double distance, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Feet => FeetToMeters(distance),
            DistanceUnit.Yards => YardsToMeters(distance),
            _ => distance
        };
    }

    public static double ToYards(double distance, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Feet => FeetToYards(distance),
            DistanceUnit.Yards => distance,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported distance unit.")
        };
    }
}
