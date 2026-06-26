using GolfSG.Core.Models;

namespace GolfSG.Core;

public interface IStrokesGainedAroundGreenService
{
    bool IsAroundGreenShot(GolfShot shot);

    double CalculateShotSgAroundGreen(GolfShot shot);

    double CalculateRoundSgAroundGreen(IEnumerable<GolfShot> shots);

    double GetAroundGreenExpectedStrokes(double distance, DistanceUnit unit, ShotLie lie);
}
