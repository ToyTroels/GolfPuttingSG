namespace GolfSG.Core.Models;

public enum PuttingGameKind
{
    Ladder,
    TourRound,
    Custom,
    Benchmark
}

public enum PuttingGameScoringMode
{
    NormalizedTargetTotal,
    RawExpectedPutts
}

public enum BenchmarkLength
{
    Short,
    Normal,
    Thorough
}

public sealed record PuttingGameDefinition(
    PuttingGameKind Kind,
    string DisplayName,
    IReadOnlyList<double> DistancesMeters,
    PuttingGameScoringMode ScoringMode,
    BenchmarkLength? BenchmarkLength = null,
    string? PresetId = null,
    int? PresetVersion = null)
{
    public int AttemptCount => DistancesMeters.Count;

    public double? MinimumDistanceMeters => DistancesMeters.Count == 0 ? null : DistancesMeters.Min();

    public double? MaximumDistanceMeters => DistancesMeters.Count == 0 ? null : DistancesMeters.Max();

    public double ExpectedTotal => ScoringMode == PuttingGameScoringMode.NormalizedTargetTotal
        ? PuttingGame.TargetPutts
        : DistancesMeters.Sum(PuttingGame.GetExpectedPutts);
}

public sealed record RoundGameInfo(
    string Type,
    string DisplayName,
    string? PresetId,
    int? PresetVersion,
    int AttemptCount,
    double? MinimumDistanceMeters,
    double? MaximumDistanceMeters,
    double ExpectedTotal);
