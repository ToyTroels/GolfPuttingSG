using GolfSG.Application.Services;

namespace GolfSG.Application.Queries;

public sealed class RoundHistoryQueryService(IRoundRepository repository) : IRoundHistoryQueryService
{
    public async Task<RoundHistoryQueryResult> LoadAsync()
    {
        var rounds = await repository.GetRoundsAsync();
        return new RoundHistoryQueryResult(rounds, repository.WasLastReadRecoveredFromBackup);
    }
}