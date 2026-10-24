using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GolfSG.Application.Courses;
using GolfSG.Core.Models;
using GolfSG.Server.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresCourseCommunityStore(AppDbContext db) : ICourseCommunityStore
{
    public async Task<IReadOnlyList<LibraryCourse>> BrowseAsync(string userId, string search, int offset, bool mine, CancellationToken token)
    {
        var query = db.SharedCourses.AsNoTracking().Where(c => mine ? c.OwnerId == userId : c.Published);
        if (search.Length > 0) query = query.Where(c => c.Name.ToLower().Contains(search.ToLower()));
        return (await query.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id)
            .Skip(offset).Take(CourseCommunityService.PageSize).ToListAsync(token)).Select(ToLibrary).ToArray();
    }

    public async Task<LibraryCourse> PublishAsync(string userId, string username, CoursePackage package, CancellationToken token, bool published = true)
    {
        var (definition, imageId) = Normalize(package);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition)));
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var lockName = "course:" + userId + ":" + package.Course.Id;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockName}, 0))", token);
        var course = await db.SharedCourses.SingleOrDefaultAsync(c => c.OwnerId == userId && c.SourceId == package.Course.Id, token);
        if (course is null)
        {
            course = new SharedCourse { Id = Guid.NewGuid(), OwnerId = userId, Creator = username, SourceId = package.Course.Id };
            db.SharedCourses.Add(course);
            // The current-revision FK is deferred until the transaction commits.
        }
        var revision = await db.CourseRevisions.SingleOrDefaultAsync(r => r.CourseId == course.Id && r.ContentHash == hash, token);
        if (revision is null)
        {
            await EnsureImageAsync(imageId, package.Image, token);
            revision = new SharedCourseRevision { Id = Guid.NewGuid(), CourseId = course.Id, ContentHash = hash,
                DefinitionJson = definition, ImageId = imageId };
            course.RevisionId = revision.Id;
            course.Name = package.Course.Name; course.Positions = package.Course.Shots.Count;
            course.Attempts = package.Course.Shots.Sum(s => s.Attempts);
            course.Published = published; course.UpdatedAt = DateTimeOffset.UtcNow;
            // Persist a newly created parent before the revision's immediate FK is checked.
            await db.SaveChangesAsync(token);
            db.CourseRevisions.Add(revision);
        }
        course.RevisionId = revision.Id;
        course.Name = package.Course.Name; course.Positions = package.Course.Shots.Count;
        course.Attempts = package.Course.Shots.Sum(s => s.Attempts);
        course.Published = published; course.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return ToLibrary(course);
    }

    public async Task<LibraryCourse> SetVisibilityAsync(string userId, Guid id, bool published, CancellationToken token)
    {
        var course = await db.SharedCourses.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == userId, token) ?? throw Missing();
        course.Published = published; course.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token);
        return ToLibrary(course);
    }

    public async Task<CoursePackage> DownloadAsync(string userId, Guid id, Guid revisionId, CancellationToken token)
    {
        if (!await db.SharedCourses.AnyAsync(c => c.Id == id && (c.Published || c.OwnerId == userId), token)) throw Missing();
        var revision = await db.CourseRevisions.AsNoTracking().SingleOrDefaultAsync(r => r.CourseId == id && r.Id == revisionId, token) ?? throw Missing();
        return new(ReadCourse(revision.DefinitionJson), await ImageAsync(revision.ImageId, token));
    }

    public async Task<CourseGameInfo> CreateGameAsync(string userId, string username, CreateCourseGameRequest request, CancellationToken token)
    {
        var (definition, imageId) = Normalize(request.Package);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        // Serialize creation only, so code collision checks and retry identity are atomic.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(714052, 1)", token);
        var existing = await db.CourseGames.AsNoTracking().SingleOrDefaultAsync(g => g.Id == request.Id, token);
        if (existing is not null)
        {
            if (existing.HostId != userId || existing.Name != request.Name || existing.ImageId != imageId ||
                JsonSerializer.Serialize(ReadCourse(existing.DefinitionJson), CommunityJsonContext.Default.PracticeCourse) != definition)
                throw Conflict("Spillets id er allerede brugt til et andet spil.");
            await transaction.CommitAsync(token); return ToGame(existing);
        }
        string code;
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        do { code = new string(Enumerable.Range(0, 6).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray()); }
        while (await db.CourseGames.AnyAsync(g => g.Code == code, token));
        await EnsureImageAsync(imageId, request.Package.Image, token);
        var game = new SharedCourseGame { Id = request.Id, HostId = userId, HostName = username, Name = request.Name,
            Code = code, DefinitionJson = definition, ImageId = imageId, ScoringVersion = CourseCommunityService.ScoringVersion,
            CreatedAt = DateTimeOffset.UtcNow };
        db.CourseGames.Add(game); await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return ToGame(game);
    }

    public async Task<CourseGameInfo> PreviewGameAsync(string code, CancellationToken token) =>
        ToGame(await db.CourseGames.AsNoTracking().SingleOrDefaultAsync(g => g.Code == code, token) ?? throw Missing());

    public async Task<JoinedCourseGame> JoinGameAsync(string userId, string username, string code, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var game = await db.CourseGames.FromSqlInterpolated($"SELECT * FROM public.course_games WHERE code = {code} FOR UPDATE")
            .SingleOrDefaultAsync(token) ?? throw Missing();
        var roundId = await db.GameParticipants.Where(p => p.GameId == game.Id && p.AccountId == userId)
            .Select(p => p.RoundId).SingleOrDefaultAsync(token);
        if (roundId is null)
        {
            if (game.Closed) throw Conflict("Spillet er lukket for nye deltagere.");
            if (await db.GameParticipants.CountAsync(p => p.GameId == game.Id, token) >= 100)
                throw Conflict("Spillet har nået grænsen på 100 deltagere.");
            roundId = Guid.NewGuid().ToString("N");
            var participant = new SharedGameParticipant { GameId = game.Id, AccountId = userId, Name = username, RoundId = roundId };
            db.GameParticipants.Add(participant); await db.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
        return new(ToGame(game), roundId);
    }

    public async Task<CourseGameDetails> GetGameAsync(string userId, Guid id, CancellationToken token)
    {
        var game = await AuthorizedGames(userId).SingleOrDefaultAsync(g => g.Id == id, token) ?? throw Missing();
        var participants = await db.GameParticipants.AsNoTracking().Where(p => p.GameId == id)
            .Select(p => new GamePlayer(p.AccountId, p.Name, p.RoundId, p.ResultsJson != null, p.TotalStrokesGained, null))
            .ToListAsync(token);
        var ranked = participants.Where(p => p.TotalStrokesGained is not null)
            .OrderByDescending(p => Math.Round(p.TotalStrokesGained!.Value, 2, MidpointRounding.AwayFromZero))
            .ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.AccountId, StringComparer.Ordinal).ToArray();
        var ranks = new Dictionary<string, int>();
        double? last = null; var rank = 0;
        for (var i = 0; i < ranked.Length; i++)
        {
            var score = Math.Round(ranked[i].TotalStrokesGained!.Value, 2, MidpointRounding.AwayFromZero);
            if (last != score) rank = i + 1;
            ranks[ranked[i].AccountId] = rank; last = score;
        }
        var players = ranked.Concat(participants.Where(p => !p.Submitted).OrderBy(p => p.Name, StringComparer.Ordinal))
            .Select(p => p with { Rank = ranks.TryGetValue(p.AccountId, out var r) ? r : null }).ToArray();
        return new(ToGame(game), players);
    }

    public async Task<IReadOnlyList<MyCourseGame>> MyGamesAsync(string userId, int offset, CancellationToken token)
    {
        var query = from game in db.CourseGames.AsNoTracking()
                    join participant in db.GameParticipants.Where(p => p.AccountId == userId) on game.Id equals participant.GameId into memberships
                    from participant in memberships.DefaultIfEmpty()
                    where game.HostId == userId || participant != null
                    orderby game.CreatedAt descending, game.Id
                    select new MyCourseGame(new CourseGameSummary(game.Id, game.Name, game.Code, game.HostId, game.Closed, game.CreatedAt),
                        participant == null ? null : participant.RoundId, participant != null && participant.ResultsJson != null);
        return await query.Skip(offset).Take(CourseCommunityService.PageSize).ToListAsync(token);
    }

    public async Task<CourseGameSubmissionContext> GetSubmissionContextAsync(string userId, Guid id, CancellationToken token)
    {
        var context = await (from game in db.CourseGames.AsNoTracking()
                             join player in db.GameParticipants on game.Id equals player.GameId
                             where game.Id == id && player.AccountId == userId
                             select new { game.DefinitionJson, game.ScoringVersion, player.RoundId }).SingleOrDefaultAsync(token) ?? throw Missing();
        return new(ReadCourse(context.DefinitionJson), context.ScoringVersion, context.RoundId);
    }

    public async Task<CourseGameImage> GetGameImageAsync(string userId, Guid id, CancellationToken token) =>
        await (from game in AuthorizedGames(userId)
               join image in db.CourseImages.AsNoTracking() on game.ImageId equals image.Id
               where game.Id == id
               select new CourseGameImage(image.Id, image.Bytes)).SingleOrDefaultAsync(token) ?? throw Missing();

    public async Task<CourseGamePlayerResults> GetPlayerResultsAsync(string userId, Guid id, string accountId, int offset, CancellationToken token)
    {
        if (offset is < 0 or > 1000) throw new CourseCommunityException(CommunityError.Invalid, "Ugyldig side.");
        var path = FormattableString.Invariant($"$[{offset} to {offset + CourseCommunityService.ResultPageSize - 1}]");
        // Slice JSONB in PostgreSQL: only the requested player's page leaves the database.
        var page = await db.Database.SqlQuery<CourseResultSlice>($"""
            SELECT p.account_id AS "AccountId", p.name AS "Name", jsonb_array_length(p.results) AS "Total",
                   jsonb_path_query_array(p.results, CAST({path} AS jsonpath))::text AS "ResultsJson"
            FROM public.course_game_participants p
            JOIN public.course_games g ON g.id = p.game_id
            WHERE g.id = {id} AND p.account_id = {accountId} AND p.results IS NOT NULL
              AND (g.host_id = {userId} OR EXISTS (
                  SELECT 1 FROM public.course_game_participants viewer
                  WHERE viewer.game_id = g.id AND viewer.account_id = {userId}))
            """).SingleOrDefaultAsync(token) ?? throw Missing();
        return new(page.AccountId, page.Name, offset, page.Total,
            JsonSerializer.Deserialize(page.ResultsJson, CommunityJsonContext.Default.ListCourseShotResult)!);
    }

    private IQueryable<SharedCourseGame> AuthorizedGames(string userId) => db.CourseGames.AsNoTracking()
        .Where(g => g.HostId == userId || db.GameParticipants.Any(p => p.GameId == g.Id && p.AccountId == userId));

    public async Task SubmitAsync(string userId, Guid id, string roundId, IReadOnlyList<CourseShotResult> results, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var game = await LockGameAsync(id, token);
        var player = await db.GameParticipants.SingleOrDefaultAsync(p => p.GameId == id && p.AccountId == userId, token) ?? throw Missing();
        if (player.RoundId != roundId) throw Conflict("Resultatet tilhører ikke din deltagelse i spillet.");
        if (player.ResultsJson is not null)
        {
            var previous = JsonSerializer.Deserialize(player.ResultsJson, CommunityJsonContext.Default.ListCourseShotResult)!;
            if (!previous.SequenceEqual(results)) throw Conflict("Dit indsendte resultat er endeligt.");
        }
        else
        {
            if (game.Closed) throw Conflict("Spillet er lukket. Dit resultat er stadig gemt i din historik.");
            player.ResultsJson = JsonSerializer.Serialize(results.ToList(), CommunityJsonContext.Default.ListCourseShotResult);
            player.TotalStrokesGained = results.Sum(r => r.StrokesGained);
            await db.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task CloseGameAsync(string userId, Guid id, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var game = await LockGameAsync(id, token);
        if (game.HostId != userId) throw Missing();
        game.Closed = true; await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
    }

    private async Task<SharedCourseGame> LockGameAsync(Guid id, CancellationToken token) =>
        await db.CourseGames.FromSqlInterpolated($"SELECT * FROM public.course_games WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(token) ?? throw Missing();

    private async Task EnsureImageAsync(string id, byte[] image, CancellationToken token) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO public.course_images (id, bytes) VALUES ({id}, {image}) ON CONFLICT (id) DO NOTHING", token);
    private Task<byte[]> ImageAsync(string id, CancellationToken token) => db.CourseImages.AsNoTracking().Where(i => i.Id == id).Select(i => i.Bytes).SingleAsync(token);
    private static (string Definition, string ImageId) Normalize(CoursePackage package)
    {
        var extension = CourseCommunityService.ValidatePackage(package);
        var id = Convert.ToHexString(SHA256.HashData(package.Image)).ToLowerInvariant();
        var course = package.Course with { ImageFile = id + extension };
        return (JsonSerializer.Serialize(course, CommunityJsonContext.Default.PracticeCourse), id);
    }
    private static PracticeCourse ReadCourse(string json) => JsonSerializer.Deserialize(json, CommunityJsonContext.Default.PracticeCourse)!;
    private static LibraryCourse ToLibrary(SharedCourse c) => new(c.Id, c.RevisionId, c.OwnerId, c.Creator, c.Name, c.Positions, c.Attempts, c.Published, c.UpdatedAt);
    private static CourseGameInfo ToGame(SharedCourseGame g) => new(g.Id, g.Name, g.Code, g.HostId, g.HostName, g.Closed,
        g.ScoringVersion, ReadCourse(g.DefinitionJson), g.CreatedAt);
    private static CourseCommunityException Missing() => new(CommunityError.Missing, "Banen eller spillet findes ikke, eller du har ikke adgang.");
    private static CourseCommunityException Conflict(string message) => new(CommunityError.Conflict, message);
}

internal sealed class CourseResultSlice
{
    public CourseResultSlice() { }
    public string AccountId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Total { get; set; }
    public string ResultsJson { get; set; } = "";
}
