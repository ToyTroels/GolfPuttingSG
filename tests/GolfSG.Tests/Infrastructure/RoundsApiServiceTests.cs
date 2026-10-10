using System.Net;
using System.Text;
using System.Text.Json;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Rounds;

namespace GolfSG.Tests.Infrastructure;

[TestClass]
public sealed class RoundsApiServiceTests
{
    private static readonly Uri Server = new("https://localhost:7151/backend/");

    [TestMethod]
    public async Task FullRoundIncludingGameAndShotDetailsSurvivesJsonRoundTrip()
    {
        var hole = StrokesGainedCalculator.BuildHole(1, 3, 2) with
        {
            AroundGreenShots = [new GolfShot { HoleNumber = 1, StartDistanceToPin = 10, EndDistanceToPin = 2,
                StartLie = ShotLie.Rough, EndLie = ShotLie.Green, ShotNumber = 1, PenaltyStrokes = 1 }],
            GreenInRegulation = false
        };
        var round = new Round("round_1", new DateTime(2026, 10, 10), [hole], RoundTrackingOptions.PuttingGame, 1,
            GameInfo: PuttingGame.CreateRoundGameInfo(PuttingGame.GetDefinition(PuttingGame.LadderMode)));
        var remote = new RemoteRound(round.Id, Guid.NewGuid(), round);
        Uri? requested = null;
        using var client = new HttpClient(new Handler((request, _) =>
        {
            requested = request.RequestUri;
            return Task.FromResult(Json(JsonSerializer.Serialize(new List<RemoteRound> { remote },
                RoundSyncJsonContext.Default.ListRemoteRound)));
        }));
        var result = (await new RoundsApiService(client).GetRoundsAsync(Server)).Single();
        Assert.AreEqual("https://localhost:7151/backend/api/rounds", requested!.AbsoluteUri);
        Assert.AreEqual(JsonSerializer.Serialize(round, RoundSyncJsonContext.Default.Round),
            JsonSerializer.Serialize(result.Round, RoundSyncJsonContext.Default.Round));
        Assert.AreEqual(1, result.Round!.Holes[0].AroundGreenShots![0].PenaltyStrokes);
    }

    [TestMethod]
    public async Task WritesVersionPreconditionAndRecognizesRetainedDeletion()
    {
        var version = Guid.NewGuid();
        var expected = Guid.NewGuid();
        using var client = new HttpClient(new Handler(async (request, token) =>
        {
            Assert.AreEqual(HttpMethod.Put, request.Method);
            Assert.AreEqual("https://localhost:7151/backend/api/rounds/round-1", request.RequestUri!.AbsoluteUri);
            var body = JsonSerializer.Deserialize(await request.Content!.ReadAsStringAsync(token),
                RoundSyncJsonContext.Default.RoundWriteRequest)!;
            Assert.AreEqual(version, body.Version);
            Assert.AreEqual(expected, body.ExpectedVersion);
            Assert.IsNull(body.Round);
            return Json(JsonSerializer.Serialize(new RemoteRound("round-1", version, null), RoundSyncJsonContext.Default.RemoteRound));
        }));
        Assert.IsNull((await new RoundsApiService(client).WriteAsync(Server, "round-1", new(version, expected, null))).Round);
    }

