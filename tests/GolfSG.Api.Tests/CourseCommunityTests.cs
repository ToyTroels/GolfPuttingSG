using System.Reflection;
using System.Security.Claims;
using GolfSG.Api.Controllers;
using GolfSG.Application.Courses;
using GolfSG.Core;
using GolfSG.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class CourseCommunityTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void MaximumLeaderboardSummaryDoesNotGrowWithShotResults()
    {
        var course = Package().Course with { Shots = Enumerable.Range(0, 100).Select(i => Package().Course.Shots[0] with { Id = "shot-" + i, Attempts = 10 }).ToArray() };
        var board = new CourseGameDetails(Game(course), Enumerable.Range(0, 100).Select(i =>
            new GamePlayer(Guid.NewGuid().ToString("N"), "Player " + i, Guid.NewGuid().ToString("N"), true, 0, 1)).ToArray());
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(board, CommunityJsonContext.Default.CourseGameDetails);
        Assert.IsLessThan(50_000, json.Length, "A representative maximum-size game's scoreboard must remain a small summary.");
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.IsFalse(document.RootElement.GetProperty("players")[0].TryGetProperty("results", out _));
        TestContext.WriteLine($"100-player, 1,000-attempt leaderboard summary: {json.Length} uncompressed bytes.");
    }
    [TestMethod]
    public async Task WebPrivateSaveDoesNotPublishAndExistingAppPublishKeepsItsDefault()
    {
        var store = new Store();
        var service = new CourseCommunityService(store);
        await service.PublishAsync("owner", "Owner", new PublishCourseRequest(Package(), false), default);
        Assert.IsFalse(store.Published!.Value);
        await service.PublishAsync("owner", "Owner", new PublishCourseRequest(Package()), default);
        Assert.IsTrue(store.Published!.Value);
    }

    internal static CoursePackage Package(int attempts = 2) => new(new("local-course", "Practice", "sample.png", 1, 100, "Measured",
        [new("putt", "Putting", new(.1, .1), new(.2, .2), ShotLie.Green, PracticeShotCategory.Putting, 3, attempts),
         new("chip", "Chip", new(.3, .3), new(.4, .4), ShotLie.Rough, PracticeShotCategory.AroundGreen, 12)]),
        [137, 80, 78, 71, 13, 10, 26, 10]);

    internal static CourseGameInfo Game(PracticeCourse? course = null) => new(Guid.NewGuid(), "Game", "H7K2Q9", "host", "Host",
        false, CourseCommunityService.ScoringVersion, course ?? Package().Course, DateTimeOffset.UtcNow);

    [TestMethod]
    public async Task ServerScoresMixedCategoriesAndPenaltiesFromFrozenSetupInActualPlayOrder()
    {
        var store = new Store(); var service = new CourseCommunityService(store);
        GameOutcome[] outcomes = [new("chip", new(1, ShotLie.Green, 1, false)),
            new("putt", new(0, ShotLie.Holed, 0, true)), new("putt", new(.5, ShotLie.Green, 0, false))];
        using var cancellation = new CancellationTokenSource();
        await service.SubmitAsync("player", store.Details.Game.Id, new("round", outcomes), cancellation.Token);
        Assert.AreEqual("player", store.LastUserId); Assert.AreEqual(cancellation.Token, store.Token);
        Assert.HasCount(3, store.Results!);
        Assert.IsNotNull(store.Results);
        var putting = new StrokesGainedPuttingService(); var around = new StrokesGainedAroundGreenService(putting);
        var scorer = new CoursePracticeScorer(putting, new StrokesGainedApproachService(putting, around), around);
        for (var i = 0; i < outcomes.Length; i++)
        {
            var shot = store.Details.Game.Course.Shots.Single(s => s.Id == outcomes[i].ShotId);
            Assert.AreEqual(scorer.Score(shot, store.Details.Game.Course, outcomes[i].Outcome), store.Results[i]);
        }
        Assert.AreEqual(0, store.Results[1].ExpectedFinish);
    }

    [TestMethod]
    [DataRow("quota")]
    [DataRow("unknown")]
    [DataRow("negative")]
    [DataRow("version")]
    public async Task InvalidSubmissionNeverReachesWrite(string defect)
    {
        var store = new Store(); var service = new CourseCommunityService(store);
        GameOutcome[] outcomes = [new("putt", new(0, ShotLie.Holed, 0, true)),
            new("putt", new(0, ShotLie.Holed, 0, true)), new("chip", new(0, ShotLie.Holed, 0, true))];
        if (defect == "quota") outcomes[2] = outcomes[0];
        if (defect == "unknown") outcomes[0] = outcomes[0] with { ShotId = "other" };
        if (defect == "negative") outcomes[0] = outcomes[0] with { Outcome = new(-1, ShotLie.Green, 0, false) };
        if (defect == "version") store.Details = store.Details with { Game = store.Details.Game with { ScoringVersion = "future" } };
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => service.SubmitAsync("player", store.Details.Game.Id, new("round", outcomes), default));
        Assert.IsNull(store.Results);
    }

    [TestMethod]
    public void ImageLimitsAndCodeNormalizationRejectInvalidInput()
    {
        Assert.AreEqual("H7K2Q9", CourseCommunityService.NormalizeCode(" h7k2q9 "));
        Assert.ThrowsExactly<CourseCommunityException>(() => CourseCommunityService.NormalizeCode("abcdef/"));
        Assert.ThrowsExactly<CourseCommunityException>(() => CourseCommunityService.ValidatePackage(Package() with { Image = new byte[8] }));
        Assert.ThrowsExactly<CourseCommunityException>(() => CourseCommunityService.ValidatePackage(Package() with { Image = new byte[CourseCommunityService.MaxImageBytes + 1] }));
        Assert.ThrowsExactly<CourseCommunityException>(() => CourseCommunityService.ValidatePackage(Package() with { Course = Package().Course with { Id = new string('x', 101) } }));
    }

    [TestMethod]
    public async Task ControllerUsesAuthenticatedIdentityAndMapsRejectedSubmission()
    {
        var store = new Store(); var controller = new CourseCommunityController(new(store), store)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "real-user"), new("user_id", "spoof"), new(ClaimTypes.Name, "Real")], "test")) } }
        };
        Assert.IsInstanceOfType<OkObjectResult>(await controller.Get(store.Details.Game.Id, default));
        Assert.AreEqual("real-user", store.LastUserId);
        var invalid = await controller.Submit(store.Details.Game.Id, new("round", []), default);
        Assert.AreEqual(400, ((ObjectResult)invalid).StatusCode);
        controller.HttpContext.User = new ClaimsPrincipal();
        Assert.IsInstanceOfType<UnauthorizedResult>(await controller.Get(store.Details.Game.Id, default));
    }

    [TestMethod]
    public void CommunityEndpointsRequireBearerAndLimitCodeGuessingAndPictureUploads()
    {
        var type = typeof(CourseCommunityController);
        Assert.AreEqual("Identity.Bearer", type.GetCustomAttribute<AuthorizeAttribute>()!.AuthenticationSchemes);
        foreach (var name in new[] { "Preview", "Join" })
            Assert.AreEqual("game-codes", type.GetMethod(name)!.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        foreach (var name in new[] { "Publish", "Create" })
            Assert.AreEqual(15L * 1024 * 1024, ((IRequestSizeLimitMetadata)type.GetMethod(name)!.GetCustomAttribute<RequestSizeLimitAttribute>()!).MaxRequestBodySize);
    }

    private sealed class Store : ICourseCommunityStore
    {
        public CourseGameDetails Details { get; set; } = new(Game(), []);
        public string? LastUserId;
        public CancellationToken Token;
        public IReadOnlyList<CourseShotResult>? Results;
        public Task<CourseGameDetails> GetGameAsync(string userId, Guid id, CancellationToken token)
        { LastUserId = userId; return Task.FromResult(Details); }
        public Task<CourseGameSubmissionContext> GetSubmissionContextAsync(string userId, Guid id, CancellationToken token)
        { LastUserId = userId; return Task.FromResult(new CourseGameSubmissionContext(Details.Game.Course, Details.Game.ScoringVersion, "round")); }
        public Task<CourseGameImage> GetGameImageAsync(string userId, Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<CourseGamePlayerResults> GetPlayerResultsAsync(string userId, Guid id, string accountId, int offset, CancellationToken token) => throw new NotSupportedException();
        public Task SubmitAsync(string userId, Guid id, string roundId, IReadOnlyList<CourseShotResult> results, CancellationToken token)
        { LastUserId = userId; Token = token; Results = results; return Task.CompletedTask; }
        public Task<IReadOnlyList<LibraryCourse>> BrowseAsync(string userId, string search, int offset, bool mine, CancellationToken token) => throw new NotSupportedException();
        public bool? Published { get; private set; }
        public Task<LibraryCourse> PublishAsync(string userId, string username, CoursePackage package, CancellationToken token, bool published = true)
        {
            Published = published;
            return Task.FromResult(new LibraryCourse(Guid.NewGuid(), Guid.NewGuid(), userId, username, package.Course.Name,
                package.Course.Shots.Count, 3, published, DateTimeOffset.UtcNow));
        }
        public Task<LibraryCourse> SetVisibilityAsync(string userId, Guid id, bool published, CancellationToken token) => throw new NotSupportedException();
        public Task<CoursePackage> DownloadAsync(string userId, Guid id, Guid revisionId, CancellationToken token) => throw new NotSupportedException();
        public Task<CourseGameInfo> CreateGameAsync(string userId, string username, CreateCourseGameRequest request, CancellationToken token) => throw new NotSupportedException();
        public Task<CourseGameInfo> PreviewGameAsync(string code, CancellationToken token) => throw new NotSupportedException();
        public Task<JoinedCourseGame> JoinGameAsync(string userId, string username, string code, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<MyCourseGame>> MyGamesAsync(string userId, int offset, CancellationToken token) => throw new NotSupportedException();
        public Task CloseGameAsync(string userId, Guid id, CancellationToken token) => throw new NotSupportedException();
    }
}
