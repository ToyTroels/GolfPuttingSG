using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Queries;

public interface IBenchmarkHistoryQueryService
{
    Task<IReadOnlyList<BenchmarkHistorySummary>> LoadAsync();
}