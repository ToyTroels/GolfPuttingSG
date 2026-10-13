using System.Security.Claims;
using GolfSG.Application.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
[Route("api/community")]
public sealed class CourseCommunityController(CourseCommunityService service, ICourseCommunityStore store) : ControllerBase
{
    [HttpGet("courses")]
    public Task<IActionResult> Browse(string search = "", int offset = 0, bool mine = false, CancellationToken token = default) =>
        Run(async (id, _) => await service.BrowseAsync(id, search, offset, mine, token));

    [HttpPost("courses")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public Task<IActionResult> Publish(PublishCourseRequest request, CancellationToken token) =>
        Run(async (id, name) => await service.PublishAsync(id, name, request, token));

    [HttpPut("courses/{id:guid}/visibility")]
    public Task<IActionResult> Visibility(Guid id, CourseVisibilityRequest request, CancellationToken token) =>
        Run(async (owner, _) => await store.SetVisibilityAsync(owner, id, request.Published, token));

    [HttpGet("courses/{id:guid}/revisions/{revisionId:guid}")]
    public Task<IActionResult> Download(Guid id, Guid revisionId, CancellationToken token) =>
        Run(async (owner, _) => await store.DownloadAsync(owner, id, revisionId, token));

    [HttpPost("games")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public Task<IActionResult> Create(CreateCourseGameRequest request, CancellationToken token) =>
        Run(async (id, name) => await service.CreateGameAsync(id, name, request, token));

    [HttpGet("games")]
    public Task<IActionResult> Mine(int offset = 0, CancellationToken token = default) => Run(async (id, _) =>
    {
        if (offset < 0 || offset > 100000) throw new CourseCommunityException(CommunityError.Invalid, "Ugyldig side.");
        return await store.MyGamesAsync(id, offset, token);
    });

    [HttpGet("games/code/{code}")]
    [EnableRateLimiting("game-codes")]
    public Task<IActionResult> Preview(string code, CancellationToken token) => Run(async (_, _) => await service.PreviewGameAsync(code, token));

    [HttpPost("games/code/{code}/join")]
    [EnableRateLimiting("game-codes")]
    public Task<IActionResult> Join(string code, CancellationToken token) => Run(async (id, name) => await service.JoinGameAsync(id, name, code, token));

    [HttpGet("games/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken token) => Run(async (owner, _) => await store.GetGameAsync(owner, id, token));

    [HttpGet("games/{id:guid}/players/{accountId}/results")]
    public Task<IActionResult> PlayerResults(Guid id, string accountId, int offset = 0, CancellationToken token = default) =>
        Run(async (owner, _) => await store.GetPlayerResultsAsync(owner, id, accountId, offset, token));

    [HttpGet("games/{id:guid}/image")]
    public Task<IActionResult> Image(Guid id, CancellationToken token) => RunResponse(async (owner, _) =>
    {
        var image = await store.GetGameImageAsync(owner, id, token);
        Response.Headers.ETag = "\"" + image.Id + "\"";
        return File(image.Bytes, image.Bytes[0] == 137 ? "image/png" : "image/jpeg");
    });

    [HttpPut("games/{id:guid}/submission")]
    [RequestSizeLimit(1024 * 1024)]
    public Task<IActionResult> Submit(Guid id, SubmitCourseGameRequest request, CancellationToken token) => Run(async (owner, _) =>
    { await service.SubmitAsync(owner, id, request, token); return null; });

    [HttpPost("games/{id:guid}/close")]
    public Task<IActionResult> Close(Guid id, CancellationToken token) => Run(async (owner, _) =>
    { await store.CloseGameAsync(owner, id, token); return null; });

    private Task<IActionResult> Run(Func<string, string, Task<object?>> action) => RunResponse(async (id, name) =>
    {
        var result = await action(id, name);
        return result is null ? NoContent() : Ok(result);
    });

    private async Task<IActionResult> RunResponse(Func<string, string, Task<IActionResult>> action)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } id) return Unauthorized();
        try
        {
            return await action(id, User.FindFirstValue(ClaimTypes.Name) ?? "Spiller");
        }
        catch (CourseCommunityException ex)
        {
            var status = ex.Error switch { CommunityError.Invalid => 400, CommunityError.Forbidden => 403,
                CommunityError.Missing => 404, _ => 409 };
            return StatusCode(status, new ProblemDetails { Status = status, Title = ex.Message });
        }
    }
}
