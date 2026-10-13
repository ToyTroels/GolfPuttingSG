using System.Net;
using System.Text;
using System.Text.Json;
using GolfSG.Application.Accounts;
using GolfSG.Application.Courses;
using GolfSG.Application.Products;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Courses;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Tests.Infrastructure.TestDoubles;

namespace GolfSG.Tests.Infrastructure;

[TestClass]
public sealed class CourseCommunityClientTests
{
    private static CoursePackage Package() => new(new("course", "Course", "course.png", 1, 20, "Measured",
        [new("shot", "Putting", new(.1, .1), new(.2, .2), ShotLie.Green, PracticeShotCategory.Putting, 3, 2)]),
        [137, 80, 78, 71, 13, 10, 26, 10]);

    [TestMethod]
    public async Task DownloadUsesAuthenticatedServerPathAndRejectsAccountChangedDuringRequest()
    {
        var settings = new Settings(); var accounts = new TestApiAccountService(settings);
        var session = accounts.Session(new(settings.BaseAddress)); var id = Guid.NewGuid(); var revision = Guid.NewGuid();
        var changeAccount = false;
        using var client = new HttpClient(new Handler((request, _) =>
        {
            Assert.AreEqual($"{settings.BaseAddress}api/community/courses/{id}/revisions/{revision}", request.RequestUri!.AbsoluteUri);
            Assert.AreEqual("Bearer test-access-token", request.Headers.Authorization!.ToString());
            if (changeAccount) accounts.AccountId = "another-user";
            return Task.FromResult(Json(JsonSerializer.Serialize(Package(), CommunityJsonContext.Default.CoursePackage)));
        }));
        var api = new CourseCommunityApi(client, accounts);
        CollectionAssert.AreEqual(Package().Image, (await api.DownloadAsync(session, id, revision)).Image);
        changeAccount = true;
        await Assert.ThrowsExactlyAsync<ApiAccountException>(() => api.DownloadAsync(session, id, revision));
    }

