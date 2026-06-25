using GolfSG.Core.Models;

namespace GolfSG.Core;

public static class PuttingGame
{
    public const int TargetPutts = 30;
    public const int TotalPutts = 18;
    public const string LadderMode = "Ladder";
    public const string TourRoundMode = "TourRound";
    private const double FeetToMeters = 0.3048;

    public static IReadOnlyList<int> PresetDistancesFeet => GetPresetDistances(LadderMode);

    public static IReadOnlyList<int> LadderDistancesFeet { get; } =
    [
        3, 4, 5, 6, 7, 8, 10, 13, 18, 19, 25, 31, 32, 36, 38, 41, 42, 45
    ];

    public static IReadOnlyList<int> TourRoundDistancesFeet { get; } =
    [
        18, 4, 31, 7, 13, 25, 5, 38, 10, 19, 3, 32, 8, 42, 6, 36, 4, 25
    ];

    public static string GetTitle(string mode) => NormalizeMode(mode) == TourRoundMode
        ? "Tour Round Game"
        : "Putting Game";

    public static IReadOnlyList<int> GetPresetDistances(string mode) => NormalizeMode(mode) == TourRoundMode
        ? TourRoundDistancesFeet
        : LadderDistancesFeet;

    public static double GetExpectedPutts(int distanceFeet, string mode = LadderMode)
    {
        var expected = PuttingStrokesGainedCalculator.GetExpectedPutts(ToMeters(distanceFeet));
        return expected * TargetPutts / GetExpectedTotal(mode);
    }

    public static HolePuttingData BuildPutt(int puttNumber, int distanceFeet, int putts, string mode = LadderMode)
    {
        var distanceMeters = ToMeters(distanceFeet);
        var expectedPutts = putts > 0 ? GetExpectedPutts(distanceFeet, mode) : 0;

        return new HolePuttingData(
            puttNumber,
            distanceMeters,
            putts,
            expectedPutts,
            StrokesGainedMath.Calculate(expectedPutts, putts));
    }

    public static string NormalizeMode(string? mode) => mode == TourRoundMode ? TourRoundMode : LadderMode;

    private static double GetExpectedTotal(string mode) => GetPresetDistances(mode)
        .Sum(distance => PuttingStrokesGainedCalculator.GetExpectedPutts(ToMeters(distance)));

    private static double ToMeters(int distanceFeet) => distanceFeet * FeetToMeters;
}
