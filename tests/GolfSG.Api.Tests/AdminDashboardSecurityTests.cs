using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using GolfSG.Api.Auth;
using GolfSG.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class AdminDashboardSecurityTests
{
    [TestMethod]
    public void ReadAccessRequiresPinnedIssuerAllowlistMfaAndDelegatedScope()
    {
        var config = Settings();
        Assert.IsTrue(AdminAccess.IsAllowed(User(), config, true));
        foreach (var missing in new[] { "iss", "sub", "amr", "scope" })
        {
            var user = User();
            var identity = (ClaimsIdentity)user.Identity!;
            identity.RemoveClaim(identity.FindFirst(missing)!);
            Assert.IsFalse(AdminAccess.IsAllowed(user, config, true), missing);
        }
        Assert.IsFalse(AdminAccess.IsAllowed(User(issuer: "https://other.example"), config, true));
        Assert.IsFalse(AdminAccess.IsAllowed(User(subject: "other"), config, true));
        Assert.IsFalse(AdminAccess.IsAllowed(User(mfa: "pwd"), config, true));
        Assert.IsFalse(AdminAccess.IsAllowed(User(scope: "admin.write"), config, true));
        Assert.IsFalse(AdminAccess.IsAllowed(User(authenticated: false), config, true));
        Assert.IsFalse(AdminAccess.IsAllowed(User(), new ConfigurationBuilder().Build(), true));
    }

    [TestMethod]
    public void EntraObjectIdAndAuthenticationContextAreSupported()
    {
        var config = Settings();
        config["AdminAuth:SubjectClaim"] = "oid";
        config["AdminAuth:MfaClaim"] = "acrs";
        config["AdminAuth:MfaValue"] = "c1";
        var user = User(subject: "pairwise-subject");
        ((ClaimsIdentity)user.Identity!).AddClaims([new("oid", "owner"), new("acrs", "c1")]);
        Assert.IsTrue(AdminAccess.IsAllowed(user, config, true));
        config["AdminAuth:Subjects:0"] = "removed";
        Assert.IsFalse(AdminAccess.IsAllowed(user, config, true));
    }

    [TestMethod]
    public void NewDashboardDisablesLegacyAdminPrivileges()
    {
        var config = Settings();
        config["Auth:SuperAdminAccountIds:0"] = "owner";
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "owner"),
            new Claim(SuperAdminAccess.AuthenticationClaim, "true")], "test"));
        Assert.IsFalse(SuperAdminAccess.IsAllowed(user, config));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("invalid-token")]
    public async Task StatusEndpointRejectsAnonymousAndInvalidBearerWithoutDatabaseAccess(string? token)
    {
        await using var factory = new DashboardApi();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (token is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        using var response = await client.GetAsync("/api/admin/status");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
    }

    [TestMethod]
    [DataRow("valid", HttpStatusCode.OK)]
    [DataRow("mfa-missing", HttpStatusCode.Forbidden)]
    [DataRow("scope-missing", HttpStatusCode.Forbidden)]
    [DataRow("unassigned", HttpStatusCode.Forbidden)]
    [DataRow("wrong-audience", HttpStatusCode.Unauthorized)]
    [DataRow("wrong-signature", HttpStatusCode.Unauthorized)]
    [DataRow("expired", HttpStatusCode.Unauthorized)]
    [DataRow("production", HttpStatusCode.Forbidden)]
    public async Task SignedJwtMustPassAuthenticationAndReadAuthorization(string scenario, HttpStatusCode expected)
    {
        await using var factory = new SignedDashboardApi(scenario == "production" ? "Production" : "Staging");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var claims = new List<Claim> { new("sub", scenario == "unassigned" ? "stranger" : "owner") };
        if (scenario != "mfa-missing") claims.Add(new("amr", "mfa"));
        if (scenario != "scope-missing") claims.Add(new("scope", "admin.read"));
        var expires = scenario == "expired" ? DateTime.UtcNow.AddMinutes(-2) : DateTime.UtcNow.AddMinutes(5);
        var key = scenario == "wrong-signature" ? new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(64)) : SignedDashboardApi.Key;
        var token = new JwtSecurityToken("https://identity.example", scenario == "wrong-audience" ? "another-api" : "admin-api",
            claims, DateTime.UtcNow.AddMinutes(-10), expires, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using var response = await client.GetAsync("/api/admin/status");
        Assert.AreEqual(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("0.2.0-alpha", body);
            Assert.Contains("Staging", body);
            Assert.DoesNotContain("access_token", body);
        }
    }

    private static IConfigurationRoot Settings() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["AdminAuth:Enabled"] = "true", ["AdminAuth:Issuer"] = "https://identity.example",
        ["AdminAuth:Subjects:0"] = "owner", ["AdminAuth:MfaClaim"] = "amr", ["AdminAuth:MfaValue"] = "mfa"
    }).Build();

    [TestMethod]
    public void PasswordResetRequiresSeparateScopeAllowlistAndRecentAuthentication()
    {
        var now = DateTimeOffset.UtcNow;
        var config = Settings();
        var principal = User(scope: "admin.read admin.users.password-reset");
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new("auth_time", now.ToUnixTimeSeconds().ToString()));
        Assert.IsFalse(AdminAccess.CanResetPasswords(principal, config, now));
        config["AdminAuth:PasswordResetSubjects:0"] = "owner";
        Assert.IsTrue(AdminAccess.CanResetPasswords(principal, config, now));
        Assert.IsFalse(AdminAccess.CanResetPasswords(principal, config, now.AddMinutes(6)));
        Assert.IsFalse(AdminAccess.CanResetPasswords(principal, config, now.AddMinutes(-2)));
        Assert.IsFalse(AdminAccess.CanResetPasswords(User(), config, now));
        identity.RemoveClaim(identity.FindFirst("amr")!);
        Assert.IsFalse(AdminAccess.CanResetPasswords(principal, config, now));
    }

    [TestMethod]
    [DataRow(false, true, HttpStatusCode.Forbidden)]
    [DataRow(true, false, HttpStatusCode.Forbidden)]
    [DataRow(true, true, HttpStatusCode.NoContent)]
    public async Task PasswordResetAccessChecksSignedScopeAndFreshness(bool writeScope, bool fresh, HttpStatusCode expected)
    {
        await using var factory = new SignedDashboardApi("Staging");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var claims = new List<Claim> { new("sub", "owner"), new("amr", "mfa"),
            new("scope", "admin.read" + (writeScope ? " admin.users.password-reset" : "")),
            new("auth_time", DateTimeOffset.UtcNow.AddMinutes(fresh ? 0 : -10).ToUnixTimeSeconds().ToString()) };
        var token = new JwtSecurityToken("https://identity.example", "admin-api", claims, DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5), new SigningCredentials(SignedDashboardApi.Key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using var response = await client.GetAsync("/api/admin/password-resets/access");
        Assert.AreEqual(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
        {
            using var denied = await client.PostAsJsonAsync("/api/admin/password-resets/missing", new { confirmUsername = "name", newPassword = "StrongReset!12345" });
            Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }

    [TestMethod]
    [DataRow("users", "", HttpStatusCode.OK)]
    [DataRow("courses", "?offset=-1", HttpStatusCode.BadRequest)]
    [DataRow("games", "?limit=101", HttpStatusCode.BadRequest)]
    [DataRow("unknown", "", HttpStatusCode.NotFound)]
    [DataRow("courses", "", HttpStatusCode.OK)]
    [DataRow("games", "", HttpStatusCode.OK)]
    public async Task DirectoryRequiresAuthorizedReadAndBoundsQueries(string section, string query, HttpStatusCode expected)
    {
        await using var factory = new SignedDashboardApi("Staging");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var denied = await client.GetAsync($"/api/admin/directory/{section}");
        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);
        var token = new JwtSecurityToken("https://identity.example", "admin-api",
            [new("sub", "owner"), new("amr", "mfa"), new("scope", "admin.read")], DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5), new SigningCredentials(SignedDashboardApi.Key, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using var response = await client.GetAsync($"/api/admin/directory/{section}{query}");
        Assert.AreEqual(expected, response.StatusCode);
        Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
    }

    private static ClaimsPrincipal User(string issuer = "https://identity.example", string subject = "owner",
        string mfa = "mfa", string scope = "openid admin.read", bool authenticated = true) => new(new ClaimsIdentity([
            new Claim("iss", issuer), new Claim("sub", subject), new Claim("amr", mfa), new Claim("scope", scope)
        ], authenticated ? "test" : null));

    private sealed class DashboardApi : WebApplicationFactory<GolfSG.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Staging")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused",
                ["Auth:DataProtectionKeysPath"] = "", ["AdminAuth:Enabled"] = "false"
            }));
    }

    private sealed class SignedDashboardApi(string environment) : WebApplicationFactory<GolfSG.Api.Program>
    {
        public static readonly SymmetricSecurityKey Key = new(RandomNumberGenerator.GetBytes(64));
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(environment)
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused",
                ["Auth:DataProtectionKeysPath"] = "", ["AdminAuth:Enabled"] = "true",
                ["AdminAuth:Authority"] = "https://identity.example", ["AdminAuth:Issuer"] = "https://identity.example",
                ["AdminAuth:Audience"] = "admin-api", ["AdminAuth:Subjects:0"] = "owner",
                ["AdminAuth:PasswordResetSubjects:0"] = "owner",
                ["AdminAuth:MfaClaim"] = "amr", ["AdminAuth:MfaValue"] = "mfa"
            })).ConfigureServices(services =>
            {
                services.RemoveAll<IAdminDatabaseStatus>();
                services.AddSingleton<IAdminDatabaseStatus, FakeDatabase>();
                services.RemoveAll<IAdminDirectory>();
                services.AddSingleton<IAdminDirectory, FakeDirectory>();
                services.PostConfigure<JwtBearerOptions>(AdminAccess.Scheme, options =>
                {
                    var metadata = new OpenIdConnectConfiguration { Issuer = "https://identity.example" };
                    metadata.SigningKeys.Add(Key);
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                });
            });
    }

    private sealed class FakeDatabase : IAdminDatabaseStatus
    {
        public Task<AdminDatabaseStatus> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(new AdminDatabaseStatus(true, "0.2.0-alpha"));
    }
    private sealed class FakeDirectory : IAdminDirectory
    {
        public Task<AdminDirectoryPage> ReadAsync(string section, string search, int offset, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(new AdminDirectoryPage(section, [new("sample-id", "Sample name", "Sample owner", "Active")], false, offset, limit, search));
    }
}
