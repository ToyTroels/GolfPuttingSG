using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GolfSG.Admin;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Admin.Tests;

[TestClass]
public sealed class PlayerPortalTests
{
    [TestMethod]
    public async Task PersonalLoginUsesSeparateSessionAndRejectsRevokedTokens()
    {
        await using var app = new PlayerSite();
        using var browser = app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var anonymous = await browser.GetAsync("/my");
        Assert.AreEqual(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains("/my/login", anonymous.Headers.Location!.OriginalString);
        using var missingCsrf = await browser.PostAsync("/my/login", new FormUrlEncodedContent([]));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        var page = await browser.GetStringAsync("/my/login");
        var csrf = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(csrf));
        using var login = await browser.PostAsync("/my/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(csrf), ["username"] = "alice", ["password"] = "TestOnly!12345" }));
        Assert.AreEqual("/my", login.Headers.Location?.OriginalString);
        Assert.DoesNotContain("player-secret-token", string.Join(";", login.Headers.GetValues("Set-Cookie")));
        var data = await browser.GetStringAsync("/my?section=rounds");
        Assert.Contains("Alice round", data);
        Assert.DoesNotContain("player-secret-token", data);
        Assert.DoesNotContain("TestOnly!12345", data);
        var performance = await browser.GetStringAsync("/my/performance");
        Assert.Contains("Your game, over time.", performance);
        Assert.Contains("Practice is excluded", performance);
        var validations = app.Api.IdentityReads;
        await browser.GetStringAsync("/my/performance?apply=1&range=30&lines=approach");
        Assert.AreEqual(1, app.Api.PerformanceReads);
        Assert.IsGreaterThan(validations, app.Api.IdentityReads);
        await browser.GetStringAsync("/my/performance?refresh=1");
        Assert.AreEqual(2, app.Api.PerformanceReads);
        var round = await browser.GetStringAsync("/my/rounds/alice-round");
        Assert.Contains("Hole-by-hole results", round);
        Assert.Contains("Hole 1", round);
        using var otherRound = await browser.GetAsync("/my/rounds/bob-round");
        Assert.AreEqual(HttpStatusCode.NotFound, otherRound.StatusCode);
        using var admin = await browser.GetAsync("/users");
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, admin.StatusCode);
        using var invalid = await browser.GetAsync("/my?section=users");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        // Picture setup uses the personal session, keeps publication private,
        // and requires an antiforgery token for writes.
        var editor = await browser.GetStringAsync("/my/setup");
        Assert.Contains("Your picture. Your shots.", editor);
        var setupCsrf = Regex.Match(editor, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var rejectedSave = await browser.PostAsync("/my/setup/course", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.AreEqual(HttpStatusCode.BadRequest, rejectedSave.StatusCode);
        using var save = new HttpRequestMessage(HttpMethod.Post, "/my/setup/course");
        save.Headers.Add("RequestVerificationToken", WebUtility.HtmlDecode(setupCsrf));
        save.Content = new StringContent("""{"package":{"course":{"name":"Private practice"},"image":"test"},"published":true}""", Encoding.UTF8, "application/json");
        using var saved = await browser.SendAsync(save);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        Assert.IsTrue(app.Api.SavedPrivate);
        using var game = new HttpRequestMessage(HttpMethod.Post, "/my/setup/game");
        game.Headers.Add("RequestVerificationToken", WebUtility.HtmlDecode(setupCsrf));
        game.Content = new StringContent("""{"id":"fixed-game-id","name":"Friends game","package":{}}""", Encoding.UTF8, "application/json");
        using var created = await browser.SendAsync(game);
        Assert.AreEqual(HttpStatusCode.OK, created.StatusCode);
        Assert.Contains("H7K2Q9", await created.Content.ReadAsStringAsync());
        var beforeWriteRefresh = app.Api.PerformanceReads;
        await browser.GetStringAsync("/my/performance");
        Assert.AreEqual(beforeWriteRefresh + 1, app.Api.PerformanceReads, "A successful setup write must invalidate cached personal data.");
        app.Api.Revoked = true;
        using var revoked = await browser.GetAsync("/my/performance");
        Assert.AreEqual(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.Contains("/my/login", revoked.Headers.Location!.OriginalString);
    }
    private sealed class PlayerSite : WebApplicationFactory<Program>
    {
        public PlayerApi Api { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Staging")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAuth:Enabled"] = "false", ["PlayerPortal:Enabled"] = "true",
                ["PlayerPortal:ApiBaseUrl"] = "https://api.example/", ["AllowedHosts"] = "localhost"
            })).ConfigureServices(services => {
                services.AddHttpClient("PlayerApi").ConfigurePrimaryHttpMessageHandler(() => Api);
                services.AddHttpClient("CourseSetupApi").ConfigurePrimaryHttpMessageHandler(() => Api);
            });
    }
    private sealed class PlayerApi : HttpMessageHandler
    {
        public bool Revoked { get; set; }
        public bool SavedPrivate { get; private set; }
        public int PerformanceReads { get; private set; }
        public int IdentityReads { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.AreEqual("api.example", request.RequestUri!.Host);
            if (request.RequestUri.AbsolutePath == "/api/auth/login")
                return Json("""{"accessToken":"player-secret-token","expiresIn":900}""");
            Assert.AreEqual("Bearer player-secret-token", request.Headers.Authorization?.ToString());
            if (request.RequestUri.AbsolutePath == "/api/community/courses" && request.Method == HttpMethod.Post)
            {
                using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                SavedPrivate = !body.RootElement.GetProperty("published").GetBoolean();
                return Json("""{"id":"saved-course"}""");
            }
            if (request.RequestUri.AbsolutePath == "/api/community/games" && request.Method == HttpMethod.Post)
                return Json("""{"id":"fixed-game-id","code":"H7K2Q9"}""");
            if (request.RequestUri.AbsolutePath == "/api/auth/me") IdentityReads++;
            if (request.RequestUri.AbsolutePath == "/api/me/performance") PerformanceReads++;
            if (Revoked) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (request.RequestUri.AbsolutePath == "/api/auth/me") return Json("""{"id":"alice","username":"alice"}""");
            if (request.RequestUri.AbsolutePath == "/api/me/performance") return Json("""{"window":30,"loaded":1,"unavailable":0,"normalRounds":1,"practiceRounds":0,"totalSg":0.5,"averageSg":0.5,"recent":[],"trend":[],"categories":[]}""");
            if (request.RequestUri.AbsolutePath == "/api/me/performance/rounds/bob-round") return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (request.RequestUri.AbsolutePath == "/api/me/performance/rounds/alice-round") return Json("""{"summary":{"id":"alice-round","title":"My golf round","date":"2026-10-17T00:00:00","practice":false,"kind":"Normal round","completed":1,"planned":9,"totalSg":0.5,"strokes":2},"rows":[{"number":1,"label":"Hole 1","description":"Putting 3 m","strokes":2,"penalties":0,"puttingSg":0.5,"totalSg":0.5}]}""");
            Assert.AreEqual("/api/me/data/rounds", request.RequestUri.AbsolutePath);
            Assert.DoesNotContain("accountId", request.RequestUri.Query);
            return Json("""{"section":"rounds","items":[{"id":"alice-round","title":"Alice round","state":"Synced","updatedAt":"2026-10-17T09:30:00Z","detail":"Own data"}],"offset":0,"hasMore":false}""");
        }
        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
