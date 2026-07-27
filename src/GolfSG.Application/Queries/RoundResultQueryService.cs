using GolfSG.Application.Services;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public sealed class RoundResultQueryService(IRoundRepository repository) : IRoundResultQueryService
{
    public async Task<RoundResultQuery?> LoadAsync(string roundId)
    {
        var round = await repository.GetRoundAsync(roundId);
        if (round is null)
        {
            return null;
        }

        var trackingOptions = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        return new RoundResultQuery(
            round,
            StrokesGainedCalculator.CalculateRoundSummary(round),
            trackingOptions);
    }
}