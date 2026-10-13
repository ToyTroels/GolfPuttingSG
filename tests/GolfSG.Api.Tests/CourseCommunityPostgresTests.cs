using System.Text.Json;
using GolfSG.Application.Courses;
using GolfSG.Core.Models;
using GolfSG.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Collections.Concurrent;
using System.Data.Common;

namespace GolfSG.Api.Tests;

[TestClass]
[TestCategory("PostgreSQL")]
public sealed class CourseCommunityPostgresTests
{
    [TestMethod]
    public async Task SummariesAndSubmissionUseBoundedQueriesWithoutLoadingOtherOutcomes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var game = await fixture.Use(s => s.CreateGameAsync(fixture.Host, "Host", new(Guid.NewGuid(), "Game", fixture.Package), default));
        var joined = await fixture.Use(s => s.JoinGameAsync(fixture.Player, "Player", game.Code, default));
        var outcomes = fixture.Package.Course.Shots.SelectMany(s => Enumerable.Range(0, s.Attempts)
            .Select(_ => new GameOutcome(s.Id, new CourseShotOutcome(0, ShotLie.Holed, 0, true)))).ToArray();
        fixture.Commands.Clear();
        await fixture.Submit(fixture.Player, game.Id, new(joined.RoundId, outcomes));
        Assert.HasCount(4, fixture.Commands, "Submission needs one small context query and three atomic save commands.");
        Assert.IsFalse(fixture.Commands.First().Contains("results", StringComparison.OrdinalIgnoreCase));

        fixture.Commands.Clear();
        var board = await fixture.Use(s => s.GetGameAsync(fixture.Player, game.Id, default));
        Assert.HasCount(2, fixture.Commands);
        Assert.Contains("results IS NOT NULL", fixture.Commands.Last());
        var json = JsonSerializer.Serialize(board, CommunityJsonContext.Default.CourseGameDetails);
        Assert.DoesNotContain("\"results\":", json);

        fixture.Commands.Clear();
        var mine = await fixture.Use(s => s.MyGamesAsync(fixture.Player, 0, default));
        Assert.HasCount(1, fixture.Commands); Assert.IsTrue(mine.Single().Submitted);
        Assert.Contains("results IS NOT NULL", fixture.Commands.Single());
        Assert.DoesNotContain("definition", fixture.Commands.Single());

