using GolfPuttingSG.Core.Models;

namespace GolfPuttingSG.Services;

public interface IRoundRepository
{
    Task<IReadOnlyList<Round>> GetRoundsAsync();
    Task<Round?> GetRoundAsync(string roundId);
    Task SaveRoundAsync(Round round);
}
