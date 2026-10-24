using System.Text.Json.Serialization;
using GolfSG.Application.Accounts;
using GolfSG.Core.Models;

namespace GolfSG.Application.Courses;

public sealed record CoursePackage(PracticeCourse Course, byte[] Image);
public sealed record LibraryCourse(Guid Id, Guid RevisionId, string OwnerId, string Creator, string Name,
    int Positions, int Attempts, bool Published, DateTimeOffset UpdatedAt);
public sealed record PublishCourseRequest(CoursePackage Package, bool Published = true);
public sealed record CourseVisibilityRequest(bool Published);
public sealed record CreateCourseGameRequest(Guid Id, string Name, CoursePackage Package);
public sealed record CourseGameInfo(Guid Id, string Name, string Code, string HostId, string HostName,
    bool Closed, string ScoringVersion, PracticeCourse Course, DateTimeOffset CreatedAt);
public sealed record GameOutcome(string ShotId, CourseShotOutcome Outcome);
public sealed record SubmitCourseGameRequest(string RoundId, IReadOnlyList<GameOutcome> Outcomes);
public sealed record GamePlayer(string AccountId, string Name, string RoundId, bool Submitted,
    double? TotalStrokesGained, int? Rank);
public sealed record CourseGameDetails(CourseGameInfo Game, IReadOnlyList<GamePlayer> Players);
public sealed record JoinedCourseGame(CourseGameInfo Game, string RoundId);
public sealed record CourseGameSummary(Guid Id, string Name, string Code, string HostId, bool Closed, DateTimeOffset CreatedAt);
public sealed record MyCourseGame(CourseGameSummary Game, string? RoundId, bool Submitted);
public sealed record CourseGamePlayerResults(string AccountId, string Name, int Offset, int Total,
    IReadOnlyList<CourseShotResult> Results);
public sealed record CourseGameSubmissionContext(PracticeCourse Course, string ScoringVersion, string RoundId);
public sealed record CourseGameImage(string Id, byte[] Bytes);

public enum CommunityError { Invalid, Missing, Forbidden, Conflict }
public sealed class CourseCommunityException(CommunityError error, string message) : Exception(message)
{
    public CommunityError Error { get; } = error;
}

public interface ICourseCommunityStore
{
    Task<IReadOnlyList<LibraryCourse>> BrowseAsync(string userId, string search, int offset, bool mine, CancellationToken token);
    Task<LibraryCourse> PublishAsync(string userId, string username, CoursePackage package, CancellationToken token, bool published = true);
    Task<LibraryCourse> SetVisibilityAsync(string userId, Guid id, bool published, CancellationToken token);
    Task<CoursePackage> DownloadAsync(string userId, Guid id, Guid revisionId, CancellationToken token);
    Task<CourseGameInfo> CreateGameAsync(string userId, string username, CreateCourseGameRequest request, CancellationToken token);
    Task<CourseGameInfo> PreviewGameAsync(string code, CancellationToken token);
    Task<JoinedCourseGame> JoinGameAsync(string userId, string username, string code, CancellationToken token);
    Task<CourseGameDetails> GetGameAsync(string userId, Guid id, CancellationToken token);
    Task<CourseGamePlayerResults> GetPlayerResultsAsync(string userId, Guid id, string accountId, int offset, CancellationToken token);
    Task<CourseGameImage> GetGameImageAsync(string userId, Guid id, CancellationToken token);
    Task<CourseGameSubmissionContext> GetSubmissionContextAsync(string userId, Guid id, CancellationToken token);
    Task<IReadOnlyList<MyCourseGame>> MyGamesAsync(string userId, int offset, CancellationToken token);
    Task SubmitAsync(string userId, Guid id, string roundId, IReadOnlyList<CourseShotResult> results, CancellationToken token);
    Task CloseGameAsync(string userId, Guid id, CancellationToken token);
}

public interface ICourseCommunityApi
{
    Task<IReadOnlyList<LibraryCourse>> BrowseAsync(ApiSession session, string search = "", int offset = 0, bool mine = false, CancellationToken token = default);
    Task<LibraryCourse> PublishAsync(ApiSession session, CoursePackage package, CancellationToken token = default);
    Task<LibraryCourse> SetVisibilityAsync(ApiSession session, Guid id, bool published, CancellationToken token = default);
    Task<CoursePackage> DownloadAsync(ApiSession session, Guid id, Guid revisionId, CancellationToken token = default);
    Task<CourseGameInfo> CreateGameAsync(ApiSession session, CreateCourseGameRequest request, CancellationToken token = default);
    Task<CourseGameInfo> PreviewGameAsync(ApiSession session, string code, CancellationToken token = default);
    Task<JoinedCourseGame> JoinGameAsync(ApiSession session, string code, CancellationToken token = default);
    Task<CourseGameDetails> GetGameAsync(ApiSession session, Guid id, CancellationToken token = default);
    Task<CourseGamePlayerResults> GetPlayerResultsAsync(ApiSession session, Guid id, string accountId, int offset = 0, CancellationToken token = default);
    Task<byte[]> GetGameImageAsync(ApiSession session, Guid id, CancellationToken token = default);
    Task<IReadOnlyList<MyCourseGame>> MyGamesAsync(ApiSession session, int offset = 0, CancellationToken token = default);
    Task SubmitAsync(ApiSession session, Guid id, SubmitCourseGameRequest request, CancellationToken token = default);
    Task CloseGameAsync(ApiSession session, Guid id, CancellationToken token = default);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CoursePackage))]
[JsonSerializable(typeof(PublishCourseRequest))]
[JsonSerializable(typeof(CourseVisibilityRequest))]
[JsonSerializable(typeof(CreateCourseGameRequest))]
[JsonSerializable(typeof(CourseGameInfo))]
[JsonSerializable(typeof(CourseGameDetails))]
[JsonSerializable(typeof(JoinedCourseGame))]
[JsonSerializable(typeof(CourseGamePlayerResults))]
[JsonSerializable(typeof(List<LibraryCourse>))]
[JsonSerializable(typeof(LibraryCourse))]
[JsonSerializable(typeof(List<MyCourseGame>))]
[JsonSerializable(typeof(SubmitCourseGameRequest))]
[JsonSerializable(typeof(List<CourseShotResult>))]
[JsonSerializable(typeof(PracticeCourse))]
public partial class CommunityJsonContext : JsonSerializerContext;