    [TestMethod]
    public async Task CompletedCoursePracticeRoundRetainsItsFrozenCourseAndOutcomes()
    {
        var shot = new CoursePracticeShot("putt", "Putting", new(0.1, 0.1), new(0.5, 0.5),
            ShotLie.Green, PracticeShotCategory.Putting, 3);
        var course = new PracticeCourse("course-1", "Træningsbane", "practice.png", 1, 10, "Målt", [shot]);
        var result = new CourseShotResult(new(0, ShotLie.Holed, 0, true), 1.5, 0, 0.5, shot.Id);
        var round = new Round("practice-1", new DateTime(2026, 10, 10),
            [StrokesGainedCalculator.BuildHole(1, 3, 1)], ConfiguredHoleCount: 1,
            CoursePractice: new(course, [result]));
        RoundSyncValidation.Validate(round);
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(JsonSerializer.Serialize(
            new List<RemoteRound> { new(round.Id, Guid.NewGuid(), round) }, RoundSyncJsonContext.Default.ListRemoteRound)))));
        var downloaded = (await new RoundsApiService(client).GetRoundsAsync(Server)).Single().Round!;
        Assert.AreEqual(course, downloaded.CoursePractice!.Course with { Shots = course.Shots });
        Assert.AreEqual(result, downloaded.CoursePractice.Results.Single());
        Assert.AreEqual(shot, downloaded.CoursePractice.Course.Shots.Single());
        Assert.AreEqual(JsonSerializer.Serialize(round, RoundSyncJsonContext.Default.Round),
            JsonSerializer.Serialize(downloaded, RoundSyncJsonContext.Default.Round));
    }

    [TestMethod]
    public async Task RejectsPayloadWithMismatchedRoundIdentity()
    {
        var round = new Round("different-id", DateTime.Today, [], ConfiguredHoleCount: 1);
        var body = JsonSerializer.Serialize(new List<RemoteRound> { new("round-1", Guid.NewGuid(), round) },
            RoundSyncJsonContext.Default.ListRemoteRound);
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(body))));
        await Assert.ThrowsAsync<RoundSyncException>(() => new RoundsApiService(client).GetRoundsAsync(Server));
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("[null]")]
    [DataRow("[{}]")]
    [DataRow("[{\"id\":\"round-1\",\"version\":\"00000000-0000-0000-0000-000000000000\",\"round\":null}]")]
    public async Task RejectsMalformedResponseBeforeChangingLocalData(string body)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(body))));
        await Assert.ThrowsAsync<RoundSyncException>(() => new RoundsApiService(client).GetRoundsAsync(Server));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Conflict)]
    [DataRow(HttpStatusCode.InternalServerError)]
    public async Task ReturnsReadableErrorWithoutExposingServerDetails(HttpStatusCode code)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json("password secret", code))));
        var exception = await Assert.ThrowsAsync<RoundSyncException>(() => new RoundsApiService(client).GetRoundsAsync(Server));
        Assert.DoesNotContain("secret", exception.Message);
    }

    [TestMethod]
    public async Task SeparatesTimeoutFromCallerCancellation()
    {
        using var client = new HttpClient(new Handler((_, _) => throw new TaskCanceledException()));
        var service = new RoundsApiService(client);
        var exception = await Assert.ThrowsAsync<RoundSyncException>(() => service.GetRoundsAsync(Server));
        Assert.Contains("i tide", exception.Message);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetRoundsAsync(Server, canceled.Token));
    }

    [TestMethod]
    public async Task RejectsNonHttpsAddress()
    {
        using var client = new HttpClient(new Handler((_, _) => throw new AssertFailedException("Unexpected network request")));
        await Assert.ThrowsAsync<RoundSyncException>(() => new RoundsApiService(client).GetRoundsAsync(new Uri("http://localhost/")));
    }

    [TestMethod]
    public void ValidationRejectsMissingHolesNonFiniteValuesAndDuplicateHoleNumbers()
    {
        var round = new Round("round-1", DateTime.Today, [StrokesGainedCalculator.BuildHole(1, 3, 2)], ConfiguredHoleCount: 1);
        Assert.Throws<ArgumentException>(() => RoundSyncValidation.Validate(round with { Holes = null! }));
        Assert.Throws<ArgumentException>(() => RoundSyncValidation.Validate(round with { Holes = [round.Holes[0], round.Holes[0]] }));
        Assert.Throws<ArgumentException>(() => RoundSyncValidation.Validate(round with
            { Holes = [round.Holes[0] with { FirstPuttDistanceMeters = double.NaN }] }));
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handler(request, cancellationToken);
    }
}
