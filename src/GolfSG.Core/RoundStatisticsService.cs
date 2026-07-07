using GolfSG.Core.Models;

namespace GolfSG.Core;

public enum StrokesGainedCategory
{
    Total,
    Putting,
    Approach,
    AroundGreen
}

public static class RoundStatisticsService
{
    public static IReadOnlyList<Round> FilterByPeriod(
        IEnumerable<Round> rounds,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        return rounds
            .Where(round => IsInPeriod(round, startDate, endDate))
            .OrderByDescending(round => round.Date)
            .ThenByDescending(round => round.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static RoundStatisticsSummary Summarize(
        IEnumerable<Round> rounds,
        StrokesGainedCategory category,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var periodRounds = FilterByPeriod(rounds, startDate, endDate);
        var trackedRounds = periodRounds
            .Where(round => IsCategoryTracked(round, category))
            .ToList();

        var roundScores = trackedRounds
            .Select(round => new RoundScore(round, CalculateStrokesGained(round, category)))
            .ToList();

        var total = roundScores.Sum(score => score.StrokesGained);

        return new RoundStatisticsSummary(
            startDate?.Date,
            endDate?.Date,
            category,
            periodRounds.Count,
            trackedRounds.Count,
            total,
            roundScores.Count == 0 ? 0 : total / roundScores.Count,
            roundScores.MaxBy(score => score.StrokesGained)?.Round,
            roundScores.MinBy(score => score.StrokesGained)?.Round,
            category is StrokesGainedCategory.Total or StrokesGainedCategory.Putting
                ? CalculatePuttingMadePercentageBuckets(trackedRounds)
                : []);
    }

    public static double CalculateStrokesGained(Round round, StrokesGainedCategory category)
    {
        ArgumentNullException.ThrowIfNull(round);

        var summary = StrokesGainedCalculator.CalculateRoundSummary(round);
        var options = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        return category switch
        {
            StrokesGainedCategory.Total =>
                (options.TrackPutting ? summary.TotalStrokesGainedPutting : 0) +
                (options.TrackApproach ? summary.TotalStrokesGainedApproach : 0) +
                (options.TrackAroundGreen ? summary.TotalStrokesGainedAroundGreen : 0),
            StrokesGainedCategory.Putting => options.TrackPutting ? summary.TotalStrokesGainedPutting : 0,
            StrokesGainedCategory.Approach => options.TrackApproach ? summary.TotalStrokesGainedApproach : 0,
            StrokesGainedCategory.AroundGreen => options.TrackAroundGreen ? summary.TotalStrokesGainedAroundGreen : 0,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown strokes-gained category.")
        };
    }

    public static IReadOnlyList<PuttingMadePercentageBucket> CalculatePuttingMadePercentageBuckets(
        IEnumerable<Round> rounds)
    {
        ArgumentNullException.ThrowIfNull(rounds);

        var puttingHoles = rounds
            .Where(round => (round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly).TrackPutting)
            .SelectMany(round => round.Holes.Where(hole => HoleResultMapper.ToResult(hole).IsPuttingCompleted))
            .ToList();

        var aggregateSummary = StrokesGainedCalculator.CalculateRoundSummary(new Round(
            "putting-statistics",
            DateTime.Today,
            puttingHoles,
            RoundTrackingOptions.PuttingOnly,
            puttingHoles.Count));

        return aggregateSummary.PuttingDistanceBuckets
            .Select(bucket =>
            {
                var bucketHoles = puttingHoles
                    .Select(HoleResultMapper.ToResult)
                    .Where(result => result.Putting is not null &&
                        IsInBucket(result.Putting.FirstPuttDistanceMeters, bucket))
                    .ToList();

                return new PuttingMadePercentageBucket(
                    bucket.Name,
                    bucket.MinimumDistanceMeters,
                    bucket.MaximumDistanceMeters,
                    bucket.Attempts,
                    bucketHoles.Count(result => result.Putting!.Putts == 1),
                    bucket.TotalPutts,
                    bucket.TotalStrokesGained);
            })
            .ToList();
    }

    private static bool IsInPeriod(Round round, DateTime? startDate, DateTime? endDate)
    {
        var date = round.Date.Date;
        return (startDate is null || date >= startDate.Value.Date) &&
            (endDate is null || date <= endDate.Value.Date);
    }

    private static bool IsCategoryTracked(Round round, StrokesGainedCategory category)
    {
        var options = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        return category switch
        {
            StrokesGainedCategory.Total => options.TrackPutting || options.TrackApproach || options.TrackAroundGreen,
            StrokesGainedCategory.Putting => options.TrackPutting,
            StrokesGainedCategory.Approach => options.TrackApproach,
            StrokesGainedCategory.AroundGreen => options.TrackAroundGreen,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown strokes-gained category.")
        };
    }

    private static bool IsInBucket(double distanceMeters, PuttingDistanceBucketSummary bucket)
    {
        if (bucket.MinimumDistanceMeters is null && bucket.MaximumDistanceMeters is not null)
        {
            return distanceMeters < bucket.MaximumDistanceMeters;
        }

        if (bucket.MinimumDistanceMeters is not null && bucket.MaximumDistanceMeters is not null)
        {
            return distanceMeters >= bucket.MinimumDistanceMeters &&
                (bucket.Name == "20-25 ft"
                    ? distanceMeters <= bucket.MaximumDistanceMeters
                    : distanceMeters < bucket.MaximumDistanceMeters);
        }

        if (bucket.MinimumDistanceMeters is not null)
        {
            return distanceMeters > bucket.MinimumDistanceMeters;
        }

        return false;
    }

    private sealed record RoundScore(Round Round, double StrokesGained);
}

public sealed record RoundStatisticsSummary(
    DateTime? StartDate,
    DateTime? EndDate,
    StrokesGainedCategory Category,
    int PeriodRoundCount,
    int TrackedRoundCount,
    double TotalStrokesGained,
    double AverageStrokesGainedPerRound,
    Round? BestRound,
    Round? WorstRound,
    IReadOnlyList<PuttingMadePercentageBucket> PuttingMadePercentageBuckets);

public sealed record PuttingMadePercentageBucket(
    string Name,
    double? MinimumDistanceMeters,
    double? MaximumDistanceMeters,
    int Attempts,
    int MadePutts,
    int TotalPutts,
    double TotalStrokesGained)
{
    public double MadePercentage => Attempts == 0 ? 0 : MadePutts / (double)Attempts * 100;

    public double AveragePutts => Attempts == 0 ? 0 : TotalPutts / (double)Attempts;
}
