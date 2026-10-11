using System.Text.Json;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Server.Infrastructure.Persistence;
using GolfSG.Server.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GolfSG.Api.Tests;

[TestClass]
[TestCategory("PostgreSQL")]
public sealed class PostgresStoreIntegrationTests
{
    [TestMethod]
    public async Task ReadinessRequiresReadableIdentitySchemaBeyondDatabaseConnectivity()
    {
        var connection = TestConnection();
        await using var ready = Context(connection);
        Assert.IsTrue(await new PostgresConnectionCheck(ready).IsReadyAsync(default));

        // The same database is reachable, but this session cannot resolve the Identity table.
        var withoutIdentity = new NpgsqlConnectionStringBuilder(connection) { SearchPath = "pg_catalog" };
        await using var missingSchema = Context(withoutIdentity.ConnectionString);
        Assert.IsFalse(await new PostgresConnectionCheck(missingSchema).IsReadyAsync(default));
    }

    [TestMethod]
    public async Task IdentityStoresPasswordHashAndUsernameAccountWithoutEmail()
    {
        var connection = TestConnection();
        var username = "test_" + Guid.NewGuid().ToString("N");
        const string password = "GolfTest!12345";
        var user = new IdentityUser { UserName = username };
        try
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connection }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServerInfrastructure(settings);
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            Assert.IsTrue((await users.CreateAsync(user, password)).Succeeded);
            Assert.IsNull(user.Email);
            var stored = await users.FindByNameAsync(username.ToUpperInvariant());
            Assert.IsNotNull(stored);
            Assert.AreEqual(user.Id, stored.Id);
            Assert.IsNotNull(stored.PasswordHash);
            Assert.AreNotEqual(password, stored.PasswordHash);
            Assert.IsTrue(await users.CheckPasswordAsync(stored, password));
            Assert.IsFalse(await users.CheckPasswordAsync(stored, "Incorrect!12345"));
            var duplicate = await users.CreateAsync(new IdentityUser { UserName = username.ToUpperInvariant() }, password);
            Assert.IsFalse(duplicate.Succeeded);
            Assert.IsTrue(duplicate.Errors.Any(error => error.Code == "DuplicateUserName"));
        }
        finally
        {
            await using var cleanup = Context(connection);
            await cleanup.Users.Where(account => account.Id == user.Id).ExecuteDeleteAsync();
        }
    }

    [TestMethod]
    public async Task ProductsPersistAcrossContextsAndSupportCompleteCrudFlow()
    {
        var connection = TestConnection();
        int? productId = null;
        var name = "Integration " + Guid.NewGuid().ToString("N");
        try
        {
            await using (var db = Context(connection))
            {
                var created = await new PostgresProductStore(db).CreateAsync(name);
                Assert.IsGreaterThan(0, created.Id);
                Assert.AreEqual(name, created.Name);
                productId = created.Id;
            }

            await using (var db = Context(connection))
            {
                var store = new PostgresProductStore(db);
                Assert.AreEqual(name, (await store.GetByIdAsync(productId.Value))!.Name);
                Assert.IsTrue((await store.GetAllAsync()).Any(product => product.Id == productId));
                Assert.IsTrue(await store.UpdateAsync(productId.Value, name + " updated"));
            }

            await using (var db = Context(connection))
            {
                var store = new PostgresProductStore(db);
                Assert.AreEqual(name + " updated", (await store.GetByIdAsync(productId.Value))!.Name);
                Assert.IsTrue(await store.DeleteAsync(productId.Value));
                Assert.IsNull(await store.GetByIdAsync(productId.Value));
                Assert.IsFalse(await store.UpdateAsync(productId.Value, "Missing"));
                Assert.IsFalse(await store.DeleteAsync(productId.Value));
            }
        }
        finally
        {
            if (productId is { } id)
            {
                await using var cleanup = Context(connection);
                await cleanup.Products.Where(product => product.Id == id).ExecuteDeleteAsync();
            }
        }
    }

    [TestMethod]
    public async Task RoundVersionsPreserveRetriesConflictsUpdatesAndRetainedDeletions()
    {
        var connection = TestConnection();
        var id = "test-" + Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid().ToString("N");
        var round = ServerRoundServiceTests.ValidRound() with { Id = id };
        var initialVersion = Guid.NewGuid();
        try
        {
            await using var db = Context(connection);
            var store = new PostgresRoundStore(db);
            Assert.IsNull(await store.GetByIdAsync(userId, id));
            Assert.IsNull(await store.WriteAsync(userId, id, new(Guid.NewGuid(), Guid.NewGuid(), round)),
                "An expected version must not create a missing round.");
            var initialRequest = new RoundWriteRequest(initialVersion, null, round);
            AssertRound(round, (await store.WriteAsync(userId, id, initialRequest))!);
            AssertRound(round, (await store.GetByIdAsync(userId, id))!);
            AssertRound(round, (await store.WriteAsync(userId, id, initialRequest))!);

            var changedRound = round with { EndedEarly = true };
            Assert.IsNull(await store.WriteAsync(userId, id, new(initialVersion, initialVersion, changedRound)),
                "The same version cannot be reused for a different payload.");
            Assert.IsNull(await store.WriteAsync(userId, id, new(Guid.NewGuid(), null, changedRound)),
                "Creating the same ID with a different version must conflict.");
            Assert.IsNull(await store.WriteAsync(userId, id, new(Guid.NewGuid(), Guid.NewGuid(), changedRound)),
                "A stale precondition must not overwrite history.");

            var updateVersion = Guid.NewGuid();
            var updateRequest = new RoundWriteRequest(updateVersion, initialVersion, changedRound);
            AssertRound(changedRound, (await store.WriteAsync(userId, id, updateRequest))!);
            AssertRound(changedRound, (await store.WriteAsync(userId, id, updateRequest))!);

            var deletionVersion = Guid.NewGuid();
            var deletionRequest = new RoundWriteRequest(deletionVersion, updateVersion, null);
            var deleted = await store.WriteAsync(userId, id, deletionRequest);
            Assert.IsNotNull(deleted);
            Assert.AreEqual(deletionVersion, deleted.Version);
            Assert.IsNull(deleted.Round);
            Assert.IsNotNull(await store.WriteAsync(userId, id, deletionRequest));
            Assert.IsNull((await store.GetByIdAsync(userId, id))!.Round);
            Assert.IsNull((await store.GetAllAsync(userId)).Single(remote => remote.Id == id).Round);
            Assert.IsNull(await store.WriteAsync(userId, id, new(Guid.NewGuid(), updateVersion, round)),
                "A stale device must not revive a retained deletion.");

            var restoredVersion = Guid.NewGuid();
            var restored = await store.WriteAsync(userId, id, new(restoredVersion, deletionVersion, round));
            Assert.IsNotNull(restored);
            Assert.AreEqual(restoredVersion, restored.Version);
            AssertRound(round, restored);
        }
        finally { await DeleteRound(connection, userId, id); }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ConcurrentCreationAndUpdatesAllowExactlyOneWinningVersion(bool firstCreation)
    {
        var connection = TestConnection();
        var id = "test-" + Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid().ToString("N");
        var round = ServerRoundServiceTests.ValidRound() with { Id = id };
        Guid? expected = firstCreation ? null : Guid.NewGuid();
        try
        {
            if (expected is { } initialVersion)
            {
                await using var seed = Context(connection);
                Assert.IsNotNull(await new PostgresRoundStore(seed).WriteAsync(userId, id, new(initialVersion, null, round)));
            }

            await using var firstDb = Context(connection);
            await using var secondDb = Context(connection);
            var firstRequest = new RoundWriteRequest(Guid.NewGuid(), expected, round);
            var secondRequest = new RoundWriteRequest(Guid.NewGuid(), expected, round with { EndedEarly = true });
            var writes = await Task.WhenAll(
                new PostgresRoundStore(firstDb).WriteAsync(userId, id, firstRequest),
                new PostgresRoundStore(secondDb).WriteAsync(userId, id, secondRequest));

            Assert.AreEqual(1, writes.Count(result => result is not null));
            var winner = writes.Single(result => result is not null)!;
            await using var verification = Context(connection);
            var stored = await new PostgresRoundStore(verification).GetByIdAsync(userId, id);
            Assert.IsNotNull(stored);
            Assert.AreEqual(winner.Version, stored.Version);
            Assert.AreEqual(Json(winner), Json(stored));
        }
        finally { await DeleteRound(connection, userId, id); }
    }

    [TestMethod]
    public async Task TwoAccountsKeepIdenticalRoundIdsPayloadsVersionsAndDeletionsIsolated()
    {
        var connection = TestConnection();
        var id = "test-" + Guid.NewGuid().ToString("N");
        var alice = Guid.NewGuid().ToString("N");
        var bob = Guid.NewGuid().ToString("N");
        var aliceRound = ServerRoundServiceTests.ValidRound() with { Id = id };
        var bobRound = aliceRound with { EndedEarly = true };
        var aliceVersion = Guid.NewGuid();
        var bobVersion = Guid.NewGuid();
        try
        {
            await using var db = Context(connection);
            var store = new PostgresRoundStore(db);
            Assert.IsNotNull(await store.WriteAsync(alice, id, new(aliceVersion, null, aliceRound)));
            Assert.IsNull(await store.GetByIdAsync(bob, id), "A guessed ID cannot read another account's round.");
            Assert.IsEmpty(await store.GetAllAsync(bob));
            Assert.IsNull(await store.WriteAsync(bob, id, new(Guid.NewGuid(), aliceVersion, null)),
                "Another account's version cannot delete its round or create an owned copy.");
            Assert.IsNotNull(await store.WriteAsync(bob, id, new(bobVersion, null, bobRound)),
                "Each account can create its own round with the same ID.");
            AssertRound(aliceRound, (await store.GetByIdAsync(alice, id))!);
            AssertRound(bobRound, (await store.GetByIdAsync(bob, id))!);
            AssertRound(aliceRound, (await store.GetAllAsync(alice)).Single());
            AssertRound(bobRound, (await store.GetAllAsync(bob)).Single());

            Assert.IsNull(await store.WriteAsync(bob, id, new(Guid.NewGuid(), aliceVersion, aliceRound)),
                "A known foreign version cannot bypass the owner's version precondition.");
            var deletionVersion = Guid.NewGuid();
            Assert.IsNotNull(await store.WriteAsync(alice, id, new(deletionVersion, aliceVersion, null)));
            Assert.IsNull((await store.GetAllAsync(alice)).Single().Round);
            Assert.AreEqual(deletionVersion, (await store.GetByIdAsync(alice, id))!.Version);
            Assert.AreEqual(bobVersion, (await store.GetByIdAsync(bob, id))!.Version);
            AssertRound(bobRound, (await store.GetAllAsync(bob)).Single());
            Assert.IsNull(await store.WriteAsync(bob, id, new(Guid.NewGuid(), deletionVersion, null)),
                "Foreign tombstones cannot delete an account's independent record.");
        }
        finally
        {
            await DeleteRound(connection, alice, id);
            await DeleteRound(connection, bob, id);
        }
    }

    [TestMethod]
    public async Task PreservedUnownedRowsAreInvisibleToNewAccounts()
    {
        var connection = TestConnection();
        var id = "test-" + Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid().ToString("N");
        var round = ServerRoundServiceTests.ValidRound() with { Id = id };
        try
        {
            await using var db = Context(connection);
            var store = new PostgresRoundStore(db);
            Assert.IsNotNull(await store.WriteAsync("__legacy__", id, new(Guid.NewGuid(), null, round)));

            Assert.IsNull(await store.GetByIdAsync(userId, id));
            Assert.IsEmpty(await store.GetAllAsync(userId));
            Assert.IsNotNull(await store.WriteAsync(userId, id, new(Guid.NewGuid(), null, round)),
                "Uploading local history creates an owned copy independently of preserved legacy data.");
        }
        finally
        {
            await DeleteRound(connection, "__legacy__", id);
            await DeleteRound(connection, userId, id);
        }
    }

    internal static string TestConnection()
    {
        var connection = Environment.GetEnvironmentVariable("GOLFSG_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connection))
            Assert.Inconclusive("Set GOLFSG_TEST_DATABASE to an explicitly disposable, prepared PostgreSQL database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Assert.IsNotNull(builder.Database);
        Assert.IsTrue(builder.Database.StartsWith("golfsg_test_", StringComparison.Ordinal),
            "Integration tests require a disposable database whose name starts with golfsg_test_.");
        return connection;
    }

    private static AppDbContext Context(string connection) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);

    private static async Task DeleteRound(string connection, string userId, string id)
    {
        await using var cleanup = Context(connection);
        await cleanup.SavedRounds.Where(round => round.UserId == userId && round.Id == id).ExecuteDeleteAsync();
    }

    private static void AssertRound(GolfSG.Core.Models.Round expected, RemoteRound actual)
    {
        Assert.AreEqual(expected.Id, actual.Id);
        Assert.AreEqual(JsonSerializer.Serialize(expected, RoundSyncJsonContext.Default.Round),
            JsonSerializer.Serialize(actual.Round, RoundSyncJsonContext.Default.Round));
    }

    private static string Json(RemoteRound remote) =>
        JsonSerializer.Serialize(remote, RoundSyncJsonContext.Default.RemoteRound);
}
