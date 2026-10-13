using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GolfSG.Application.Accounts;
using GolfSG.Application.Courses;

namespace GolfSG.Infrastructure.Courses;

public sealed class CourseCommunityApi(HttpClient client, IApiAccountService accounts) : ICourseCommunityApi
{
    public async Task<IReadOnlyList<LibraryCourse>> BrowseAsync(ApiSession session, string search = "", int offset = 0, bool mine = false, CancellationToken token = default) =>
        await ReadAsync(session, HttpMethod.Get, $"courses?search={Uri.EscapeDataString(search)}&offset={offset}&mine={mine.ToString().ToLowerInvariant()}",
            null, CommunityJsonContext.Default.ListLibraryCourse, token);
    public Task<LibraryCourse> PublishAsync(ApiSession session, CoursePackage package, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Post, "courses", JsonContent.Create(new PublishCourseRequest(package), CommunityJsonContext.Default.PublishCourseRequest), CommunityJsonContext.Default.LibraryCourse, token);
    public Task<LibraryCourse> SetVisibilityAsync(ApiSession session, Guid id, bool published, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Put, $"courses/{id}/visibility", JsonContent.Create(new CourseVisibilityRequest(published), CommunityJsonContext.Default.CourseVisibilityRequest), CommunityJsonContext.Default.LibraryCourse, token);
    public Task<CoursePackage> DownloadAsync(ApiSession session, Guid id, Guid revisionId, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Get, $"courses/{id}/revisions/{revisionId}", null, CommunityJsonContext.Default.CoursePackage, token);
    public Task<CourseGameInfo> CreateGameAsync(ApiSession session, CreateCourseGameRequest request, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Post, "games", JsonContent.Create(request, CommunityJsonContext.Default.CreateCourseGameRequest), CommunityJsonContext.Default.CourseGameInfo, token);
    public Task<CourseGameInfo> PreviewGameAsync(ApiSession session, string code, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Get, $"games/code/{Uri.EscapeDataString(CourseCommunityService.NormalizeCode(code))}", null, CommunityJsonContext.Default.CourseGameInfo, token);
    public Task<JoinedCourseGame> JoinGameAsync(ApiSession session, string code, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Post, $"games/code/{Uri.EscapeDataString(CourseCommunityService.NormalizeCode(code))}/join", null, CommunityJsonContext.Default.JoinedCourseGame, token);
    public Task<CourseGameDetails> GetGameAsync(ApiSession session, Guid id, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Get, $"games/{id}", null, CommunityJsonContext.Default.CourseGameDetails, token);
    public Task<CourseGamePlayerResults> GetPlayerResultsAsync(ApiSession session, Guid id, string accountId, int offset = 0, CancellationToken token = default) =>
        ReadAsync(session, HttpMethod.Get, $"games/{id}/players/{Uri.EscapeDataString(accountId)}/results?offset={offset}", null,
            CommunityJsonContext.Default.CourseGamePlayerResults, token);
    public Task<byte[]> GetGameImageAsync(ApiSession session, Guid id, CancellationToken token = default) =>
        SendAsync(session, HttpMethod.Get, $"games/{id}/image", null, token);
    public async Task<IReadOnlyList<MyCourseGame>> MyGamesAsync(ApiSession session, int offset = 0, CancellationToken token = default) =>
        await ReadAsync(session, HttpMethod.Get, $"games?offset={offset}", null, CommunityJsonContext.Default.ListMyCourseGame, token);
    public async Task SubmitAsync(ApiSession session, Guid id, SubmitCourseGameRequest request, CancellationToken token = default) =>
        _ = await SendAsync(session, HttpMethod.Put, $"games/{id}/submission", JsonContent.Create(request, CommunityJsonContext.Default.SubmitCourseGameRequest), token);
    public async Task CloseGameAsync(ApiSession session, Guid id, CancellationToken token = default) =>
        _ = await SendAsync(session, HttpMethod.Post, $"games/{id}/close", null, token);

    private async Task<T> ReadAsync<T>(ApiSession session, HttpMethod method, string path, HttpContent? content, JsonTypeInfo<T> type, CancellationToken token)
    {
        var bytes = await SendAsync(session, method, path, content, token);
        try { return JsonSerializer.Deserialize(bytes, type) ?? throw new JsonException(); }
        catch (JsonException) { throw new InvalidOperationException("Serverens banedata kunne ikke læses."); }
    }

    private async Task<byte[]> SendAsync(ApiSession pinned, HttpMethod method, string path, HttpContent? content, CancellationToken token)
    {
        using var message = new HttpRequestMessage(method, new Uri(pinned.Server, "api/community/" + path)) { Content = content };
        try
        {
            var current = await accounts.GetSessionAsync(pinned.Server, token);
            if (current.SessionId != pinned.SessionId || current.AccountId != pinned.AccountId || current.ServerAddress != pinned.ServerAddress)
                throw new ApiAccountException("Kontoen er ændret. Åbn siden igen.");
            await accounts.WithSessionAsync(pinned, () =>
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
                return Task.FromResult(true);
            }, token);
            using var response = await client.SendAsync(message, token);
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new ApiAccountException("Log ind igen via Konto og login.");
            if (!response.IsSuccessStatusCode)
            {
                var title = "Serveren kunne ikke udføre handlingen. Prøv igen senere.";
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Conflict)
                {
                    try
                    {
                        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                        if (problem.RootElement.TryGetProperty("title", out var value) && value.ValueKind == JsonValueKind.String)
                            title = value.GetString() ?? title;
                    }
                    catch (JsonException) { }
                }
                if ((int)response.StatusCode == 429) title = "For mange kodeforsøg. Vent et minut, og prøv igen.";
                throw new InvalidOperationException(title);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(token);
            return await accounts.WithSessionAsync(pinned, () => Task.FromResult(bytes), token);
        }
        catch (HttpRequestException) { throw new InvalidOperationException("Kan ikke kontakte serveren. Dine lokale data er bevaret."); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new InvalidOperationException("Serveren svarede ikke i tide. Prøv igen; dine lokale data er bevaret."); }
    }
}
