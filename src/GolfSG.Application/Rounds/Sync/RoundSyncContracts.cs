using System.Text.Json.Serialization;
using GolfSG.Core.Models;
using GolfSG.Application.Accounts;

namespace GolfSG.Application.Rounds.Sync;

// A null round is a retained deletion, so another device cannot restore it by accident.
public sealed record RemoteRound(
    [property: JsonRequired] string Id,
    [property: JsonRequired] Guid Version,
    [property: JsonRequired] Round? Round);

public sealed record RoundWriteRequest(
    [property: JsonRequired] Guid Version,
    [property: JsonRequired] Guid? ExpectedVersion,
    [property: JsonRequired] Round? Round);

public interface IRoundsApiService
{
    Task<IReadOnlyList<RemoteRound>> GetRoundsAsync(ApiSession session, CancellationToken cancellationToken = default);
    Task<RemoteRound> WriteAsync(ApiSession session, string id, RoundWriteRequest request,
        CancellationToken cancellationToken = default);
}

public interface IRoundSyncService
{
    Task<RoundSyncResult> SyncAsync(CancellationToken cancellationToken = default);
}

public sealed record RoundSyncResult(int Uploaded, int Downloaded, int Deleted);

public sealed class RoundSyncException(string message, Exception? innerException = null)
    : Exception(message, innerException);
