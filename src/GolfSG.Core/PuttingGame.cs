using GolfSG.Core.Models;

namespace GolfSG.Core;

public enum PuttingDistanceOrder
{
    Random,
    Ascending,
    Descending
}

public enum PuttingTrainingDistanceDistribution
{
    BellCurve,
    Short,
    Long,
    Random
}

public static class PuttingGame
{
    public const int TargetPutts = 30;
    public const int TotalPutts = 18;
    public const int DefaultHoleCount = 20;
    public const double DefaultMinimumDistanceMeters = 1;
    public const double DefaultMaximumDistanceMeters = 12;
    public const string LadderMode = "Ladder";
    public const string TourRoundMode = "TourRound";
    public const string ShortBenchmark = "Short";
    public const string NormalBenchmark = "Normal";
    public const string ThoroughBenchmark = "Thorough";
    public const string ShortLadderBenchmark = "LadderShort";
    public const string NormalLadderBenchmark = "LadderNormal";
    public const string ThoroughLadderBenchmark = "LadderThorough";
    public const int BenchmarkPresetVersion = 1;
    public const string ShortBenchmarkPresetId = "benchmark-short-v1";
    public const string NormalBenchmarkPresetId = "benchmark-normal-v1";
    public const string ThoroughBenchmarkPresetId = "benchmark-thorough-v1";
    public const string ShortLadderBenchmarkPresetId = "benchmark-ladder-short-v1";
    public const string NormalLadderBenchmarkPresetId = "benchmark-ladder-normal-v1";
    public const int ThoroughLadderBenchmarkPresetVersion = 2;
    public const string ThoroughLadderBenchmarkPresetId = "benchmark-ladder-thorough-v2";

    public static IReadOnlyList<int> PresetDistancesFeet => GetPresetDistances(LadderMode);

    public static IReadOnlyList<int> LadderDistancesFeet => PuttingGamePresets.LadderDistancesFeet;

    public static IReadOnlyList<int> TourRoundDistancesFeet => PuttingGamePresets.TourRoundDistancesFeet;

    public static IReadOnlyList<double> ShortBenchmarkDistancesMeters => PuttingGamePresets.ShortBenchmarkDistancesMeters;

    public static IReadOnlyList<double> NormalBenchmarkDistancesMeters => PuttingGamePresets.NormalBenchmarkDistancesMeters;

    public static IReadOnlyList<double> ThoroughBenchmarkDistancesMeters => PuttingGamePresets.ThoroughBenchmarkDistancesMeters;

    public static string GetTitle(string mode) => GetDefinition(mode).DisplayName;

    public static string GetTitle(RoundGameInfo? gameInfo, string? legacyMode = null) =>
        gameInfo?.DisplayName ?? GetTitle(legacyMode ?? LadderMode);

    public static PuttingGameDefinition GetDefinition(string mode) =>
        PuttingGamePresets.GetDefinition(mode);

    public static PuttingGameDefinition CreateCustomDefinition(IReadOnlyList<double> distancesMeters) =>
        PuttingGamePresets.CreateCustomDefinition(distancesMeters);

    public static PuttingGameDefinition GetBenchmarkDefinition(string benchmark) =>
        PuttingGamePresets.GetBenchmarkDefinition(benchmark);

    public static RoundGameInfo CreateRoundGameInfo(PuttingGameDefinition definition) =>
        PuttingGameFactory.CreateRoundGameInfo(definition);

    public static IReadOnlyList<int> GetPresetDistances(string mode) =>
        PuttingGamePresets.GetPresetDistances(mode);

    public static double GetExpectedPutts(int distanceFeet, string mode = LadderMode) =>
        PuttingGameScoring.GetExpectedPutts(distanceFeet, mode);

    public static double GetExpectedPutts(double distanceMeters) =>
        PuttingGameScoring.GetExpectedPutts(distanceMeters);

    public static HolePuttingData BuildPutt(int puttNumber, int distanceFeet, int putts, string mode = LadderMode) =>
        PuttingGameFactory.BuildPutt(puttNumber, distanceFeet, putts, mode);

    public static HolePuttingData BuildPutt(int puttNumber, double distanceMeters, int putts) =>
        PuttingGameFactory.BuildPutt(puttNumber, distanceMeters, putts);

    public static IReadOnlyList<double> GetBenchmarkDistancesMeters(string benchmark) =>
        PuttingGamePresets.GetBenchmarkDistancesMeters(benchmark);

    public static IReadOnlyList<double> OrderDistances(
        IReadOnlyList<double> distancesMeters,
        PuttingDistanceOrder order,
        Random? random = null) =>
        PuttingDistanceGenerator.OrderDistances(distancesMeters, order, random);

    public static IReadOnlyList<double> BuildBellCurveDistancesMeters(
        int holeCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters) =>
        PuttingDistanceGenerator.BuildBellCurveDistancesMeters(holeCount, minimumDistanceMeters, maximumDistanceMeters);

    public static IReadOnlyList<double> BuildTrainingDistancesMeters(
        int puttCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters,
        PuttingTrainingDistanceDistribution distribution,
        Random? random = null) =>
        PuttingDistanceGenerator.BuildTrainingDistancesMeters(
            puttCount,
            minimumDistanceMeters,
            maximumDistanceMeters,
            distribution,
            random);

    public static IReadOnlyList<double> BuildLadderDistancesMeters(
        double startDistanceMeters,
        double endDistanceMeters,
        double distanceStepMeters,
        int puttsPerDistance) =>
        PuttingDistanceGenerator.BuildLadderDistancesMeters(
            startDistanceMeters,
            endDistanceMeters,
            distanceStepMeters,
            puttsPerDistance);

    public static IReadOnlyList<double> BuildLadderDistancesMeters(
        IReadOnlyList<double> distanceStopsMeters,
        int puttsPerDistance) =>
        PuttingDistanceGenerator.BuildLadderDistancesMeters(distanceStopsMeters, puttsPerDistance);

    public static string NormalizeMode(string? mode) =>
        PuttingGamePresets.NormalizeMode(mode);
}
