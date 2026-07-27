using GolfSG.Application.Rounds;
using GolfSG.Application.Services;

namespace GolfSG.Infrastructure.Persistence;

public sealed class FileActiveRoundSessionRepository : IActiveRoundSessionRepository
{
    private readonly ActiveRoundSessionFileStore store;

    public FileActiveRoundSessionRepository(string appDataDirectory)
    {
        store = new ActiveRoundSessionFileStore(appDataDirectory);
    }

    public string ActiveStoragePath => store.ActiveStoragePath;
    public Task<ActiveRoundSession?> GetAsync() => store.GetAsync();
    public Task SaveAsync(ActiveRoundSession session) => store.SaveAsync(session);
    public Task DeleteAsync() => store.DeleteAsync();
}
