using System.Text.Json;
using GolfSG.Application.Courses;
using GolfSG.Core.Models;

namespace GolfSG.Application.Rounds.Sync;

public static class RoundSyncValidation
{
    public static bool IsValidId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 100 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    public static void Validate(Round round)
    {
        ArgumentNullException.ThrowIfNull(round);
        if (!IsValidId(round.Id) || round.Date == default || round.Holes is null ||
            round.ConfiguredHoleCount is < 1 or > 2000 || round.Holes.Count > 2000 ||
            round.Holes.Any(h => h is null || h.HoleNumber is < 1 or > 2000 || h.Putts < 0 ||
                h.ApproachShots < 0 || h.AroundGreenShots?.Any(s => s is null) == true) ||
            round.Holes.Select(h => h.HoleNumber).Distinct().Count() != round.Holes.Count)
            throw new ArgumentException("Runden indeholder ugyldige oplysninger.");

        if (round.CoursePractice is { } practice)
        {
            CoursePracticeService.ValidateResults(practice.Course, practice.Results, requireComplete: true);
            if (round.Holes.Count != practice.Results.Count || round.ConfiguredHoleCount != practice.Results.Count)
                throw new ArgumentException("Banetræningen mangler slagresultater.");
        }

        // Also rejects non-finite distances and scores throughout the complete payload.
        _ = JsonSerializer.Serialize(round, RoundSyncJsonContext.Default.Round);
    }
}
