using GolfSG.Application.Services;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public sealed class RoundStatisticsQueryService(IRoundRepository repository) : IRoundStatisticsQueryService
{
    public Task<IReadOnlyList<Round>> LoadRoundsAsync() => repository.GetRoundsAsync();

    public RoundStatisticsSummary Summarize(
        IReadOnlyList<Round> rounds,
        StrokesGainedCategory category,
        DateTime? startDate = null,
        DateTime? endDate = null) =>
        RoundStatisticsService.Summarize(rounds, category, startDate, endDate);
}