using System.Net;
using System.Security.Claims;
using GolfSG.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GolfSG.Admin.Tests;

[TestClass]
public sealed class AdminSessionTests
{
    [TestMethod]
    public async Task TicketStoreRemovesLoggedOutAndExpiredSessions()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new ServerTicketStore(cache);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(1) }, "Cookies");
        var first = await store.StoreAsync(ticket);
        var second = await store.StoreAsync(ticket);
        Assert.AreNotEqual(first, second);
        Assert.IsNotNull(await store.RetrieveAsync(first));
        await store.RemoveAsync(first);
        Assert.IsNull(await store.RetrieveAsync(first));
        ticket.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        await store.RenewAsync(second, ticket);
        Assert.IsNull(await store.RetrieveAsync(second));
    }

    [TestMethod]
    public async Task UnconfiguredDashboardFailsClosed()
    {
        await using var factory = new DisabledAdmin();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        foreach (var path in new[] { "/", "/signin-oidc", "/admin.css" })
        {
            using var response = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        }
    }

    [TestMethod]
    public async Task DashboardRendersStatusWithoutExposingTokenAndLogoutRequiresAntiforgery()
    {
        await using var factory = new AuthenticatedAdmin();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("System overview", html);
        Assert.Contains("0.2.0-alpha", html);
        Assert.Contains("Staging", html);
        Assert.DoesNotContain("secret-not-in-html", html);
        Assert.DoesNotContain("<script", html);
        Assert.AreEqual("Bearer secret-not-in-html", factory.Api.Authorization);
        using var missingToken = await client.PostAsync("/logout", new FormUrlEncodedContent([]));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingToken.StatusCode);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(token));
        using var signedOut = await client.PostAsync("/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token) }));
        Assert.AreEqual(HttpStatusCode.Redirect, signedOut.StatusCode);
        Assert.AreEqual("/signed-out", signedOut.Headers.Location?.OriginalString);
    }

    private sealed class AuthenticatedAdmin : WebApplicationFactory<Program>
    {
        public ApiStub Api { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Staging")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminAuth:Enabled"] = "true", ["AdminAuth:Authority"] = "https://identity.example",
                ["AdminAuth:Issuer"] = "https://identity.example", ["AdminAuth:ClientId"] = "test-client",
                ["AdminAuth:ClientSecret"] = "test-only", ["AdminAuth:ApiScope"] = "admin.read",
                ["AdminAuth:Subjects:0"] = "owner", ["AdminAuth:MfaClaim"] = "amr", ["AdminAuth:MfaValue"] = "mfa",
                ["AdminApi:BaseUrl"] = "https://api.example/", ["AllowedHosts"] = "localhost"
            })).ConfigureServices(services =>
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                });
                services.AddHttpClient("AdminApi").ConfigurePrimaryHttpMessageHandler(() => Api);
            });
    }

    [TestMethod]
    public async Task PasswordResetRequiresAntiforgeryAndProducesSingleUseConfirmation()
    {
        await using var factory = new AuthenticatedAdmin();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var missingCsrf = await client.PostAsync("/users/sample-id/reset-password/submit", new FormUrlEncodedContent([]));
        Assert.AreEqual(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        Assert.AreEqual(0, factory.Api.ResetRequests);
        var form = await client.GetStringAsync("/users/sample-id/reset-password");
        Assert.Contains("Reset user password", form);
        var token = Regex.Match(form, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(token));
        using var response = await client.PostAsync("/users/sample-id/reset-password/submit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token), ["confirmUsername"] = "sample-user",
            ["newPassword"] = "Replacement!12345", ["confirmPassword"] = "Replacement!12345", ["confirmed"] = "true"
        }));
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual(1, factory.Api.ResetRequests);
        Assert.Contains("receipt=", response.Headers.Location!.OriginalString);
        var completed = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("Password reset completed", completed);
        Assert.DoesNotContain("Replacement!12345", completed);
        var replay = await client.GetStringAsync(response.Headers.Location);
        Assert.DoesNotContain("Password reset completed", replay);
        Assert.Contains("confirmation has expired", replay);
    }

    [TestMethod]
    [DataRow("users")]
    [DataRow("courses")]
    [DataRow("games")]
    public async Task DirectoryRendersEncodedDataAndPaginationWithoutExposingAccessToken(string section)
    {
        await using var factory = new AuthenticatedAdmin();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync($"/{section}?search=sample&offset=25");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;sample&lt;/script&gt;", html);
        Assert.Contains("Previous", html);
        Assert.Contains("Next", html);
        Assert.Contains("View details", html);
        Assert.DoesNotContain("secret-not-in-html", html);
        Assert.DoesNotContain("<script>", html);
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "owner"), new Claim("iss", "https://identity.example"),
                new Claim("golfsg:api-mfa-verified", "true")], "Test"));
            var properties = new AuthenticationProperties();
            properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "secret-not-in-html" }]);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, "Test")));
        }
    }

    private sealed class ApiStub : HttpMessageHandler
    {
        public int ResetRequests { get; private set; }
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/admin/password-resets/access")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            if (request.RequestUri!.AbsolutePath == "/api/admin/password-resets/sample-id")
            {
                Assert.AreEqual(HttpMethod.Post, request.Method);
                Assert.AreEqual("Bearer secret-not-in-html", request.Headers.Authorization?.ToString());
                ResetRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
            if (request.RequestUri!.AbsolutePath.StartsWith("/api/admin/directory/", StringComparison.Ordinal))
            {
                Assert.AreEqual("api.example", request.RequestUri.Host);
                Assert.AreEqual("Bearer secret-not-in-html", request.Headers.Authorization?.ToString());
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"section":"users","items":[{"id":"sample-id","name":"<script>sample</script>","owner":"Sample owner","status":"Active"}],"hasMore":true,"offset":25,"limit":25,"search":"sample"}""",
                        System.Text.Encoding.UTF8, "application/json")
                });
            }
            Assert.AreEqual("https://api.example/api/admin/status", request.RequestUri!.AbsoluteUri);
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"environment":"Staging","apiVersion":"0.2.0-alpha","commit":null,"databaseAvailable":true,"databaseVersion":"0.2.0-alpha","compatibility":"Unknown"}""",
                    System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class DisabledAdmin : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Staging")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["AdminAuth:Enabled"] = "false" }));
    }
}