        fixture.Commands.Clear();
        await fixture.Use(s => s.JoinGameAsync(fixture.Player, "Player", game.Code, default));
        Assert.HasCount(2, fixture.Commands);
        Assert.IsFalse(fixture.Commands.Any(sql => sql.Contains("results") || sql.Contains("bytes")));
    }

    [TestMethod]
    public async Task ResultPagesAndBinaryPicturesAreLimitedToGameMembersAndHost()
    {
        await using var fixture = await Fixture.CreateAsync();
        var package = fixture.Package with { Course = fixture.Package.Course with
            { Shots = fixture.Package.Course.Shots.Select((s, i) => i == 0 ? s with { Attempts = 100 } : s).ToArray() } };
        var game = await fixture.Use(s => s.CreateGameAsync(fixture.Host, "Host", new(Guid.NewGuid(), "Game", package), default));
        fixture.Commands.Clear();
        var joined = await fixture.Use(s => s.JoinGameAsync(fixture.Player, "Player", game.Code, default));
        Assert.HasCount(4, fixture.Commands);
        Assert.IsFalse(fixture.Commands.Any(sql => sql.Contains("bytes")));
        var outcomes = package.Course.Shots.SelectMany(s => Enumerable.Range(0, s.Attempts)
            .Select(_ => new GameOutcome(s.Id, new CourseShotOutcome(0, ShotLie.Holed, 0, true)))).ToArray();
        await fixture.Submit(fixture.Player, game.Id, new(joined.RoundId, outcomes));
        fixture.Commands.Clear();
        var first = await fixture.Use(s => s.GetPlayerResultsAsync(fixture.Host, game.Id, fixture.Player, 0, default));
        Assert.HasCount(1, fixture.Commands); Assert.HasCount(100, first.Results); Assert.AreEqual(101, first.Total);
        var second = await fixture.Use(s => s.GetPlayerResultsAsync(fixture.Player, game.Id, fixture.Player, 100, default));
        Assert.HasCount(1, second.Results); Assert.AreEqual("chip", second.Results.Single().ShotId);
        Assert.IsEmpty((await fixture.Use(s => s.GetPlayerResultsAsync(fixture.Player, game.Id, fixture.Player, 101, default))).Results);
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.GetPlayerResultsAsync(fixture.Other, game.Id, fixture.Player, 0, default)));
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.GetPlayerResultsAsync(fixture.Player, game.Id, fixture.Host, 0, default)));
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.GetPlayerResultsAsync(fixture.Player, game.Id, fixture.Player, -1, default)));
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.GetGameImageAsync(fixture.Other, game.Id, default)));
        fixture.Commands.Clear();
        var image = await fixture.Use(s => s.GetGameImageAsync(fixture.Player, game.Id, default));
        Assert.HasCount(1, fixture.Commands); CollectionAssert.AreEqual(package.Image, image.Bytes);
    }
    [TestMethod]
    public async Task LibraryRevisionsOwnershipImagesAndGamesPersistAcrossContexts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var package = fixture.Package;
        var first = await fixture.Use(s => s.PublishAsync(fixture.Host, "Host", package, default));
        var duplicate = await fixture.Use(s => s.PublishAsync(fixture.Host, "Host", package, default));
        Assert.AreEqual(first.RevisionId, duplicate.RevisionId);
        var newer = await fixture.Use(s => s.PublishAsync(fixture.Host, "Host", package with { Course = package.Course with { Name = "Updated", Shots = package.Course.Shots.Select(s => s with { Attempts = 3 }).ToArray() } }, default));
        Assert.AreEqual(first.Id, newer.Id); Assert.AreNotEqual(first.RevisionId, newer.RevisionId);
        var original = await fixture.Use(s => s.DownloadAsync(fixture.Player, first.Id, first.RevisionId, default));
        CollectionAssert.AreEqual(package.Image, original.Image);
        Assert.AreEqual(package.Course.Name, original.Course.Name);
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.SetVisibilityAsync(fixture.Player, first.Id, false, default)));

        var request = new CreateCourseGameRequest(Guid.NewGuid(), "Private game", original);
        var game = await fixture.Use(s => s.CreateGameAsync(fixture.Host, "Host", request, default));
        var retry = await fixture.Use(s => s.CreateGameAsync(fixture.Host, "Host", request, default));
        Assert.AreEqual(game.Code, retry.Code);
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.GetGameAsync(fixture.Player, game.Id, default)));
        await fixture.Use(s => s.SetVisibilityAsync(fixture.Host, first.Id, false, default));
        Assert.IsEmpty(await fixture.Use(s => s.BrowseAsync(fixture.Player, fixture.SourceId, 0, false, default)));
        var joined = await fixture.Use(s => s.JoinGameAsync(fixture.Player, "Player", game.Code, default));
        Assert.AreEqual(package.Course.Name, joined.Game.Course.Name);
        Assert.AreEqual(2, joined.Game.Course.Shots[0].Attempts);
        CollectionAssert.AreEqual(package.Image, (await fixture.Use(s => s.GetGameImageAsync(fixture.Player, game.Id, default))).Bytes);
        Assert.HasCount(1, await fixture.Use(s => s.MyGamesAsync(fixture.Player, 0, default)));
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.CloseGameAsync(fixture.Player, game.Id, default)));
    }

    [TestMethod]
    public async Task ConcurrentJoinsAndSubmissionsHaveOneMembershipAndOneFinalResult()
    {
        await using var fixture = await Fixture.CreateAsync();
        var game = await fixture.Use(s => s.CreateGameAsync(fixture.Host, "Host", new(Guid.NewGuid(), "Game", fixture.Package), default));
        var joins = await Task.WhenAll(fixture.Use(s => s.JoinGameAsync(fixture.Player, "Player", game.Code, default)),
            fixture.Use(s => s.JoinGameAsync(fixture.Player, "Player", game.Code, default)));
        Assert.AreEqual(joins[0].RoundId, joins[1].RoundId);
        await fixture.Use(s => s.JoinGameAsync(fixture.Host, "Host", game.Code, default));
        var outcomes = fixture.Package.Course.Shots.SelectMany(s => Enumerable.Range(0, s.Attempts)
            .Select(_ => new GameOutcome(s.Id, new CourseShotOutcome(0, ShotLie.Holed, 0, true)))).ToArray();
        var submission = new SubmitCourseGameRequest(joins[0].RoundId, outcomes);
        await Task.WhenAll(fixture.Submit(fixture.Player, game.Id, submission), fixture.Submit(fixture.Player, game.Id, submission));
        var host = (await fixture.Use(s => s.GetGameAsync(fixture.Host, game.Id, default))).Players.Single(p => p.AccountId == fixture.Host);
        await fixture.Submit(fixture.Host, game.Id, submission with { RoundId = host.RoundId });
        var board = await fixture.Use(s => s.GetGameAsync(fixture.Player, game.Id, default));
        Assert.HasCount(2, board.Players); Assert.IsTrue(board.Players.All(p => p.Rank == 1 && p.Submitted));
        await fixture.Use(s => s.CloseGameAsync(fixture.Host, game.Id, default));
        await fixture.Submit(fixture.Player, game.Id, submission); // Lost-response retries still succeed after closure.
        var changed = submission with { Outcomes = outcomes.Select((o, i) => i == 0 ? o with { Outcome = new(1, ShotLie.Green, 0, false) } : o).ToArray() };
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Submit(fixture.Player, game.Id, changed));
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.JoinGameAsync(fixture.Other, "Other", game.Code, default)));
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.GetGameAsync(fixture.Other, game.Id, default)));
    }

    [TestMethod]
    public async Task ClosedGameRejectsFirstSubmissionAndWrongParticipantRound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var game = await fixture.Use(s => s.CreateGameAsync(fixture.Host, "Host", new(Guid.NewGuid(), "Game", fixture.Package), default));
        var joined = await fixture.Use(s => s.JoinGameAsync(fixture.Player, "Player", game.Code, default));
        var results = fixture.Package.Course.Shots.SelectMany(s => Enumerable.Range(0, s.Attempts)
            .Select(_ => new CourseShotResult(new(0, ShotLie.Holed, 0, true), 1, 0, 0, s.Id))).ToArray();
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.SubmitAsync(fixture.Player, game.Id, Guid.NewGuid().ToString("N"), results, default)));
        await fixture.Use(s => s.CloseGameAsync(fixture.Host, game.Id, default));
        await Assert.ThrowsExactlyAsync<CourseCommunityException>(() => fixture.Use(s => s.SubmitAsync(fixture.Player, game.Id, joined.RoundId, results, default)));
        Assert.IsFalse((await fixture.Use(s => s.GetGameAsync(fixture.Host, game.Id, default))).Players.Single().Submitted);
    }

    private sealed class Fixture(string connection) : IAsyncDisposable
    {
        public string Host { get; } = Guid.NewGuid().ToString("N");
        public string Player { get; } = Guid.NewGuid().ToString("N");
        public string Other { get; } = Guid.NewGuid().ToString("N");
        public string SourceId { get; } = Guid.NewGuid().ToString("N");
        public CoursePackage Package { get; private set; } = null!;
        private readonly QueryCounter counter = new();
        public ConcurrentQueue<string> Commands => counter.Commands;
        private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).AddInterceptors(counter).Options);
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture(PostgresStoreIntegrationTests.TestConnection());
            var template = CourseCommunityTests.Package();
            fixture.Package = template with { Course = template.Course with { Id = fixture.SourceId, Name = fixture.SourceId }, Image = template.Image.Concat(Guid.NewGuid().ToByteArray()).ToArray() };
            await using var db = fixture.Context();
            foreach (var id in new[] { fixture.Host, fixture.Player, fixture.Other }) db.Users.Add(new IdentityUser { Id = id, UserName = id, NormalizedUserName = id });
            await db.SaveChangesAsync(); return fixture;
        }
        public async Task<T> Use<T>(Func<PostgresCourseCommunityStore, Task<T>> action)
        { await using var db = Context(); return await action(new(db)); }
        public async Task Use(Func<PostgresCourseCommunityStore, Task> action)
        { await using var db = Context(); await action(new(db)); }
        public Task Submit(string userId, Guid id, SubmitCourseGameRequest request) => Use(s => new CourseCommunityService(s).SubmitAsync(userId, id, request, default));
        public async ValueTask DisposeAsync()
        {
            await using var db = Context(); await using var transaction = await db.Database.BeginTransactionAsync();
            var games = db.CourseGames.Where(g => g.HostId == Host).Select(g => g.Id);
            await db.GameParticipants.Where(p => games.Contains(p.GameId)).ExecuteDeleteAsync();
            await db.CourseGames.Where(g => g.HostId == Host).ExecuteDeleteAsync();
            var courses = db.SharedCourses.Where(c => c.OwnerId == Host).Select(c => c.Id);
            await db.CourseRevisions.Where(r => courses.Contains(r.CourseId)).ExecuteDeleteAsync();
            await db.SharedCourses.Where(c => c.OwnerId == Host).ExecuteDeleteAsync();
            var imageId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Package.Image)).ToLowerInvariant();
            await db.CourseImages.Where(i => i.Id == imageId).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == Host || u.Id == Player || u.Id == Other).ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken token = default)
        { Commands.Enqueue(command.CommandText); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<int> result, CancellationToken token = default)
        { Commands.Enqueue(command.CommandText); return ValueTask.FromResult(result); }
    }
}
