using GolfSG.Core.Models;

namespace GolfSG.Core;

public sealed class StrokesGainedPuttingService : IStrokesGainedPuttingService
{
    private const double MetersPerFoot = 0.3048;
    private const double FeetPerYard = 3;

    /// <summary>
    /// Delegates putting expected strokes to the app's existing putting benchmark calculator.
    /// </summary>
    public double GetExpectedPutts(double distance, DistanceUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        var distanceMeters = unit switch
        {
            DistanceUnit.Feet => distance * MetersPerFoot,
            DistanceUnit.Yards => distance * FeetPerYard * MetersPerFoot,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported distance unit.")
        };

        return PuttingStrokesGainedCalculator.GetExpectedPutts(distanceMeters);
    }
}
