using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GolfSG.Api.Auth;
using GolfSG.Application.Courses;
using GolfSG.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GolfSG.Api.Tests;

[TestClass]
[TestCategory("PostgreSQL")]
public sealed class CourseCommunityHttpTests
{
    [TestMethod]
    public async Task AuthenticatedHttpFlowPublishesDownloadsJoinsSubmitsAndClosesWithoutSharingHistory()
    {
        var connection = PostgresStoreIntegrationTests.TestConnection();
        var marker = Guid.NewGuid().ToString("N");
        var keys = Path.Combine(Path.GetTempPath(), "golfsg-community-http-" + marker);
        var owners = new List<string>();
        var package = CourseCommunityTests.Package() with { Course = CourseCommunityTests.Package().Course with { Id = marker, Name = marker },
            Image = CourseCommunityTests.Package().Image.Concat(Guid.NewGuid().ToByteArray()).ToArray() };
        try
        {
            await using var factory = new TestApi(connection, keys);
            using var host = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
            using var player = factory.CreateClient(new() { BaseAddress = new("https://localhost") });
            using (var anonymous = await host.GetAsync("/api/community/courses")) Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            owners.Add(await RegisterAndLogin(host, "host_" + marker));
            owners.Add(await RegisterAndLogin(player, "player_" + marker));
            using var published = await host.PostAsJsonAsync("/api/community/courses", new { package, ownerId = owners[1] });
            var library = await Read(published, CommunityJsonContext.Default.LibraryCourse);
            Assert.AreEqual(owners[0], library.OwnerId);
            using var browse = await player.GetAsync("/api/community/courses?search=" + marker);
            Assert.HasCount(1, await Read(browse, CommunityJsonContext.Default.ListLibraryCourse));
            using var downloaded = await player.GetAsync($"/api/community/courses/{library.Id}/revisions/{library.RevisionId}");
            var copy = await Read(downloaded, CommunityJsonContext.Default.CoursePackage);
            CollectionAssert.AreEqual(package.Image, copy.Image);
            using (var forbidden = await player.PutAsJsonAsync($"/api/community/courses/{library.Id}/visibility", new CourseVisibilityRequest(false)))
                Assert.AreEqual(HttpStatusCode.NotFound, forbidden.StatusCode);
            using var created = await host.PostAsJsonAsync("/api/community/games", new CreateCourseGameRequest(Guid.NewGuid(), "Game", copy));
            var game = await Read(created, CommunityJsonContext.Default.CourseGameInfo);
            Assert.AreEqual(owners[0], game.HostId);
            using (var denied = await player.GetAsync($"/api/community/games/{game.Id}")) Assert.AreEqual(HttpStatusCode.NotFound, denied.StatusCode);
            using (var denied = await player.GetAsync($"/api/community/games/{game.Id}/image")) Assert.AreEqual(HttpStatusCode.NotFound, denied.StatusCode);
            using var joined = await player.PostAsync($"/api/community/games/code/{game.Code.ToLowerInvariant()}/join", null);
            var participation = await Read(joined, CommunityJsonContext.Default.JoinedCourseGame);
            using (var picture = await player.GetAsync($"/api/community/games/{game.Id}/image"))
            {
                Assert.AreEqual(HttpStatusCode.OK, picture.StatusCode);
                Assert.AreEqual("image/png", picture.Content.Headers.ContentType!.MediaType);
                CollectionAssert.AreEqual(package.Image, await picture.Content.ReadAsByteArrayAsync());
            }
            var outcomes = copy.Course.Shots.SelectMany(s => Enumerable.Range(0, s.Attempts)
                .Select(_ => new GameOutcome(s.Id, new(0, GolfSG.Core.Models.ShotLie.Holed, 0, true)))).ToArray();
            using (var incomplete = await player.PutAsJsonAsync($"/api/community/games/{game.Id}/submission", new SubmitCourseGameRequest(participation.RoundId, outcomes[..1])))
                Assert.AreEqual(HttpStatusCode.BadRequest, incomplete.StatusCode);
            using (var submitted = await player.PutAsJsonAsync($"/api/community/games/{game.Id}/submission", new { roundId = participation.RoundId, outcomes, totalStrokesGained = 9999, userId = owners[0] }))
                Assert.AreEqual(HttpStatusCode.NoContent, submitted.StatusCode);
            using var leaderboard = await host.GetAsync($"/api/community/games/{game.Id}");
            var details = await Read(leaderboard, CommunityJsonContext.Default.CourseGameDetails);
            using (var json = JsonDocument.Parse(await leaderboard.Content.ReadAsStringAsync()))
                Assert.IsFalse(json.RootElement.GetProperty("players")[0].TryGetProperty("results", out _));
            Assert.AreEqual(owners[1], details.Players.Single().AccountId);
            Assert.AreEqual(1, details.Players.Single().Rank);
            Assert.IsLessThan(10d, details.Players.Single().TotalStrokesGained!.Value);
            using (var resultsResponse = await player.GetAsync($"/api/community/games/{game.Id}/players/{owners[1]}/results?offset=0"))
            {
                var resultPage = await Read(resultsResponse, CommunityJsonContext.Default.CourseGamePlayerResults);
                Assert.HasCount(outcomes.Length, resultPage.Results);
                Assert.AreEqual(outcomes.Length, resultPage.Total);
            }
            using (var history = await player.GetAsync("/api/rounds"))
            { Assert.AreEqual(HttpStatusCode.OK, history.StatusCode); Assert.AreEqual("[]", await history.Content.ReadAsStringAsync()); }
            using (var denied = await player.PostAsync($"/api/community/games/{game.Id}/close", null)) Assert.AreEqual(HttpStatusCode.NotFound, denied.StatusCode);
            using (var closed = await host.PostAsync($"/api/community/games/{game.Id}/close", null)) Assert.AreEqual(HttpStatusCode.NoContent, closed.StatusCode);
            using (var retry = await player.PutAsJsonAsync($"/api/community/games/{game.Id}/submission", new SubmitCourseGameRequest(participation.RoundId, outcomes)))
                Assert.AreEqual(HttpStatusCode.NoContent, retry.StatusCode);
            using (var hidden = await host.PutAsJsonAsync($"/api/community/courses/{library.Id}/visibility", new CourseVisibilityRequest(false))) Assert.AreEqual(HttpStatusCode.OK, hidden.StatusCode);
            using (var stillThere = await player.GetAsync($"/api/community/games/{game.Id}")) Assert.AreEqual(HttpStatusCode.OK, stillThere.StatusCode);
        }
        finally
        {
            await using var cleanup = Context(connection); await using var tx = await cleanup.Database.BeginTransactionAsync();
            var games = cleanup.CourseGames.Where(g => owners.Contains(g.HostId)).Select(g => g.Id);
            await cleanup.GameParticipants.Where(p => games.Contains(p.GameId)).ExecuteDeleteAsync();
            await cleanup.CourseGames.Where(g => owners.Contains(g.HostId)).ExecuteDeleteAsync();
            var courses = cleanup.SharedCourses.Where(c => owners.Contains(c.OwnerId)).Select(c => c.Id);
            await cleanup.CourseRevisions.Where(r => courses.Contains(r.CourseId)).ExecuteDeleteAsync();
            await cleanup.SharedCourses.Where(c => owners.Contains(c.OwnerId)).ExecuteDeleteAsync();
            var imageId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(package.Image)).ToLowerInvariant();
            await cleanup.CourseImages.Where(i => i.Id == imageId).ExecuteDeleteAsync();
            await cleanup.Users.Where(u => owners.Contains(u.Id)).ExecuteDeleteAsync(); await tx.CommitAsync();
            if (Directory.Exists(keys)) Directory.Delete(keys, true);
        }
    }
    private static async Task<string> RegisterAndLogin(HttpClient client, string username)
    {
        const string password = "GolfTest!12345";
        using var registered = await client.PostAsJsonAsync("/api/auth/register", new { username, password });
        Assert.AreEqual(HttpStatusCode.OK, registered.StatusCode);
        var account = (await registered.Content.ReadFromJsonAsync<AccountIdentityDto>())!;
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.AreEqual(HttpStatusCode.OK, login.StatusCode);
        using var tokens = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.RootElement.GetProperty("accessToken").GetString());
        return account.Id;
    }
    private static async Task<T> Read<T>(HttpResponseMessage response, JsonTypeInfo<T> type)
    { Assert.AreEqual(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync(type))!; }
    private static AppDbContext Context(string connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    private sealed class TestApi(string connection, string keys) : WebApplicationFactory<GolfSG.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Default"] = connection, ["Auth:DataProtectionKeysPath"] = keys }));
    }
}
