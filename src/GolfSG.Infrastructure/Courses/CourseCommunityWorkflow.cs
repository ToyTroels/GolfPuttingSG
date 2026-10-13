using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GolfSG.Application.Accounts;
using GolfSG.Application.Courses;
using GolfSG.Application.Products;
using GolfSG.Application.Services;
using GolfSG.Core.Models;

namespace GolfSG.Infrastructure.Courses;

/// <summary>Local files remain the durable source for unfinished rounds and pending game results.</summary>
public sealed class CourseCommunityWorkflow(ICourseCommunityApi api, IApiAccountService accounts,
    IProductsApiSettings settings, CoursePracticeService practice, IRoundRepository rounds, string directory)
{
    private readonly SemaphoreSlim createGate = new(1, 1);
    private string Images => Path.Combine(directory, "course-images");
    public ICourseCommunityApi Api => api;
    public CoursePracticeService Practice => practice;

    public Task<ApiSession> SessionAsync(CancellationToken token = default)
    {
        if (!ProductsApiAddress.TryNormalize(settings.BaseAddress, out var server))
            throw new InvalidOperationException("Serverens adresse er ugyldig.");
        return accounts.GetSessionAsync(server!, token);
    }

    public async Task<CoursePackage> PackageAsync(PracticeCourse course, CancellationToken token = default)
    {
        CoursePracticeService.ValidateCourse(course);
        var path = Path.Combine(Images, course.ImageFile);
        if (new FileInfo(path).Length > CourseCommunityService.MaxImageBytes)
            throw new ArgumentException("Banebilledet må højst være 10 MB.");
        var package = new CoursePackage(course, await File.ReadAllBytesAsync(path, token));
        CourseCommunityService.ValidatePackage(package); return package;
    }

    public async Task<PracticeCourse> SaveDownloadAsync(ApiSession session, CoursePackage package, CancellationToken token = default)
    {
        var course = await SaveImageAsync(package, token);
        // Content identity preserves local edits and makes retrying a download safe.
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(course, CommunityJsonContext.Default.PracticeCourse))).ToLowerInvariant();
        course = course with { Id = "library-" + hash };
        return await accounts.WithSessionAsync(session, () => practice.ImportCourseAsync(course), token);
    }

    public async Task<CoursePracticeSession> StartAsync(ApiSession session, JoinedCourseGame joined, CancellationToken token = default)
    {
        if (joined.Game.Closed) throw new InvalidOperationException("Spillet er lukket.");
        if (joined.Game.ScoringVersion != CourseCommunityService.ScoringVersion)
            throw new InvalidOperationException("Opdater appen for at spille med denne referenceversion.");
        var course = joined.Game.Course;
        CoursePracticeService.ValidateCourse(course);
        var link = new CourseGameLink(joined.Game.Id.ToString("N"), session.AccountId, session.ServerAddress);
        var active = (await practice.LoadAsync()).Active;
        if (active is not null && (active.Id != joined.RoundId || active.Game != link))
            throw new InvalidOperationException("Fortsæt eller opgiv den igangværende træningsrunde først.");
        var expectedHash = Path.GetFileNameWithoutExtension(course.ImageFile);
        if (expectedHash.Length != 64 || expectedHash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidOperationException("Spillets billedreference er ugyldig.");
        var path = Path.Combine(Images, course.ImageFile);
        if (!await HasImageAsync(path, expectedHash, token))
        {
            var bytes = await api.GetGameImageAsync(session, joined.Game.Id, token);
            if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Banebilledet stemmer ikke med spillets opsætning.");
            course = await SaveImageAsync(new(course, bytes), token);
        }
        return await accounts.WithSessionAsync(session, () => practice.StartGameAsync(course, joined.RoundId, link), token);
    }

    public async Task<CoursePracticeSession> PlayAsync(ApiSession session, CourseGameInfo game, GamePlayer? membership, CancellationToken token = default)
    {
        if (membership is not null && membership.AccountId != session.AccountId)
            throw new ApiAccountException("Deltagelsen tilhører en anden konto.");
        if (membership?.Submitted == true) throw new InvalidOperationException("Dit resultat er allerede indsendt.");
        var joined = membership is null ? await api.JoinGameAsync(session, game.Code, token) : new JoinedCourseGame(game, membership.RoundId);
        return await StartAsync(session, joined, token);
    }

    public Task<Round?> FindRoundAsync(string roundId) => rounds.GetRoundAsync(roundId);

    public async Task<IReadOnlySet<string>> SavedRoundIdsAsync(IEnumerable<string> roundIds)
    {
        var requested = roundIds.ToHashSet(StringComparer.Ordinal);
        if (requested.Count == 0) return requested;
        var history = await rounds.GetRoundsAsync();
        return history.Where(r => requested.Contains(r.Id)).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<bool> HasImageAsync(string path, string expectedHash, CancellationToken token)
    {
        if (!File.Exists(path)) return false;
        try
        {
            await using var stream = File.OpenRead(path);
            if (stream.Length is < 8 or > CourseCommunityService.MaxImageBytes) return false;
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
    }

    public async Task SubmitAsync(ApiSession session, Round round, CancellationToken token = default)
    {
        var details = round.CoursePractice ?? throw new InvalidOperationException("Runden er ikke banetræning.");
        var game = details.Game ?? throw new InvalidOperationException("Runden er ikke knyttet til et spil.");
        if (game.AccountId != session.AccountId || game.ServerAddress != session.ServerAddress)
            throw new ApiAccountException("Log ind på den konto og server, der blev brugt til at tilmelde spillet.");
        var shots = details.PlayedShots;
        var outcomes = details.Results.Select((r, i) => new GameOutcome(shots[i].Id, r.Outcome)).ToArray();
        await api.SubmitAsync(session, Guid.Parse(game.GameId), new(round.Id, outcomes), token);
    }

    public async Task<CourseGameInfo> CreateAsync(ApiSession session, string name, PracticeCourse course, CancellationToken token = default)
    {
        var package = await PackageAsync(course, token);
        var imageHash = Convert.ToHexString(SHA256.HashData(package.Image));
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session.SyncScope + "|" + name + "|" + imageHash + "|" +
            JsonSerializer.Serialize(package.Course, CommunityJsonContext.Default.PracticeCourse))));
        await createGate.WaitAsync(token);
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "course-game-create.json");
            PendingGameCreation? pending = null;
            if (File.Exists(path)) pending = JsonSerializer.Deserialize(await File.ReadAllTextAsync(path, token), LocalCommunityJsonContext.Default.PendingGameCreation);
            if (pending?.Key != key)
            {
                pending = new(key, Guid.NewGuid());
                var snapshot = pending;
                await accounts.WithSessionAsync(session, async () =>
                {
                    await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(snapshot, LocalCommunityJsonContext.Default.PendingGameCreation), token);
                    File.Move(path + ".tmp", path, true); return true;
                }, token);
            }
            var game = await api.CreateGameAsync(session, new(pending.Id, name, package), token);
            await accounts.WithSessionAsync(session, () => { File.Delete(path); return Task.FromResult(true); }, token);
            return game;
        }
        finally { createGate.Release(); }
    }

    private async Task<PracticeCourse> SaveImageAsync(CoursePackage package, CancellationToken token)
    {
        var extension = CourseCommunityService.ValidatePackage(package);
        var hash = Convert.ToHexString(SHA256.HashData(package.Image)).ToLowerInvariant();
        var name = hash + extension;
        Directory.CreateDirectory(Images);
        var path = Path.Combine(Images, name); var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { await File.WriteAllBytesAsync(temp, package.Image, token); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return package.Course with { ImageFile = name };
    }
}

internal sealed record PendingGameCreation(string Key, Guid Id);
[JsonSerializable(typeof(PendingGameCreation))]
internal partial class LocalCommunityJsonContext : JsonSerializerContext;
