using GolfSG.Application.Services;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public sealed class RoundStatisticsQueryService(IRoundRepository repository) : IRoundStatisticsQueryService
{
    public async Task<IReadOnlyList<Round>> LoadRoundsAsync() => (await repository.GetRoundsAsync()).Where(r => r.CoursePractice is null).ToArray();

    public RoundStatisticsSummary Summarize(
        IReadOnlyList<Round> rounds,
        StrokesGainedCategory category,
        DateTime? startDate = null,
        DateTime? endDate = null) =>
        RoundStatisticsService.Summarize(rounds, category, startDate, endDate);
}
