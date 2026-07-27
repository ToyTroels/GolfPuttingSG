using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public interface IRoundResultQueryService
{
    Task<RoundResultQuery?> LoadAsync(string roundId);
}

public sealed record RoundResultQuery(
    Round Round,
    RoundSummary Summary,
    RoundTrackingOptions TrackingOptions);