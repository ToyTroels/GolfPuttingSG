namespace GolfSG.Core;

internal static class PuttingGameScoring
{
    public static double ToMeters(int distanceFeet) => DistanceConversions.FeetToMeters(distanceFeet);

    public static double GetExpectedPutts(int distanceFeet, string mode)
    {
        var expected = PuttingStrokesGainedCalculator.GetExpectedPutts(ToMeters(distanceFeet));
        return expected * PuttingGame.TargetPutts / GetExpectedTotal(mode);
    }

    public static double GetExpectedPutts(double distanceMeters) =>
        PuttingStrokesGainedCalculator.GetExpectedPutts(distanceMeters);

    private static double GetExpectedTotal(string mode) => PuttingGamePresets.GetPresetDistances(mode)
        .Sum(distance => PuttingStrokesGainedCalculator.GetExpectedPutts(ToMeters(distance)));
}
