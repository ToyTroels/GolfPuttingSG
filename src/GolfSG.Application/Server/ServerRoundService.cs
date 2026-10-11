using System.Text.Json;
using GolfSG.Application.Rounds.Sync;

namespace GolfSG.Application.Server;

public enum ServerRoundWriteStatus { Written, InvalidIdentity, InvalidData, Conflict }

public sealed record ServerRoundWriteResult(ServerRoundWriteStatus Status, RemoteRound? Round = null);

/// <summary>Validates uploaded history without recalculating or replacing the original round.</summary>
public sealed class ServerRoundService(IServerRoundStore store)
{
    public Task<IReadOnlyList<RemoteRound>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return store.GetAllAsync(userId, cancellationToken);
    }

    public Task<RemoteRound?> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return store.GetByIdAsync(userId, id, cancellationToken);
    }

    public async Task<ServerRoundWriteResult> WriteAsync(string userId, string id, RoundWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        if (!RoundSyncValidation.IsValidId(id) || request.Version == Guid.Empty ||
            request.ExpectedVersion == Guid.Empty || request.Round is { } data && data.Id != id)
            return new(ServerRoundWriteStatus.InvalidIdentity);

        if (request.Round is { } round)
        {
            try { RoundSyncValidation.Validate(round); }
            catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException)
            { return new(ServerRoundWriteStatus.InvalidData); }
        }

        var written = await store.WriteAsync(userId, id, request, cancellationToken);
        return written is null
            ? new(ServerRoundWriteStatus.Conflict)
            : new(ServerRoundWriteStatus.Written, written);
    }
}
