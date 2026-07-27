using GolfSG.Application.Services;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public sealed class BenchmarkHistoryQueryService(IRoundRepository repository) : IBenchmarkHistoryQueryService
{
    public async Task<IReadOnlyList<BenchmarkHistorySummary>> LoadAsync()
    {
        var rounds = await repository.GetRoundsAsync();
        return BenchmarkHistoryService.Summarize(rounds);
    }
}