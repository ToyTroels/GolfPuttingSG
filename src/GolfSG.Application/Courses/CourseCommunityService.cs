using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.Courses;

/// <summary>Shared validation and versioned server scoring; persistence owns atomic membership/state changes.</summary>
public sealed class CourseCommunityService(ICourseCommunityStore store)
{
    public const string ScoringVersion = "sg-v1";
    public const int MaxImageBytes = 10 * 1024 * 1024;
    public const int PageSize = 30;
    public const int ResultPageSize = 100;

    public static string ValidatePackage(CoursePackage? package)
    {
        if (package is null) throw Invalid("Banen mangler.");
        try { CoursePracticeService.ValidateCourse(package.Course); }
        catch (ArgumentException ex) { throw Invalid(ex.Message); }
        if (package.Course.Id.Length > 100 || package.Course.Shots.Any(s => s.Id.Length > 100))
            throw Invalid("Banens eller slagets id er for langt.");
        if (package.Course.ScaleNote is null || package.Course.ScaleNote.Length > 500)
            throw Invalid("Målestokkens beskrivelse må højst være 500 tegn.");
        var image = package.Image;
        if (image is null || image.Length < 8 || image.Length > MaxImageBytes)
            throw Invalid("Banebilledet må højst være 10 MB.");
        if (image.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (image.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) return ".jpg";
        throw Invalid("Banebilledet skal være JPEG eller PNG.");
    }

    public static string NormalizeCode(string? code)
    {
        code = code?.Trim().ToUpperInvariant();
        if (code is null || code.Length != 6 || code.Any(c => !"ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(c)))
            throw Invalid("Angiv spillets kode på 6 tegn.");
        return code;
    }

    public Task<IReadOnlyList<LibraryCourse>> BrowseAsync(string userId, string search, int offset, bool mine, CancellationToken token)
    {
        if (search.Length > 100 || offset < 0 || offset > 100000) throw Invalid("Ugyldig søgning.");
        return store.BrowseAsync(userId, search.Trim(), offset, mine, token);
    }

    public Task<LibraryCourse> PublishAsync(string userId, string username, PublishCourseRequest request, CancellationToken token)
    {
        ValidatePackage(request.Package);
        return store.PublishAsync(userId, username, request.Package, token, request.Published);
    }

    public Task<CourseGameInfo> CreateGameAsync(string userId, string username, CreateCourseGameRequest request, CancellationToken token)
    {
        ValidatePackage(request.Package);
        if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
            throw Invalid("Giv spillet et navn på højst 100 tegn.");
        return store.CreateGameAsync(userId, username, request with { Name = request.Name.Trim() }, token);
    }

    public Task<CourseGameInfo> PreviewGameAsync(string code, CancellationToken token) => store.PreviewGameAsync(NormalizeCode(code), token);
    public Task<JoinedCourseGame> JoinGameAsync(string userId, string username, string code, CancellationToken token) =>
        store.JoinGameAsync(userId, username, NormalizeCode(code), token);

    public async Task SubmitAsync(string userId, Guid id, SubmitCourseGameRequest request, CancellationToken token)
    {
        if (request.Outcomes is null || request.Outcomes.Count is < 1 or > 1000)
            throw Invalid("Spil alle planlagte slag, før du indsender resultatet.");
        var context = await store.GetSubmissionContextAsync(userId, id, token);
        if (request.RoundId != context.RoundId)
            throw new CourseCommunityException(CommunityError.Conflict, "Resultatet tilhører ikke din deltagelse i spillet.");
        if (context.ScoringVersion != ScoringVersion)
            throw new CourseCommunityException(CommunityError.Conflict, "Spillets referenceversion understøttes ikke.");
        if (request.Outcomes.Count != context.Course.Shots.Sum(s => s.Attempts))
            throw Invalid("Spil alle planlagte slag, før du indsender resultatet.");
        var shots = context.Course.Shots.ToDictionary(s => s.Id);
        var putting = new StrokesGainedPuttingService();
        var around = new StrokesGainedAroundGreenService(putting);
        var scorer = new CoursePracticeScorer(putting, new StrokesGainedApproachService(putting, around), around);
        var results = new List<CourseShotResult>();
        foreach (var entry in request.Outcomes)
        {
            if (entry is null || entry.Outcome is null || entry.ShotId is null || !shots.TryGetValue(entry.ShotId, out var shot))
                throw Invalid("Resultatet henviser til et ukendt slag.");
            try { results.Add(scorer.Score(shot, context.Course, entry.Outcome)); }
            catch (ArgumentException ex) { throw Invalid(ex.Message); }
        }
        try { CoursePracticeService.ValidateResults(context.Course, results, requireComplete: true); }
        catch (ArgumentException ex) { throw Invalid(ex.Message); }
        // Store rechecks membership, the fixed round id and game closure under the game lock.
        await store.SubmitAsync(userId, id, request.RoundId, results, token);
    }

    private static CourseCommunityException Invalid(string message) => new(CommunityError.Invalid, message);
}
