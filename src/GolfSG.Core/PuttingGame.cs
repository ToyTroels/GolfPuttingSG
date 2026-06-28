using GolfSG.Core.Models;

namespace GolfSG.Core;

public enum PuttingDistanceOrder
{
    Random,
    Ascending,
    Descending
}

public static class PuttingGame
{
    public const int TargetPutts = 30;
    public const int TotalPutts = 18;
    public const int DefaultHoleCount = 18;
    public const double DefaultMinimumDistanceMeters = 1;
    public const double DefaultMaximumDistanceMeters = 12;
    public const string LadderMode = "Ladder";
    public const string TourRoundMode = "TourRound";
    public const string ShortBenchmark = "Short";
    public const string NormalBenchmark = "Normal";
    public const string ThoroughBenchmark = "Thorough";
    public const int BenchmarkPresetVersion = 1;
    public const string ShortBenchmarkPresetId = "benchmark-short-v1";
    public const string NormalBenchmarkPresetId = "benchmark-normal-v1";
    public const string ThoroughBenchmarkPresetId = "benchmark-thorough-v1";
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

    public static string GetTitle(string mode) => GetDefinition(mode).DisplayName;

    public static string GetTitle(RoundGameInfo? gameInfo, string? legacyMode = null) =>
        gameInfo?.DisplayName ?? GetTitle(legacyMode ?? LadderMode);

    public static PuttingGameDefinition GetDefinition(string mode)
    {
        return NormalizeMode(mode) == TourRoundMode
            ? new PuttingGameDefinition(
                PuttingGameKind.TourRound,
                "Tour-runde",
                TourRoundDistancesFeet.Select(ToMeters).ToList(),
                PuttingGameScoringMode.NormalizedTargetTotal)
            : new PuttingGameDefinition(
                PuttingGameKind.Ladder,
                "Putting-spil",
                LadderDistancesFeet.Select(ToMeters).ToList(),
                PuttingGameScoringMode.NormalizedTargetTotal);
    }

    public static PuttingGameDefinition CreateCustomDefinition(IReadOnlyList<double> distancesMeters) =>
        new(PuttingGameKind.Custom, "Tilpasset putting-spil", distancesMeters, PuttingGameScoringMode.RawExpectedPutts);

    public static PuttingGameDefinition GetBenchmarkDefinition(string benchmark) => benchmark switch
    {
        ShortBenchmark => new PuttingGameDefinition(
            PuttingGameKind.Benchmark,
            "Kort benchmark",
            ShortBenchmarkDistancesMeters,
            PuttingGameScoringMode.RawExpectedPutts,
            BenchmarkLength.Short,
            ShortBenchmarkPresetId,
            BenchmarkPresetVersion),
        NormalBenchmark => new PuttingGameDefinition(
            PuttingGameKind.Benchmark,
            "Normal benchmark",
            NormalBenchmarkDistancesMeters,
            PuttingGameScoringMode.RawExpectedPutts,
            BenchmarkLength.Normal,
            NormalBenchmarkPresetId,
            BenchmarkPresetVersion),
        ThoroughBenchmark => new PuttingGameDefinition(
            PuttingGameKind.Benchmark,
            "Grundig benchmark",
            ThoroughBenchmarkDistancesMeters,
            PuttingGameScoringMode.RawExpectedPutts,
            BenchmarkLength.Thorough,
            ThoroughBenchmarkPresetId,
            BenchmarkPresetVersion),
        _ => throw new ArgumentOutOfRangeException(nameof(benchmark), "Unknown benchmark.")
    };

    public static RoundGameInfo CreateRoundGameInfo(PuttingGameDefinition definition) =>
        new(
            definition.Kind switch
            {
                PuttingGameKind.Benchmark => "PuttingBenchmark",
                PuttingGameKind.Custom => "PuttingGame",
                _ => "PuttingGame"
            },
            definition.DisplayName,
            definition.PresetId,
            definition.PresetVersion,
            definition.AttemptCount,
            definition.MinimumDistanceMeters,
            definition.MaximumDistanceMeters,
            definition.ExpectedTotal);

    public static IReadOnlyList<int> GetPresetDistances(string mode) => NormalizeMode(mode) == TourRoundMode
        ? TourRoundDistancesFeet
        : LadderDistancesFeet;

    public static double GetExpectedPutts(int distanceFeet, string mode = LadderMode)
    {
        var expected = PuttingStrokesGainedCalculator.GetExpectedPutts(ToMeters(distanceFeet));
        return expected * TargetPutts / GetExpectedTotal(mode);
    }

