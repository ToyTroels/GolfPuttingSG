using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public interface IRoundStatisticsQueryService
{
    Task<IReadOnlyList<Round>> LoadRoundsAsync();

    RoundStatisticsSummary Summarize(
        IReadOnlyList<Round> rounds,
        StrokesGainedCategory category,
        DateTime? startDate = null,
        DateTime? endDate = null);
}