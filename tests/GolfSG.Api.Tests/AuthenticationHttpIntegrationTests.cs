using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GolfSG.Api.Auth;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GolfSG.Api.Tests;

[TestClass]
[TestCategory("PostgreSQL")]
public sealed class AuthenticationHttpIntegrationTests
{
    [TestMethod]
    public async Task RealHttpAuthenticationRefreshAndAccountOwnershipProtectRoundHistory()
    {
        var connection = PostgresStoreIntegrationTests.TestConnection();
        var marker = Guid.NewGuid().ToString("N");
        var roundId = "http-" + marker;
        var legacyId = "legacy-" + marker;
        var keyPath = Path.Combine(Path.GetTempPath(), "golfsg-auth-tests-" + marker);
        var usernames = new[] { "alice_" + marker, "bob_" + marker };
        const string password = "GolfTest!12345";
        var ownerIds = new List<string>();
        try
        {
            await using var factory = new TestApi(connection, keyPath);
            using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions
                { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            await ExpectStatus(anonymous.GetAsync("/api/rounds"), HttpStatusCode.Unauthorized);
            await ExpectStatus(anonymous.GetAsync("/api/products"), HttpStatusCode.Unauthorized);
            await ExpectStatus(anonymous.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);

            foreach (var username in usernames)
            {
                using var registration = await anonymous.PostAsJsonAsync("/api/auth/register", new { username, password });
                Assert.AreEqual(HttpStatusCode.OK, registration.StatusCode);
                var account = await registration.Content.ReadFromJsonAsync<AccountIdentityDto>();
                Assert.IsNotNull(account);
                Assert.AreEqual(username, account.Username);
                ownerIds.Add(account.Id);
            }
            Assert.AreNotEqual(ownerIds[0], ownerIds[1]);
            await ExpectStatus(anonymous.PostAsJsonAsync("/api/auth/login",
                new { username = usernames[0], password = "Incorrect!12345" }), HttpStatusCode.Unauthorized);

            var aliceTokens = await Login(anonymous, usernames[0], password);
            var bobTokens = await Login(anonymous, usernames[1], password);
            using var alice = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            using var bob = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            alice.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceTokens.AccessToken);
            bob.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bobTokens.AccessToken);
            var identity = await alice.GetFromJsonAsync<AccountIdentityDto>("/api/auth/me");
            Assert.IsNotNull(identity);
            Assert.AreEqual(ownerIds[0], identity.Id);
            Assert.AreEqual(usernames[0], identity.Username);

            var round = ServerRoundServiceTests.ValidRound() with { Id = roundId };
            var aliceVersion = Guid.NewGuid();
            await ExpectStatus(alice.PutAsJsonAsync("/api/rounds/" + roundId,
                new RoundWriteRequest(aliceVersion, null, round)), HttpStatusCode.OK);
            await ExpectStatus(bob.GetAsync("/api/rounds/" + roundId), HttpStatusCode.NotFound);
            var bobBeforeCreate = await bob.GetFromJsonAsync<List<RemoteRound>>("/api/rounds");
            Assert.IsNotNull(bobBeforeCreate);
            Assert.IsEmpty(bobBeforeCreate);

            // Supplying a foreign owner and its version cannot bypass the authenticated account scope.
            await ExpectStatus(bob.PutAsJsonAsync("/api/rounds/" + roundId, new
            {
                userId = ownerIds[0], ownerId = ownerIds[0], version = Guid.NewGuid(),
                expectedVersion = aliceVersion, round = (object?)null
            }), HttpStatusCode.Conflict);
            var bobVersion = Guid.NewGuid();
            var bobRound = round with { EndedEarly = true };
            await ExpectStatus(bob.PutAsJsonAsync("/api/rounds/" + roundId,
                new RoundWriteRequest(bobVersion, null, bobRound)), HttpStatusCode.OK);
            Assert.IsFalse((await alice.GetFromJsonAsync<RemoteRound>("/api/rounds/" + roundId))!.Round!.EndedEarly);
            Assert.IsTrue((await bob.GetFromJsonAsync<RemoteRound>("/api/rounds/" + roundId))!.Round!.EndedEarly);

            var deletedVersion = Guid.NewGuid();
            await ExpectStatus(alice.PutAsJsonAsync("/api/rounds/" + roundId,
                new RoundWriteRequest(deletedVersion, aliceVersion, null)), HttpStatusCode.OK);
            Assert.IsNull((await alice.GetFromJsonAsync<RemoteRound>("/api/rounds/" + roundId))!.Round);
            var bobAfterDeletion = await bob.GetFromJsonAsync<RemoteRound>("/api/rounds/" + roundId);
            Assert.IsNotNull(bobAfterDeletion);
            Assert.AreEqual(bobVersion, bobAfterDeletion.Version);
            Assert.IsNotNull(bobAfterDeletion.Round);

            await using (var seed = Context(connection))
                Assert.IsNotNull(await new PostgresRoundStore(seed).WriteAsync("__legacy__", legacyId,
                    new(Guid.NewGuid(), null, round with { Id = legacyId })));
            await ExpectStatus(alice.GetAsync("/api/rounds/" + legacyId), HttpStatusCode.NotFound);
            await ExpectStatus(bob.GetAsync("/api/rounds/" + legacyId), HttpStatusCode.NotFound);

            using var refreshed = await anonymous.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = aliceTokens.RefreshToken });
            var refreshedTokens = await ReadTokens(refreshed);
            alice.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshedTokens.AccessToken);
            Assert.AreEqual(ownerIds[0], (await alice.GetFromJsonAsync<AccountIdentityDto>("/api/auth/me"))!.Id);
            await ExpectStatus(anonymous.PostAsJsonAsync("/api/auth/refresh",
                new { refreshToken = "invalid-token" }), HttpStatusCode.Unauthorized);
            anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-token");
            await ExpectStatus(anonymous.GetAsync("/api/rounds"), HttpStatusCode.Unauthorized);
        }
        finally
        {
            await using var cleanup = Context(connection);
            await cleanup.SavedRounds.Where(round => ownerIds.Contains(round.UserId) && round.Id == roundId ||
                round.UserId == "__legacy__" && round.Id == legacyId).ExecuteDeleteAsync();
            await cleanup.Users.Where(account => ownerIds.Contains(account.Id)).ExecuteDeleteAsync();
            if (Directory.Exists(keyPath)) Directory.Delete(keyPath, recursive: true);
        }
    }

    private static async Task<TokenPair> Login(HttpClient client, string username, string password)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        return await ReadTokens(response);
    }

    private static async Task<TokenPair> ReadTokens(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("Bearer", payload.RootElement.GetProperty("tokenType").GetString());
        Assert.IsGreaterThan(0L, payload.RootElement.GetProperty("expiresIn").GetInt64());
        var access = payload.RootElement.GetProperty("accessToken").GetString();
        var refresh = payload.RootElement.GetProperty("refreshToken").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(access));
        Assert.IsFalse(string.IsNullOrWhiteSpace(refresh));
        return new(access!, refresh!);
    }

    private static async Task ExpectStatus(Task<HttpResponseMessage> request, HttpStatusCode expected)
    {
        using var response = await request;
        Assert.AreEqual(expected, response.StatusCode);
    }

    private static AppDbContext Context(string connection) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private sealed record TokenPair(string AccessToken, string RefreshToken);

    private sealed class TestApi(string connection, string keyPath) : WebApplicationFactory<GolfSG.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connection,
                    ["Auth:DataProtectionKeysPath"] = keyPath
                }));
    }
}
