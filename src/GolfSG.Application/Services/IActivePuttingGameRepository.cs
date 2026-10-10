using GolfSG.Application.Putting;

namespace GolfSG.Application.Services;

public interface IActivePuttingGameRepository
{
    Task<ActivePuttingGameSession?> GetAsync();
    Task SaveAsync(ActivePuttingGameSession session);
    Task DeleteAsync();
}

public sealed class NullActivePuttingGameRepository : IActivePuttingGameRepository
{
    public static NullActivePuttingGameRepository Instance { get; } = new();
    public Task<ActivePuttingGameSession?> GetAsync() => Task.FromResult<ActivePuttingGameSession?>(null);
    public Task SaveAsync(ActivePuttingGameSession session) => Task.CompletedTask;
    public Task DeleteAsync() => Task.CompletedTask;
}
