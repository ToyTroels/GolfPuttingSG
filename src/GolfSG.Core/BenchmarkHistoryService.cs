using GolfSG.Core.Models;

namespace GolfSG.Core;

public static class BenchmarkHistoryService
{
    public static IReadOnlyList<BenchmarkHistorySummary> Summarize(IEnumerable<Round> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        return rounds
            .Where(IsBenchmarkRound)
            .GroupBy(round => round.GameInfo!.PresetId, StringComparer.Ordinal)
            .Select(group => BuildSummary(group.ToList()))
            .OrderBy(summary => summary.BenchmarkType)
            .ThenBy(summary => summary.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    private static BenchmarkHistorySummary BuildSummary(IReadOnlyList<Round> rounds)
    {
        var orderedAttempts = rounds
            .OrderByDescending(round => round.Date)
            .ThenByDescending(round => round.Id, StringComparer.Ordinal)
            .Select(round => new BenchmarkAttemptSummary(
                round.Id,
                round.Date,
                CalculateScore(round),
                round.Holes.Sum(hole => hole.Putts)))
            .ToList();

        var latest = orderedAttempts[0];
        var best = orderedAttempts.MaxBy(attempt => attempt.Score)!;
        var first = orderedAttempts[^1];
        var gameInfo = rounds[0].GameInfo!;

        return new BenchmarkHistorySummary(
            gameInfo.PresetId!,
            gameInfo.DisplayName,
            gameInfo.BenchmarkType,
            gameInfo.PresetVersion,
            gameInfo.AttemptCount,
            gameInfo.ExpectedTotal,
            gameInfo.StartDistanceMeters,
            gameInfo.EndDistanceMeters,
            gameInfo.DistanceStepMeters,
            gameInfo.PuttsPerDistance,
            gameInfo.DistanceStepDescription,
            orderedAttempts.Count,
            latest,
            best,
            orderedAttempts.Take(3).Average(attempt => attempt.Score),
            latest.Score - first.Score,
            orderedAttempts);
    }

    private static bool IsBenchmarkRound(Round round)
    {
        var gameInfo = round.GameInfo;
        return gameInfo is not null &&
            string.Equals(gameInfo.Type, "PuttingBenchmark", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(gameInfo.PresetId) &&
            round.Holes.Count > 0;
    }

    private static double CalculateScore(Round round) =>
        round.Holes.Sum(hole => hole.StrokesGainedPutting);
}

public sealed record BenchmarkHistorySummary(
    string PresetId,
    string DisplayName,
    PuttingBenchmarkType? BenchmarkType,
    int? PresetVersion,
    int AttemptCount,
    double ExpectedTotal,
    double? StartDistanceMeters,
    double? EndDistanceMeters,
    double? DistanceStepMeters,
    int? PuttsPerDistance,
    string? DistanceStepDescription,
    int Attempts,
    BenchmarkAttemptSummary LatestAttempt,
    BenchmarkAttemptSummary BestAttempt,
    double AverageLastThreeScore,
    double ImprovementFromFirstToLatest,
    IReadOnlyList<BenchmarkAttemptSummary> RecentAttempts);

public sealed record BenchmarkAttemptSummary(
    string RoundId,
    DateTime Date,
    double Score,
    int TotalPutts);
