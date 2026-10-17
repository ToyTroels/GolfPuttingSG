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
    public async Task PersonalPerformanceKeepsOwnershipPracticeAndMalformedRecordsSeparate()
    {
        await using var db = Context(TestConnection());
        var marker = Guid.NewGuid().ToString("N");
        var alice = "performance-a-" + marker; var bob = "performance-b-" + marker;
        var normal = new GolfSG.Core.Models.Round("normal-" + marker, DateTime.Today,
            [GolfSG.Core.StrokesGainedCalculator.BuildHole(1, 3, 2)], ConfiguredHoleCount: 9);
        var practice = normal with { Id = "practice-" + marker, TrackingOptions = GolfSG.Core.Models.RoundTrackingOptions.PuttingGame };
        var other = normal with { Id = "other-" + marker };
        foreach (var (owner, round) in new[] { (alice, normal), (alice, practice), (bob, other) })
            db.SavedRounds.Add(new() { UserId = owner, Id = round.Id, Version = Guid.NewGuid(), UpdatedAt = DateTime.UtcNow,
                Payload = JsonSerializer.Serialize(round, RoundSyncJsonContext.Default.Round) });
        var badId = "bad-" + marker;
        db.SavedRounds.Add(new() { UserId = alice, Id = badId, Version = Guid.NewGuid(), UpdatedAt = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(new { id = badId }) });
        await db.SaveChangesAsync();
        try
        {
            var performance = new PostgresPersonalPerformance(db);
            var overview = await performance.OverviewAsync(alice, default);
            Assert.AreEqual(1, overview.NormalRounds);
            Assert.AreEqual(1, overview.PracticeRounds);
            Assert.AreEqual(1, overview.Unavailable);
            Assert.HasCount(1, overview.Trend);
            Assert.AreEqual(normal.Id, overview.Trend[0].Id);
            Assert.IsNull(await performance.DetailAsync(alice, other.Id, default));
            Assert.IsNull(await performance.DetailAsync(alice, badId, default));
            var detail = await performance.DetailAsync(alice, normal.Id, default);
            Assert.IsNotNull(detail);
            Assert.AreEqual(normal.Holes[0].StrokesGainedPutting, detail.Summary.TotalSg, 1e-10);
        }
        finally { await db.SavedRounds.Where(r => r.UserId == alice || r.UserId == bob).ExecuteDeleteAsync(); }
    }

    [TestMethod]
    public async Task PersonalCoursesAndGamesAreScopedToOwnerOrParticipationAndOwnScore()
    {
        await using var db = Context(TestConnection());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var marker = Guid.NewGuid().ToString("N");
        var alice = "own_a_" + marker; var bob = "own_b_" + marker;
        db.Users.AddRange(new() { Id = alice, UserName = alice }, new() { Id = bob, UserName = bob });
        var image = new GolfSG.Server.Infrastructure.Persistence.Models.SharedCourseImage { Id = marker, Bytes = new byte[8] };
        db.CourseImages.Add(image);
        foreach (var owner in new[] { alice, bob })
        {
            var courseId = Guid.NewGuid(); var revisionId = Guid.NewGuid();
            db.SharedCourses.Add(new() { Id = courseId, OwnerId = owner, Creator = owner, SourceId = marker,
                RevisionId = revisionId, Name = owner + " course", Positions = 1, Attempts = 1, Published = true, UpdatedAt = DateTimeOffset.UtcNow });
            db.CourseRevisions.Add(new() { Id = revisionId, CourseId = courseId, ContentHash = marker, DefinitionJson = "{}", ImageId = marker });
        }
        var joined = Guid.NewGuid(); var privateGame = Guid.NewGuid();
        var alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string Code() => new(System.Security.Cryptography.RandomNumberGenerator.GetBytes(6).Select(b => alphabet[b % alphabet.Length]).ToArray());
        foreach (var id in new[] { joined, privateGame }) db.CourseGames.Add(new() { Id = id, HostId = bob, HostName = bob,
            Name = id == joined ? "Joined game" : "Other account game", Code = Code(), DefinitionJson = "{}", ImageId = marker,
            ScoringVersion = "v1", CreatedAt = DateTimeOffset.UtcNow });
        db.GameParticipants.Add(new() { GameId = joined, AccountId = alice, Name = alice, RoundId = marker, ResultsJson = "[]", TotalStrokesGained = 1.5 });
        db.GameParticipants.Add(new() { GameId = joined, AccountId = bob, Name = bob, RoundId = Guid.NewGuid().ToString("N"), ResultsJson = "[]", TotalStrokesGained = 9.0 });
        await db.SaveChangesAsync();
        var data = new PostgresPersonalData(db);
        var courses = await data.ReadAsync(alice, "courses", 0, default);
        Assert.HasCount(1, courses.Items);
        Assert.AreEqual(alice + " course", courses.Items[0].Title);
        var games = await data.ReadAsync(alice, "games", 0, default);
        Assert.HasCount(1, games.Items);
        Assert.AreEqual("Joined game", games.Items[0].Title);
        Assert.AreEqual(1.5, games.Items[0].Score);
        var hosted = await data.ReadAsync(bob, "games", 0, default);
        Assert.HasCount(2, hosted.Items);
        await transaction.RollbackAsync();
    }

    [TestMethod]
    public async Task PersonalDataNeverReturnsAnotherAccountsRowsOrDeletedRounds()
    {
        await using var database = Context(TestConnection());
        var marker = Guid.NewGuid().ToString("N");
        var alice = "personal_alice_" + marker;
        var bob = "personal_bob_" + marker;
        database.SavedRounds.AddRange(
            new() { UserId = alice, Id = "alice-round", Version = Guid.NewGuid(), Payload = "{\"id\":\"alice-round\"}", UpdatedAt = DateTime.UtcNow },
            new() { UserId = bob, Id = "bob-round", Version = Guid.NewGuid(), Payload = "{\"id\":\"bob-round\"}", UpdatedAt = DateTime.UtcNow },
            new() { UserId = alice, Id = "deleted-round", Version = Guid.NewGuid(), Payload = null, UpdatedAt = DateTime.UtcNow });
        await database.SaveChangesAsync();
        try
        {
            var data = new PostgresPersonalData(database);
            var page = await data.ReadAsync(alice, "rounds", 0, default);
            Assert.HasCount(1, page.Items);
            Assert.AreEqual("alice-round", page.Items[0].Id);
            var bobPage = await data.ReadAsync(bob, "rounds", 0, default);
            Assert.HasCount(1, bobPage.Items);
            Assert.AreEqual("bob-round", bobPage.Items[0].Id);
            Assert.IsEmpty((await data.ReadAsync(alice, "courses", 0, default)).Items);
            Assert.IsEmpty((await data.ReadAsync(alice, "games", 0, default)).Items);
        }
        finally { await database.SavedRounds.Where(r => r.UserId == alice || r.UserId == bob).ExecuteDeleteAsync(); }
    }

    [TestMethod]
    public async Task AdminResetChangesPasswordAndSecurityStampButPreservesLockout()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Default"] = TestConnection() }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServerInfrastructure(config);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var account = new IdentityUser { UserName = "reset_" + Guid.NewGuid().ToString("N") };
        Assert.IsTrue((await users.CreateAsync(account, "Original!12345")).Succeeded);
        try
        {
            await users.SetLockoutEndDateAsync(account, DateTimeOffset.UtcNow.AddDays(1));
            var stamp = account.SecurityStamp;
            var lockout = account.LockoutEnd;
            var controller = new GolfSG.Api.Controllers.AdminPasswordResetController(users, config,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GolfSG.Api.Controllers.AdminPasswordResetController>.Instance)
                { ControllerContext = new() { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };
            Assert.IsInstanceOfType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await controller.Reset(account.Id,
                new() { ConfirmUsername = "wrong", NewPassword = "Replacement!12345" }));
            Assert.AreEqual(stamp, account.SecurityStamp);
            Assert.IsInstanceOfType<Microsoft.AspNetCore.Mvc.NoContentResult>(await controller.Reset(account.Id,
                new() { ConfirmUsername = account.UserName, NewPassword = "Replacement!12345" }));
            Assert.AreNotEqual(stamp, account.SecurityStamp);
            Assert.AreEqual(lockout, account.LockoutEnd);
            Assert.IsTrue(await users.CheckPasswordAsync(account, "Replacement!12345"));
            Assert.IsFalse(await users.CheckPasswordAsync(account, "Original!12345"));
        }
        finally { await users.DeleteAsync(account); }
    }

    [TestMethod]
    [DataRow("users")]
    [DataRow("courses")]
    [DataRow("games")]
    public async Task AdminDirectoryUsesBoundedReadOnlyProjections(string section)
    {
        await using var database = Context(TestConnection());
        var directory = new PostgresAdminDirectory(database);
        var page = await directory.ReadAsync(section, "", 0, 1, default);
        Assert.IsTrue(page.Items.Count <= 1);
        Assert.AreEqual(section, page.Section);
        var empty = await directory.ReadAsync(section, "nonexistent_" + Guid.NewGuid().ToString("N"), 0, 25, default);
        Assert.AreEqual(0, empty.Items.Count);
        Assert.IsFalse(empty.HasMore);
        Assert.IsFalse(database.ChangeTracker.HasChanges());
    }

    [TestMethod]
    public async Task AdminStatusReadsTheInstalledDatabaseRelease()
    {
        await using var database = Context(TestConnection());
        var result = await new PostgresAdminDatabaseStatus(database).ReadAsync(default);
        Assert.IsTrue(result.Available);
        Assert.AreEqual("0.2.0-alpha", result.Version);
    }

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