    public static double GetExpectedPutts(double distanceMeters) =>
        PuttingStrokesGainedCalculator.GetExpectedPutts(distanceMeters);

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

    public static HolePuttingData BuildPutt(int puttNumber, double distanceMeters, int putts)
    {
        var expectedPutts = putts > 0 ? GetExpectedPutts(distanceMeters) : 0;

        return new HolePuttingData(
            puttNumber,
            distanceMeters,
            putts,
            expectedPutts,
            StrokesGainedMath.Calculate(expectedPutts, putts));
    }

    public static IReadOnlyList<double> GetBenchmarkDistancesMeters(string benchmark) => benchmark switch
    {
        ShortBenchmark or NormalBenchmark or ThoroughBenchmark => GetBenchmarkDefinition(benchmark).DistancesMeters,
        _ => throw new ArgumentOutOfRangeException(nameof(benchmark), "Unknown benchmark.")
    };

    public static IReadOnlyList<double> OrderDistances(
        IReadOnlyList<double> distancesMeters,
        PuttingDistanceOrder order,
        Random? random = null)
    {
        return order switch
        {
            PuttingDistanceOrder.Ascending => distancesMeters.Order().ToList(),
            PuttingDistanceOrder.Descending => distancesMeters.OrderDescending().ToList(),
            PuttingDistanceOrder.Random => ShuffleDistances(distancesMeters, random ?? Random.Shared),
            _ => throw new ArgumentOutOfRangeException(nameof(order), "Unknown distance order.")
        };
    }

    public static IReadOnlyList<double> BuildBellCurveDistancesMeters(
        int holeCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters)
    {
        if (holeCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(holeCount), "Hole count must be at least 1.");
        }

        if (minimumDistanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDistanceMeters), "Minimum distance must be greater than 0.");
        }

        if (maximumDistanceMeters < minimumDistanceMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistanceMeters), "Maximum distance must be greater than or equal to the minimum distance.");
        }

        if (holeCount == 1 || Math.Abs(maximumDistanceMeters - minimumDistanceMeters) < 0.001)
        {
            return [RoundToNearestTenth((minimumDistanceMeters + maximumDistanceMeters) / 2)];
        }

        var binCount = Math.Min(Math.Max(holeCount, 5), 21);
        var weights = Enumerable.Range(0, binCount)
            .Select(index =>
            {
                var centerOffset = (index - (binCount - 1) / 2d) / ((binCount - 1) / 2d);
                return Math.Exp(-0.5 * Math.Pow(centerOffset / 0.45, 2));
            })
            .ToList();

        var totalWeight = weights.Sum();
        var allocations = weights
            .Select((weight, index) =>
            {
                var exact = weight / totalWeight * holeCount;
                return new DistanceAllocation(index, (int)Math.Floor(exact), exact - Math.Floor(exact));
            })
            .ToList();

        var allocated = allocations.Sum(allocation => allocation.Count);
        foreach (var allocation in allocations
            .OrderByDescending(allocation => allocation.Remainder)
            .ThenBy(allocation => Math.Abs(allocation.Index - (binCount - 1) / 2d))
            .Take(holeCount - allocated))
        {
            allocation.Count++;
        }

        var distances = allocations
            .SelectMany(allocation => Enumerable.Repeat(
                RoundToNearestTenth(minimumDistanceMeters + (maximumDistanceMeters - minimumDistanceMeters) * allocation.Index / (binCount - 1)),
                allocation.Count))
            .OrderBy(distance => Math.Abs(distance - (minimumDistanceMeters + maximumDistanceMeters) / 2))
            .ThenBy(distance => distance)
            .ToList();

        return distances;
    }

    public static string NormalizeMode(string? mode) => mode == TourRoundMode ? TourRoundMode : LadderMode;

    private static double GetExpectedTotal(string mode) => GetPresetDistances(mode)
        .Sum(distance => PuttingStrokesGainedCalculator.GetExpectedPutts(ToMeters(distance)));

    private static double ToMeters(int distanceFeet) => distanceFeet * FeetToMeters;

    private static double RoundToNearestTenth(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private static IReadOnlyList<double> ShuffleDistances(IReadOnlyList<double> distancesMeters, Random random)
    {
        var distances = distancesMeters.ToList();
        for (var index = distances.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (distances[index], distances[swapIndex]) = (distances[swapIndex], distances[index]);
        }

        return distances;
    }

    private sealed class DistanceAllocation(int index, int count, double remainder)
    {
        public int Index { get; } = index;
        public int Count { get; set; } = count;
        public double Remainder { get; } = remainder;
    }
}
