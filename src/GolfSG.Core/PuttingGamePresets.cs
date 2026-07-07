using GolfSG.Core.Models;

namespace GolfSG.Core;

internal static class PuttingGamePresets
{
    public static IReadOnlyList<int> LadderDistancesFeet { get; } =
    [
        3, 4, 5, 6, 7, 8, 10, 13, 18, 19, 25, 31, 32, 36, 38, 41, 42, 45
    ];

    public static IReadOnlyList<int> TourRoundDistancesFeet { get; } =
    [
        18, 4, 31, 7, 13, 25, 5, 38, 10, 19, 3, 32, 8, 42, 6, 36, 4, 25
    ];

    public static IReadOnlyList<double> ShortBenchmarkDistancesMeters { get; } =
    [
        6.5, 6.5, 6.5, 6.5, 6.0, 6.0, 6.0, 6.0, 7.1, 7.1,
        7.1, 7.1, 5.4, 5.4, 5.4, 7.6, 7.6, 7.6, 4.9, 4.9,
        4.9, 8.2, 8.2, 8.2, 8.7, 8.7, 4.3, 4.3, 3.8, 3.8,
        9.3, 9.3, 3.2, 9.8, 2.7, 10.4, 2.1, 10.9, 1.6, 11.5
    ];

    public static IReadOnlyList<double> NormalBenchmarkDistancesMeters { get; } =
    [
        6.5, 6.5, 6.5, 6.5, 6.5, 6.5, 6.5, 6.0, 6.0, 6.0,
        6.0, 6.0, 6.0, 6.0, 7.1, 7.1, 7.1, 7.1, 7.1, 7.1,
        7.1, 5.4, 5.4, 5.4, 5.4, 5.4, 5.4, 7.6, 7.6, 7.6,
        7.6, 7.6, 7.6, 4.9, 4.9, 4.9, 4.9, 4.9, 8.2, 8.2,
        8.2, 8.2, 8.2, 8.7, 8.7, 8.7, 8.7, 4.3, 4.3, 4.3,
        4.3, 3.8, 3.8, 3.8, 3.8, 9.3, 9.3, 9.3, 9.3, 3.2,
        3.2, 3.2, 9.8, 9.8, 9.8, 2.7, 2.7, 10.4, 10.4, 2.1,
        10.9, 1.6, 11.5, 1.0, 12.0
    ];

    public static IReadOnlyList<double> ThoroughBenchmarkDistancesMeters { get; } =
    [
        6.5, 6.5, 6.5, 6.5, 6.5, 6.5, 6.5, 6.5, 6.5, 6.0,
        6.0, 6.0, 6.0, 6.0, 6.0, 6.0, 6.0, 6.0, 7.1, 7.1,
        7.1, 7.1, 7.1, 7.1, 7.1, 7.1, 7.1, 5.4, 5.4, 5.4,
        5.4, 5.4, 5.4, 5.4, 5.4, 7.6, 7.6, 7.6, 7.6, 7.6,
        7.6, 7.6, 7.6, 4.9, 4.9, 4.9, 4.9, 4.9, 4.9, 4.9,
        8.2, 8.2, 8.2, 8.2, 8.2, 8.2, 8.2, 8.7, 8.7, 8.7,
        8.7, 8.7, 8.7, 4.3, 4.3, 4.3, 4.3, 4.3, 4.3, 3.8,
        3.8, 3.8, 3.8, 3.8, 9.3, 9.3, 9.3, 9.3, 9.3, 3.2,
        3.2, 3.2, 3.2, 9.8, 9.8, 9.8, 9.8, 2.7, 2.7, 2.7,
        10.4, 10.4, 2.1, 2.1, 10.9, 10.9, 1.6, 11.5, 1.0, 12.0
    ];

    public static string NormalizeMode(string? mode) => mode == PuttingGame.TourRoundMode
        ? PuttingGame.TourRoundMode
        : PuttingGame.LadderMode;

    public static IReadOnlyList<int> GetPresetDistances(string mode) => NormalizeMode(mode) == PuttingGame.TourRoundMode
        ? TourRoundDistancesFeet
        : LadderDistancesFeet;

    public static PuttingGameDefinition GetDefinition(string mode)
    {
        return NormalizeMode(mode) == PuttingGame.TourRoundMode
            ? new PuttingGameDefinition(
                PuttingGameKind.TourRound,
                "Tour-runde",
                TourRoundDistancesFeet.Select(PuttingGameScoring.ToMeters).ToList(),
                PuttingGameScoringMode.NormalizedTargetTotal)
            : new PuttingGameDefinition(
                PuttingGameKind.Ladder,
                "Putting-spil",
                LadderDistancesFeet.Select(PuttingGameScoring.ToMeters).ToList(),
                PuttingGameScoringMode.NormalizedTargetTotal);
    }

    public static PuttingGameDefinition CreateCustomDefinition(IReadOnlyList<double> distancesMeters) =>
        new(PuttingGameKind.Custom, "Tilpasset putting-spil", distancesMeters, PuttingGameScoringMode.RawExpectedPutts);

