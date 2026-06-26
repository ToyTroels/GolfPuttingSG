using GolfSG.Core.Models;

namespace GolfSG.Core;

public interface IStrokesGainedApproachService
{
    bool IsApproachShot(GolfShot shot);

    double CalculateShotSgApproach(GolfShot shot);

    double CalculateRoundSgApproach(IEnumerable<GolfShot> shots);

    double GetApproachExpectedStrokes(double distance, DistanceUnit unit, ShotLie lie);
}
