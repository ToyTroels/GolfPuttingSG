using GolfSG.Application.Services;
using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public interface IRoundHistoryQueryService
{
    Task<RoundHistoryQueryResult> LoadAsync();
}

public sealed record RoundHistoryQueryResult(
    IReadOnlyList<Round> Rounds,
    bool WasRecoveredFromBackup);