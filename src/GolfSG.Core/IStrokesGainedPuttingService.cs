using GolfSG.Core.Models;

namespace GolfSG.Core;

public interface IStrokesGainedPuttingService
{
    double GetExpectedPutts(double distance, DistanceUnit unit);
}
