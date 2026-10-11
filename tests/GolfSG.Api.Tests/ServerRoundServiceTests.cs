using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.Server;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class ServerRoundServiceTests
{
    private const string Owner = "test-user";
    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("round/1")]
    [DataRow("round.1")]
    [DataRow("røund-1")]
    public async Task InvalidIdentityNeverReachesPersistence(string id)
    {
        var store = new TestRoundStore();
        var result = await new ServerRoundService(store).WriteAsync(Owner, id, new(Guid.NewGuid(), null, null));

        Assert.AreEqual(ServerRoundWriteStatus.InvalidIdentity, result.Status);
        Assert.IsNull(result.Round);
        Assert.AreEqual(0, store.WriteCalls);
    }

    [TestMethod]
    public async Task IdentityExceedingStorageLimitNeverReachesPersistence()
    {
        var store = new TestRoundStore();
        var result = await new ServerRoundService(store).WriteAsync(Owner, new string('a', 101),
            new(Guid.NewGuid(), null, null));

        Assert.AreEqual(ServerRoundWriteStatus.InvalidIdentity, result.Status);
        Assert.AreEqual(0, store.WriteCalls);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task EmptyVersionOrPreconditionNeverReachesPersistence(bool emptyVersion)
    {
        var store = new TestRoundStore();
        var request = new RoundWriteRequest(emptyVersion ? Guid.Empty : Guid.NewGuid(),
            emptyVersion ? null : Guid.Empty, null);
        var result = await new ServerRoundService(store).WriteAsync(Owner, "round-1", request);

        Assert.AreEqual(ServerRoundWriteStatus.InvalidIdentity, result.Status);
        Assert.AreEqual(0, store.WriteCalls);
    }

    [TestMethod]
    public async Task MismatchedPayloadIdentityNeverReachesPersistence()
    {
        var store = new TestRoundStore();
        var result = await new ServerRoundService(store).WriteAsync(Owner, "round-1",
            new(Guid.NewGuid(), null, ValidRound() with { Id = "different-round" }));

        Assert.AreEqual(ServerRoundWriteStatus.InvalidIdentity, result.Status);
        Assert.AreEqual(0, store.WriteCalls);
    }

    [TestMethod]
    [DataRow("date")]
    [DataRow("missing-holes")]
    [DataRow("duplicate-holes")]
    [DataRow("negative-putts")]
    [DataRow("non-finite-distance")]
    [DataRow("configured-count")]
    public async Task InvalidRoundDataNeverReachesPersistence(string defect)
    {
        var round = ValidRound();
        var invalidRound = defect switch
        {
            "date" => round with { Date = default },
            "missing-holes" => round with { Holes = null! },
            "duplicate-holes" => round with { Holes = [round.Holes[0], round.Holes[0]] },
            "negative-putts" => round with { Holes = [round.Holes[0] with { Putts = -1 }] },
            "non-finite-distance" => round with
                { Holes = [round.Holes[0] with { FirstPuttDistanceMeters = double.NaN }] },
            "configured-count" => round with { ConfiguredHoleCount = 0 },
            _ => throw new AssertFailedException("Unknown round defect.")
        };
        var store = new TestRoundStore();
        var result = await new ServerRoundService(store).WriteAsync(Owner, round.Id,
            new(Guid.NewGuid(), null, invalidRound));

        Assert.AreEqual(ServerRoundWriteStatus.InvalidData, result.Status);
        Assert.IsNull(result.Round);
        Assert.AreEqual(0, store.WriteCalls);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ValidRoundAndRetainedDeletionPreserveVersionAndCancellation(bool deletion)
    {
        var round = ValidRound();
        var request = new RoundWriteRequest(Guid.NewGuid(), Guid.NewGuid(), deletion ? null : round);
        var written = new RemoteRound(round.Id, request.Version, request.Round);
        var store = new TestRoundStore { WriteResult = written };
        using var cancellation = new CancellationTokenSource();

        var result = await new ServerRoundService(store).WriteAsync(Owner, round.Id, request, cancellation.Token);

        Assert.AreEqual(ServerRoundWriteStatus.Written, result.Status);
        Assert.AreSame(written, result.Round);
        Assert.AreEqual(1, store.WriteCalls);
        Assert.AreEqual(round.Id, store.LastId);
        Assert.AreEqual(Owner, store.LastUserId);
        Assert.AreSame(request, store.LastRequest);
        Assert.AreEqual(cancellation.Token, store.LastToken);
    }

    [TestMethod]
    public async Task FailedStoragePreconditionReturnsConflict()
    {
        var store = new TestRoundStore();
        var result = await new ServerRoundService(store).WriteAsync(Owner, "round-1",
            new(Guid.NewGuid(), Guid.NewGuid(), null));

        Assert.AreEqual(ServerRoundWriteStatus.Conflict, result.Status);
        Assert.IsNull(result.Round);
        Assert.AreEqual(1, store.WriteCalls);
    }

    [TestMethod]
    public async Task ReadsIncludeRetainedDeletionsAndForwardCancellation()
    {
        var remote = new RemoteRound("round-1", Guid.NewGuid(), null);
        IReadOnlyList<RemoteRound> rounds = [remote];
        var store = new TestRoundStore { Rounds = rounds };
        var service = new ServerRoundService(store);
        using var cancellation = new CancellationTokenSource();

        Assert.AreSame(rounds, await service.GetAllAsync(Owner, cancellation.Token));
        Assert.AreEqual(Owner, store.LastUserId);
        Assert.AreEqual(cancellation.Token, store.LastToken);
        Assert.AreSame(remote, await service.GetByIdAsync(Owner, remote.Id, cancellation.Token));
        Assert.AreEqual(Owner, store.LastUserId);
        Assert.AreEqual(remote.Id, store.LastId);
        Assert.AreEqual(cancellation.Token, store.LastToken);
        Assert.IsNull(await service.GetByIdAsync(Owner, "missing", cancellation.Token));
    }

    internal static Round ValidRound() => new("round-1", new DateTime(2026, 10, 10),
        [StrokesGainedCalculator.BuildHole(1, 3, 2)], ConfiguredHoleCount: 1);
}