    [TestMethod]
    public async Task OfflineGameProgressAndPendingResultSurviveRestartAndCannotChangeQuota()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new Settings(); var accounts = new TestApiAccountService(settings); var session = accounts.Session(new(settings.BaseAddress));
            var game = Game();
            var roundId = Guid.NewGuid().ToString("N"); var joined = new JoinedCourseGame(game, roundId);
            var rounds = new FileRoundRepository(directory);
            var service = Practice(new FileCoursePracticeRepository(directory), rounds);
            using var client = new HttpClient(new Handler((_, _) => throw new HttpRequestException("Offline")));
            var workflow = new CourseCommunityWorkflow(new CourseCommunityApi(client, accounts), accounts, settings, service, rounds, directory);
            await workflow.SaveDownloadAsync(session, Package());
            var active = await workflow.StartAsync(session, joined);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.UpdateAttemptsAsync(active.Id, "shot", 3, 0, "shot", 2));
            await service.SaveInputAsync(active.Id, new("0,5", ShotLie.Green, 1), 0, "shot");
            var restart = Practice(new FileCoursePracticeRepository(directory), rounds);
            var recovered = (await restart.LoadAsync()).Active!;
            Assert.AreEqual(active.Game, recovered.Game); Assert.AreEqual("0,5", recovered.Input.Distance);
            Assert.AreEqual(roundId, recovered.Id);
            await restart.RecordAsync(roundId, 0, new(.5, ShotLie.Green, 1, false), "shot");
            await restart.RecordAsync(roundId, 1, new(0, ShotLie.Holed, 0, true), "shot");
            await restart.FinishAsync(roundId);
            var persisted = (await new FileRoundRepository(directory).GetRoundAsync(roundId))!;
            Assert.AreEqual(active.Game, persisted.CoursePractice!.Game);
            Assert.IsNull((await restart.LoadAsync()).Active);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => workflow.SubmitAsync(session, persisted));
            Assert.IsNotNull(await rounds.GetRoundAsync(roundId), "Failed submission must never remove personal History.");

            SubmitCourseGameRequest? submitted = null;
            using var online = new HttpClient(new Handler(async (request, token) =>
            {
                submitted = JsonSerializer.Deserialize(await request.Content!.ReadAsStringAsync(token), CommunityJsonContext.Default.SubmitCourseGameRequest);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }));
            var retried = new CourseCommunityWorkflow(new CourseCommunityApi(online, accounts), accounts, settings, restart, rounds, directory);
            await retried.SubmitAsync(session, persisted); await retried.SubmitAsync(session, persisted);
            Assert.AreEqual(roundId, submitted!.RoundId); Assert.HasCount(2, submitted.Outcomes);
            await Assert.ThrowsExactlyAsync<ApiAccountException>(() => retried.SubmitAsync(session with { AccountId = "other" }, persisted));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task LostCreateResponseReusesDurableRequestIdAfterRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "course-images"));
            await File.WriteAllBytesAsync(Path.Combine(directory, "course-images", "course.png"), Package().Image);
            var settings = new Settings(); var accounts = new TestApiAccountService(settings); var session = accounts.Session(new(settings.BaseAddress));
            var rounds = new FileRoundRepository(directory); var practice = Practice(new FileCoursePracticeRepository(directory), rounds);
            var ids = new List<Guid>(); var fail = true;
            using var client = new HttpClient(new Handler(async (request, token) =>
            {
                var body = JsonSerializer.Deserialize(await request.Content!.ReadAsStringAsync(token), CommunityJsonContext.Default.CreateCourseGameRequest)!;
                ids.Add(body.Id);
                if (fail) throw new HttpRequestException("Lost response");
                return Json(JsonSerializer.Serialize(new CourseGameInfo(body.Id, body.Name, "H7K2Q9", session.AccountId, session.Username,
                    false, CourseCommunityService.ScoringVersion, body.Package.Course, DateTimeOffset.UtcNow), CommunityJsonContext.Default.CourseGameInfo));
            }));
            var api = new CourseCommunityApi(client, accounts);
            var first = new CourseCommunityWorkflow(api, accounts, settings, practice, rounds, directory);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first.CreateAsync(session, "Game", Package().Course));
            fail = false;
            var restarted = new CourseCommunityWorkflow(api, accounts, settings, practice, rounds, directory);
            await restarted.CreateAsync(session, "Game", Package().Course);
            Assert.AreEqual(ids[0], ids[1]);
            await restarted.CreateAsync(session, "Game", Package().Course);
            Assert.AreNotEqual(ids[1], ids[2], "A deliberate new game after success gets a new id.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task LibraryDownloadPreservesEditedCopiesAndGameStartCannotReplaceOtherProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new Settings(); var accounts = new TestApiAccountService(settings); var session = accounts.Session(new(settings.BaseAddress));
            var rounds = new FileRoundRepository(directory); var practice = Practice(new FileCoursePracticeRepository(directory), rounds);
            using var client = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("No network needed")));
            var workflow = new CourseCommunityWorkflow(new CourseCommunityApi(client, accounts), accounts, settings, practice, rounds, directory);
            var downloaded = await workflow.SaveDownloadAsync(session, Package());
            await practice.SaveCourseAsync(downloaded with { Name = "My edit" });
            var repeated = await workflow.SaveDownloadAsync(session, Package());
            Assert.AreEqual("My edit", repeated.Name);
            var active = await practice.StartAsync(downloaded);
            var game = Game();
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => workflow.StartAsync(session, new(game, Guid.NewGuid().ToString("N"))));
            Assert.AreEqual(active.Id, (await practice.LoadAsync()).Active!.Id);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task StartingAndResumingGameFetchesBinaryPictureOnceAndRepairsCorruptCache()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new Settings(); var accounts = new TestApiAccountService(settings); var session = accounts.Session(new(settings.BaseAddress));
            var rounds = new FileRoundRepository(directory); var practice = Practice(new FileCoursePracticeRepository(directory), rounds);
            var game = Game(); var joined = new JoinedCourseGame(game, Guid.NewGuid().ToString("N"));
            var calls = 0;
            using var client = new HttpClient(new Handler((request, _) =>
            {
                Assert.AreEqual($"{settings.BaseAddress}api/community/games/{game.Id}/image", request.RequestUri!.AbsoluteUri);
                Assert.AreEqual("Bearer test-access-token", request.Headers.Authorization!.ToString());
                calls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Package().Image) });
            }));
            var workflow = new CourseCommunityWorkflow(new CourseCommunityApi(client, accounts), accounts, settings, practice, rounds, directory);
            var membership = new GamePlayer(session.AccountId, session.Username, joined.RoundId, false, null, null);
            var active = await workflow.PlayAsync(session, game, membership);
            await practice.SaveInputAsync(active.Id, new("0,5"), 0, "shot");
            var restarted = new CourseCommunityWorkflow(new CourseCommunityApi(client, accounts), accounts, settings, practice, rounds, directory);
            var resumed = await restarted.PlayAsync(session, game, membership);
            Assert.AreEqual("0,5", resumed.Input.Distance); Assert.AreEqual(1, calls);
            await File.WriteAllBytesAsync(Path.Combine(directory, "course-images", game.Course.ImageFile), new byte[8]);
            await restarted.PlayAsync(session, game, membership); Assert.AreEqual(2, calls);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task PictureWithWrongHashNeverStartsGame()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new Settings(); var accounts = new TestApiAccountService(settings); var session = accounts.Session(new(settings.BaseAddress));
            var rounds = new FileRoundRepository(directory); var practice = Practice(new FileCoursePracticeRepository(directory), rounds);
            using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[8]) })));
            var workflow = new CourseCommunityWorkflow(new CourseCommunityApi(client, accounts), accounts, settings, practice, rounds, directory);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => workflow.StartAsync(session, new(Game(), Guid.NewGuid().ToString("N"))));
            Assert.IsNull((await practice.LoadAsync()).Active);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static CourseGameInfo Game() => new(Guid.NewGuid(), "Game", "H7K2Q9", "host", "Host", false,
        CourseCommunityService.ScoringVersion, Package().Course with
        { ImageFile = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Package().Image)).ToLowerInvariant() + ".png" }, DateTimeOffset.UtcNow);

    private static CoursePracticeService Practice(ICoursePracticeRepository store, GolfSG.Application.Services.IRoundRepository rounds)
    {
        var putting = new StrokesGainedPuttingService(); var around = new StrokesGainedAroundGreenService(putting);
        return new(store, rounds, putting, new StrokesGainedApproachService(putting, around), around);
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Settings : IProductsApiSettings { public string BaseAddress { get; set; } = "https://localhost:7151/backend/"; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken); }
}