    public static PuttingGameDefinition GetBenchmarkDefinition(string benchmark) => benchmark switch
    {
        PuttingGame.ShortBenchmark => new PuttingGameDefinition(
            PuttingGameKind.Benchmark,
            "Kort benchmark",
            ShortBenchmarkDistancesMeters,
            PuttingGameScoringMode.RawExpectedPutts,
            BenchmarkLength.Short,
            PuttingGame.ShortBenchmarkPresetId,
            PuttingGame.BenchmarkPresetVersion,
            PuttingBenchmarkType.BellCurve),
        PuttingGame.NormalBenchmark => new PuttingGameDefinition(
            PuttingGameKind.Benchmark,
            "Normal benchmark",
            NormalBenchmarkDistancesMeters,
            PuttingGameScoringMode.RawExpectedPutts,
            BenchmarkLength.Normal,
            PuttingGame.NormalBenchmarkPresetId,
            PuttingGame.BenchmarkPresetVersion,
            PuttingBenchmarkType.BellCurve),
        PuttingGame.ThoroughBenchmark => new PuttingGameDefinition(
            PuttingGameKind.Benchmark,
            "Grundig benchmark",
            ThoroughBenchmarkDistancesMeters,
            PuttingGameScoringMode.RawExpectedPutts,
            BenchmarkLength.Thorough,
            PuttingGame.ThoroughBenchmarkPresetId,
            PuttingGame.BenchmarkPresetVersion,
            PuttingBenchmarkType.BellCurve),
        PuttingGame.ShortLadderBenchmark => CreateLadderBenchmarkDefinition(
            "Kort ladder benchmark",
            BenchmarkLength.Short,
            PuttingGame.ShortLadderBenchmarkPresetId,
            startDistanceMeters: 1,
            endDistanceMeters: 3,
            distanceStepMeters: 1,
            puttsPerDistance: 5),
        PuttingGame.NormalLadderBenchmark => CreateLadderBenchmarkDefinition(
            "Normal ladder benchmark",
            BenchmarkLength.Normal,
            PuttingGame.NormalLadderBenchmarkPresetId,
            startDistanceMeters: 1,
            endDistanceMeters: 6,
            distanceStepMeters: 1,
            puttsPerDistance: 5),
        PuttingGame.ThoroughLadderBenchmark => CreateLadderBenchmarkDefinition(
            "Grundig ladder benchmark",
            BenchmarkLength.Thorough,
            PuttingGame.ThoroughLadderBenchmarkPresetId,
            presetVersion: PuttingGame.ThoroughLadderBenchmarkPresetVersion,
            distanceStopsMeters: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12.5, 15],
            puttsPerDistance: 5,
            distanceStepDescription: "1 m trin til 10 m, derefter 2,5 m"),
        _ => throw new ArgumentOutOfRangeException(nameof(benchmark), "Unknown benchmark.")
    };

    public static IReadOnlyList<double> GetBenchmarkDistancesMeters(string benchmark) => benchmark switch
    {
        PuttingGame.ShortBenchmark or PuttingGame.NormalBenchmark or PuttingGame.ThoroughBenchmark or
            PuttingGame.ShortLadderBenchmark or PuttingGame.NormalLadderBenchmark or PuttingGame.ThoroughLadderBenchmark =>
            GetBenchmarkDefinition(benchmark).DistancesMeters,
        _ => throw new ArgumentOutOfRangeException(nameof(benchmark), "Unknown benchmark.")
    };

    private static PuttingGameDefinition CreateLadderBenchmarkDefinition(
        string displayName,
        BenchmarkLength benchmarkLength,
        string presetId,
        double startDistanceMeters,
        double endDistanceMeters,
        double distanceStepMeters,
        int puttsPerDistance) =>
        new(
            PuttingGameKind.Benchmark,
            displayName,
            PuttingDistanceGenerator.BuildLadderDistancesMeters(startDistanceMeters, endDistanceMeters, distanceStepMeters, puttsPerDistance),
            PuttingGameScoringMode.RawExpectedPutts,
            benchmarkLength,
            presetId,
            PuttingGame.BenchmarkPresetVersion,
            PuttingBenchmarkType.Ladder,
            startDistanceMeters,
            endDistanceMeters,
            distanceStepMeters,
            puttsPerDistance,
            $"{distanceStepMeters:0.#} m steps");

    private static PuttingGameDefinition CreateLadderBenchmarkDefinition(
        string displayName,
        BenchmarkLength benchmarkLength,
        string presetId,
        int presetVersion,
        IReadOnlyList<double> distanceStopsMeters,
        int puttsPerDistance,
        string distanceStepDescription)
    {
        var distancesMeters = PuttingDistanceGenerator.BuildLadderDistancesMeters(distanceStopsMeters, puttsPerDistance);
        return new PuttingGameDefinition(
            PuttingGameKind.Benchmark,
            displayName,
            distancesMeters,
            PuttingGameScoringMode.RawExpectedPutts,
            benchmarkLength,
            presetId,
            presetVersion,
            PuttingBenchmarkType.Ladder,
            distanceStopsMeters.Min(),
            distanceStopsMeters.Max(),
            null,
            puttsPerDistance,
            distanceStepDescription);
    }
}
