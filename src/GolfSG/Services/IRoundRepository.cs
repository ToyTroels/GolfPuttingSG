using GolfSG.Core.Models;

namespace GolfSG.Services;

public interface IRoundRepository
{
    Task<IReadOnlyList<Round>> GetRoundsAsync();
    Task<Round?> GetRoundAsync(string roundId);
    Task SaveRoundAsync(Round round);
    Task DeleteRoundAsync(string roundId);
}
