using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;

namespace GolfSG.Application.Rounds;

public sealed record RoundDraft(
    string Id,
    DateTime Date,
    IReadOnlyList<HolePuttingData> Holes,
    RoundTrackingOptions TrackingOptions,
    int ConfiguredHoleCount);

public interface IRoundApplicationService
{
    int ClampHoleCount(int requestedCount, int highestEnteredHoleNumber = 1);
    bool IsComplete(RoundDraft draft);
    Round CreateRound(RoundDraft draft);
    RoundSummary Summarize(RoundDraft draft);
    Task<Round?> LoadAsync(string roundId);
    Task<Round> SaveAsync(RoundDraft draft);
}

public sealed class RoundApplicationService(IRoundRepository repository) : IRoundApplicationService
{
    public const int MinimumHoleCount = 1;
    public const int MaximumHoleCount = 36;

    public int ClampHoleCount(int requestedCount, int highestEnteredHoleNumber = MinimumHoleCount)
    {
        var minimum = Math.Clamp(highestEnteredHoleNumber, MinimumHoleCount, MaximumHoleCount);
        return Math.Clamp(requestedCount, minimum, MaximumHoleCount);
    }

    public bool IsComplete(RoundDraft draft)
    {
        var round = CreateRound(draft);
        return round.CompletedHoleCount >= round.ConfiguredHoleCount;
    }

    public Round CreateRound(RoundDraft draft)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.Id);
        ArgumentNullException.ThrowIfNull(draft.Holes);
        ArgumentNullException.ThrowIfNull(draft.TrackingOptions);

        var configuredHoleCount = ClampHoleCount(draft.ConfiguredHoleCount);
        var round = new Round(
            draft.Id,
            draft.Date,
            draft.Holes.ToList(),
            draft.TrackingOptions,
            configuredHoleCount,
            false);

        return round with { EndedEarly = round.CompletedHoleCount < configuredHoleCount };
    }

    public RoundSummary Summarize(RoundDraft draft) =>
        StrokesGainedCalculator.CalculateRoundSummary(CreateRound(draft));

    public Task<Round?> LoadAsync(string roundId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roundId);
        return repository.GetRoundAsync(roundId);
    }

    public async Task<Round> SaveAsync(RoundDraft draft)
    {
        var round = CreateRound(draft);
        await repository.SaveRoundAsync(round);
        return round;
    }
}
