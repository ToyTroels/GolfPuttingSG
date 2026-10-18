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
public sealed class PasswordAdminTests
{
    [TestMethod]
    public async Task PasswordLoginRequiresCsrfHidesTokensAndRechecksAccess()
    {
        await using var app = new Site();
        using var browser = app.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var anonymous = await browser.GetAsync("/users");
        Assert.Contains("/login", anonymous.Headers.Location!.OriginalString);
        using var missing = await browser.PostAsync("/login", new FormUrlEncodedContent([]));
        Assert.AreEqual(HttpStatusCode.Redirect, missing.StatusCode);
        Assert.AreEqual("/login?expired=1", missing.Headers.Location?.OriginalString);
        Assert.Contains("form expired", await browser.GetStringAsync("/login?expired=1"));
        var html = await browser.GetStringAsync("/login");
        var csrf = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(csrf));
        using var login = await browser.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(csrf), ["username"] = "owner", ["password"] = "TestOnly!12345" }));
        Assert.AreEqual("/", login.Headers.Location?.OriginalString);
        Assert.DoesNotContain("private-admin-token", string.Join(";", login.Headers.GetValues("Set-Cookie")));
        using var page = await browser.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        Assert.DoesNotContain("private-admin-token", await page.Content.ReadAsStringAsync());
        // Back restores the original anonymous form; both revisiting and
        // resubmitting it must keep the authenticated session usable.
        using var back = await browser.GetAsync("/login");
        Assert.AreEqual("/", back.Headers.Location?.OriginalString);
        using var stale = await browser.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(csrf), ["username"] = "owner", ["password"] = "TestOnly!12345" }));
        Assert.AreEqual("/", stale.Headers.Location?.OriginalString);
        var fresh = await browser.GetStringAsync("/login?reauth=1");
        Assert.Contains("/login?reauth=1", fresh);
        using var staleReauth = await browser.PostAsync("/login?reauth=1", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(csrf) }));
        Assert.AreEqual("/login?reauth=1&expired=1", staleReauth.Headers.Location?.OriginalString);
        var freshCsrf = Regex.Match(fresh, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var reauth = await browser.PostAsync("/login?reauth=1", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(freshCsrf), ["username"] = "owner", ["password"] = "TestOnly!12345" }));
        Assert.AreEqual("/", reauth.Headers.Location?.OriginalString);
        foreach (var path in Enumerable.Repeat(new[] { "/games", "/courses", "/users" }, 10).SelectMany(paths => paths))
        {
            using var directory = await browser.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.OK, directory.StatusCode, path);
            Assert.DoesNotContain("Please sign in again", await directory.Content.ReadAsStringAsync());
        }
        app.Api.Revoked = true;
        using var revoked = await browser.GetAsync("/users");
        Assert.AreEqual(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.Contains("/login", revoked.Headers.Location!.OriginalString);
    }
    private sealed class Site : WebApplicationFactory<Program>
    {
        public Api Api { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Staging")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAuth:Enabled"] = "true", ["AdminAuth:Mode"] = "Password",
                ["AdminAuth:Subjects:0"] = "owner", ["AdminApi:BaseUrl"] = "https://api.example/", ["AllowedHosts"] = "localhost"
            })).ConfigureServices(services => services.AddHttpClient("AdminApi").ConfigurePrimaryHttpMessageHandler(() => Api));
    }
    private sealed class Api : HttpMessageHandler
    {
        public bool Revoked { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/dashboard-login")
                return Task.FromResult(Json("""{"accessToken":"private-admin-token","expiresIn":900}"""));
            Assert.AreEqual("Bearer private-admin-token", request.Headers.Authorization?.ToString());
            if (Revoked) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            if (request.RequestUri.AbsolutePath.StartsWith("/api/admin/directory/"))
            {
                var section = request.RequestUri.AbsolutePath.Split('/').Last();
                return Task.FromResult(Json(System.Text.Json.JsonSerializer.Serialize(new { section, items = Array.Empty<object>(), hasMore = false, offset = 0, limit = 25, search = "" })));
            }
            return Task.FromResult(Json(request.RequestUri.AbsolutePath == "/api/admin/session"
                ? """{"id":"owner","username":"owner"}"""
                : """{"environment":"Staging","apiVersion":"test","databaseAvailable":true,"databaseVersion":"test","compatibility":"Unknown"}"""));
        }
        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
