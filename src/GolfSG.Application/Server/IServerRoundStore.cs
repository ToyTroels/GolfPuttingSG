using GolfSG.Application.Rounds.Sync;

namespace GolfSG.Application.Server;

/// <summary>Server history, including retained deletions; independent of device file storage.</summary>
public interface IServerRoundStore
{
    Task<IReadOnlyList<RemoteRound>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
    Task<RemoteRound?> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default);

    /// <summary>Atomically writes with a version precondition. Null indicates a conflict.</summary>
    Task<RemoteRound?> WriteAsync(string userId, string id, RoundWriteRequest request,
        CancellationToken cancellationToken = default);
}
