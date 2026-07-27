using GolfSG.Application.Rounds;
using GolfSG.Application.Services;

namespace GolfSG.Tests;

internal sealed class TestActiveRoundSessionRepository(
    ActiveRoundSession? session = null) : IActiveRoundSessionRepository
{
    public ActiveRoundSession? Session { get; private set; } = session;
    public int SaveCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public Exception? SaveException { get; set; }
    public Exception? DeleteException { get; set; }

    public string ActiveStoragePath => string.Empty;

    public Task<ActiveRoundSession?> GetAsync() =>
        Task.FromResult(Session);

    public Task SaveAsync(ActiveRoundSession value)
    {
        SaveCallCount++;
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Session = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync()
    {
        DeleteCallCount++;
        if (DeleteException is not null)
        {
            throw DeleteException;
        }

        Session = null;
        return Task.CompletedTask;
    }
}
