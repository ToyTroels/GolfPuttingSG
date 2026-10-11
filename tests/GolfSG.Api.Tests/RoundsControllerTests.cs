using System.Reflection;
using System.Security.Claims;
using GolfSG.Api.Controllers;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class RoundsControllerTests
{
    private const string Owner = "authenticated-user";
    [TestMethod]
    public async Task MissingRoundReturnsNotFound()
    {
        var controller = Controller(new TestRoundStore());

        Assert.IsInstanceOfType<NotFoundResult>((await controller.GetById("missing", default)).Result);
    }

    [TestMethod]
    public async Task RetainedDeletionIsAnExistingRound()
    {
        var remote = new RemoteRound("deleted-round", Guid.NewGuid(), null);
        var store = new TestRoundStore { Rounds = [remote] };
        var controller = Controller(store);

        var response = (await controller.GetById(remote.Id, default)).Result;

        Assert.IsInstanceOfType<OkObjectResult>(response);
        Assert.AreSame(remote, ((OkObjectResult)response).Value);
        Assert.AreEqual(Owner, store.LastUserId);
    }

    [TestMethod]
    public async Task CollectionIncludesSavedRoundsAndRetainedDeletions()
    {
        var round = ServerRoundServiceTests.ValidRound();
        IReadOnlyList<RemoteRound> rounds =
        [new(round.Id, Guid.NewGuid(), round), new("deleted-round", Guid.NewGuid(), null)];
        var controller = Controller(new TestRoundStore { Rounds = rounds });
        using var cancellation = new CancellationTokenSource();

        var response = (await controller.GetAll(cancellation.Token)).Result;

        Assert.IsInstanceOfType<OkObjectResult>(response);
        CollectionAssert.AreEqual(rounds.ToArray(),
            ((IEnumerable<RemoteRound>)((OkObjectResult)response).Value!).ToArray());
    }

    [TestMethod]
    [DataRow("identity", "Invalid round ID or version.")]
    [DataRow("data", "Invalid round data.")]
    public async Task InvalidWritesReturnBadRequestWithExistingProblemTitle(string defect, string title)
    {
        var round = ServerRoundServiceTests.ValidRound();
        var id = defect == "identity" ? "invalid/id" : round.Id;
        var request = new RoundWriteRequest(Guid.NewGuid(), null,
            defect == "data" ? round with { Date = default } : null);
        var store = new TestRoundStore();

        var response = (await Controller(store).Write(id, request, default)).Result;

        Assert.IsInstanceOfType<BadRequestObjectResult>(response);
        Assert.IsInstanceOfType<ProblemDetails>(((BadRequestObjectResult)response).Value);
        Assert.AreEqual(title, ((ProblemDetails)((BadRequestObjectResult)response).Value!).Title);
        Assert.AreEqual(0, store.WriteCalls);
    }

    [TestMethod]
    public async Task ConcurrentWriteReturnsConflictWithExistingProblemTitle()
    {
        var response = (await Controller(new TestRoundStore()).Write("round-1",
            new(Guid.NewGuid(), Guid.NewGuid(), null), default)).Result;

        Assert.IsInstanceOfType<ConflictObjectResult>(response);
        Assert.AreEqual("The round changed. Read its current version and retry.",
            ((ProblemDetails)((ConflictObjectResult)response).Value!).Title);
    }

    [TestMethod]
    public async Task SuccessfulWriteReturnsStoredVersionAndForwardsCancellation()
    {
        var round = ServerRoundServiceTests.ValidRound();
        var request = new RoundWriteRequest(Guid.NewGuid(), null, round);
        var remote = new RemoteRound(round.Id, request.Version, round);
        var store = new TestRoundStore { WriteResult = remote };
        using var cancellation = new CancellationTokenSource();

        var response = (await Controller(store).Write(round.Id, request, cancellation.Token)).Result;

        Assert.IsInstanceOfType<OkObjectResult>(response);
        Assert.AreSame(remote, ((OkObjectResult)response).Value);
        Assert.AreEqual(cancellation.Token, store.LastToken);
        Assert.AreEqual(Owner, store.LastUserId);
    }

    [TestMethod]
    public void RoundEndpointsRequireIdentityBearerAuthentication()
    {
        var authorization = typeof(RoundsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.IsNotNull(authorization);
        Assert.AreEqual(IdentityConstants.BearerScheme, authorization.AuthenticationSchemes);
    }

    [TestMethod]
    public async Task OwnershipComesOnlyFromAuthenticatedIdentifierClaim()
    {
        var store = new TestRoundStore();
        var controller = Controller(store);
        controller.HttpContext.User.AddIdentity(new ClaimsIdentity(
            [new Claim("user_id", "spoofed-owner"), new Claim(ClaimTypes.Name, "different-name")], "extra"));

        await controller.GetAll(default);
        Assert.AreEqual(Owner, store.LastUserId);
        await controller.GetById("round-1", default);
        Assert.AreEqual(Owner, store.LastUserId);
        await controller.Write("round-1", new(Guid.NewGuid(), null, null), default);
        Assert.AreEqual(Owner, store.LastUserId);
    }

    [TestMethod]
    public void RoutesAndPayloadLimitRemainCompatibleWithExistingClients()
    {
        var controller = typeof(RoundsController);
        Assert.AreEqual("api/rounds", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        var write = controller.GetMethod(nameof(RoundsController.Write))!;
        Assert.AreEqual("{id}", write.GetCustomAttribute<HttpPutAttribute>()!.Template);
        var limit = (IRequestSizeLimitMetadata)write.GetCustomAttribute<RequestSizeLimitAttribute>()!;
        Assert.AreEqual(2L * 1024 * 1024, limit.MaxRequestBodySize);
    }

    private static RoundsController Controller(TestRoundStore store) => new(new ServerRoundService(store))
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Owner)], IdentityConstants.BearerScheme))
            }
        }
    };
}
