using GolfSG.Application.Rounds;

namespace GolfSG.Application.Services;

public interface IActiveRoundSessionRepository
{
    string ActiveStoragePath { get; }
    Task<ActiveRoundSession?> GetAsync();
    Task SaveAsync(ActiveRoundSession session);
    Task DeleteAsync();
}

public sealed class NullActiveRoundSessionRepository : IActiveRoundSessionRepository
{
    public static NullActiveRoundSessionRepository Instance { get; } = new();

    private NullActiveRoundSessionRepository()
    {
    }

    public string ActiveStoragePath => string.Empty;
    public Task<ActiveRoundSession?> GetAsync() => Task.FromResult<ActiveRoundSession?>(null);
    public Task SaveAsync(ActiveRoundSession session) => Task.CompletedTask;
    public Task DeleteAsync() => Task.CompletedTask;
}
