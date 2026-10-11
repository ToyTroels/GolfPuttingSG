using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.Server;

namespace GolfSG.Api.Tests;

internal sealed class TestRoundStore : IServerRoundStore
{
    public IReadOnlyList<RemoteRound> Rounds { get; set; } = [];
    public RemoteRound? WriteResult { get; set; }
    public int WriteCalls { get; private set; }
    public string? LastId { get; private set; }
    public string? LastUserId { get; private set; }
    public RoundWriteRequest? LastRequest { get; private set; }
    public CancellationToken LastToken { get; private set; }

    public Task<IReadOnlyList<RemoteRound>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        LastUserId = userId;
        LastToken = cancellationToken;
        return Task.FromResult(Rounds);
    }

    public Task<RemoteRound?> GetByIdAsync(string userId, string id, CancellationToken cancellationToken = default)
    {
        LastUserId = userId;
        LastId = id;
        LastToken = cancellationToken;
        return Task.FromResult(Rounds.SingleOrDefault(round => round.Id == id));
    }

    public Task<RemoteRound?> WriteAsync(string userId, string id, RoundWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        WriteCalls++;
        LastUserId = userId;
        LastId = id;
        LastRequest = request;
        LastToken = cancellationToken;
        return Task.FromResult(WriteResult);
    }
}
