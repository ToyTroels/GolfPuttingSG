using GolfSG.Core;
using GolfSG.Core.Models;
using System.Globalization;

namespace GolfSG.Application.Server;

public static class PersonalPerformanceMapper
{
    public static bool IsPractice(Round round) => round.CoursePractice is not null || round.GameInfo is not null || round.TrackingOptions?.IsPuttingGame == true;
    public static PersonalRoundDetail Detail(Round round)
    {
        var rows = new List<PersonalScoreRow>();
        if (round.CoursePractice is { } practice)
        {
            var shots = practice.PlayedShots;
            var attempts = new Dictionary<string, int>();
            for (var i = 0; i < practice.Results.Count; i++)
            {
                var shot = shots[i]; var result = practice.Results[i];
                attempts[shot.Id] = attempts.GetValueOrDefault(shot.Id) + 1;
                rows.Add(new(i + 1, $"{shot.Name} · attempt {attempts[shot.Id]}",
                    FormattableString.Invariant($"{shot.DistanceMeters(practice.Course):0.##} m from {shot.Lie} → {(result.Outcome.Holed ? "Holed" : result.Outcome.EndDistanceMeters.ToString("0.##", CultureInfo.InvariantCulture) + " m · " + result.Outcome.EndLie)}"),
                    1, result.Outcome.PenaltyStrokes,
                    shot.Category == PracticeShotCategory.Putting ? result.StrokesGained : null,
                    shot.Category == PracticeShotCategory.Approach ? result.StrokesGained : null,
                    shot.Category == PracticeShotCategory.AroundGreen ? result.StrokesGained : null, result.StrokesGained));
            }
            var planned = practice.Course.Shots.Sum(s => s.Attempts);
            return new(new(round.Id, practice.Course.Name, round.Date, true, "Course practice", rows.Count, planned,
                rows.Sum(r => r.TotalSg), rows.Sum(r => r.Strokes + r.Penalties), Sum(rows, r => r.PuttingSg), Sum(rows, r => r.ApproachSg), Sum(rows, r => r.AroundGreenSg)), rows);
        }
        var tracking = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        foreach (var hole in round.Holes.OrderBy(h => h.HoleNumber))
        {
            var putting = tracking.TrackPutting && hole.IsCompleted;
            var approach = tracking.TrackApproach && hole.IsApproachCompleted;
            var around = tracking.TrackAroundGreen && hole.IsAroundGreenCompleted;
            var p = putting ? hole.StrokesGainedPutting : (double?)null;
            var a = approach ? hole.StrokesGainedApproach : (double?)null;
            var g = around ? hole.StrokesGainedAroundGreen : (double?)null;
            var distances = new List<string>();
            if (putting) distances.Add(FormattableString.Invariant($"Putting {hole.FirstPuttDistanceMeters:0.##} m"));
            if (approach) distances.Add(FormattableString.Invariant($"Approach {hole.ApproachDistanceMeters:0.##} m"));
            if (around) distances.Add(FormattableString.Invariant($"Around green {DistanceConversions.YardsToMeters(hole.AroundGreenStartDistanceYards):0.##} m"));
            rows.Add(new(hole.HoleNumber, $"{(IsPractice(round) ? "Attempt" : "Hole")} {hole.HoleNumber}",
                distances.Count == 0 ? "No recorded result" : string.Join(" · ", distances),
                (putting ? hole.Putts : 0) + (approach ? hole.ApproachShots : 0) + (around ? hole.AroundGreenShotCount : 0),
                (approach ? hole.ApproachPenaltyStrokes : 0) + (around ? hole.AroundGreenPenaltyStrokes : 0),
                p, a, g, (p ?? 0) + (a ?? 0) + (g ?? 0)));
        }
        return new(new(round.Id, round.GameInfo?.DisplayName ?? (IsPractice(round) ? "Putting practice" : "Golf round"),
            round.Date, IsPractice(round), IsPractice(round) ? "Putting practice" : "Normal round", round.CompletedHoleCount,
            round.GameInfo?.AttemptCount ?? round.ConfiguredHoleCount, RoundStatisticsService.CalculateStrokesGained(round, StrokesGainedCategory.Total), rows.Sum(r => r.Strokes + r.Penalties), Sum(rows, r => r.PuttingSg), Sum(rows, r => r.ApproachSg), Sum(rows, r => r.AroundGreenSg)), rows);
    }

    private static double? Sum(IEnumerable<PersonalScoreRow> rows, Func<PersonalScoreRow, double?> select)
    {
        var values = rows.Select(select).Where(v => v.HasValue).ToArray();
        return values.Length == 0 ? null : values.Sum(v => v!.Value);
    }

    public static PersonalPerformance Overview(IReadOnlyList<Round> rounds, int unavailable = 0, int window = 30)
    {
        var summaries = rounds.Select(r => Detail(r).Summary).OrderByDescending(r => r.Date).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
        var normal = rounds.Where(r => !IsPractice(r)).ToArray();
        var scored = summaries.Where(s => !s.Practice && s.Completed > 0).ToArray();
        var categories = new List<PersonalCategoryScore>();
        foreach (var (category, name) in new[] { (StrokesGainedCategory.Putting, "Putting"), (StrokesGainedCategory.Approach, "Approach"), (StrokesGainedCategory.AroundGreen, "Around green") })
        {
            var eligible = normal.Where(r => category switch
            {
                StrokesGainedCategory.Putting => r.Holes.Any(h => h.IsCompleted),
                StrokesGainedCategory.Approach => r.Holes.Any(h => h.IsApproachCompleted),
                _ => r.Holes.Any(h => h.IsAroundGreenCompleted)
            }).ToArray();
            var stats = RoundStatisticsService.Summarize(eligible, category);
            if (stats.TrackedRoundCount > 0) categories.Add(new(name, stats.TrackedRoundCount, stats.AverageStrokesGainedPerRound));
        }
        return new(window, rounds.Count, unavailable, summaries.Count(s => !s.Practice), summaries.Count(s => s.Practice),
            scored.Sum(s => s.TotalSg), scored.Length == 0 ? null : scored.Average(s => s.TotalSg), summaries.Take(12).ToArray(),
            scored.Take(12).Reverse().ToArray(), categories, summaries);
    }
}
