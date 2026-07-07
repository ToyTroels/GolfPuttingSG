using GolfSG.Core.Models;

namespace GolfSG.Core;

public sealed class StrokesGainedPuttingService : IStrokesGainedPuttingService
{
    /// <summary>
    /// Delegates putting expected strokes to the app's existing putting benchmark calculator.
    /// </summary>
    public double GetExpectedPutts(double distance, DistanceUnit unit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        var distanceMeters = unit switch
        {
            DistanceUnit.Feet => DistanceConversions.FeetToMeters(distance),
            DistanceUnit.Yards => DistanceConversions.YardsToMeters(distance),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported distance unit.")
        };

        return PuttingStrokesGainedCalculator.GetExpectedPutts(distanceMeters);
    }
}
