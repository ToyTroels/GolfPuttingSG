using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GolfSG.Admin.Components;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GolfSG.Admin;

public static class CourseSetupPortal
{
    public static void MapCourseSetup(this WebApplication app)
    {
        app.MapGet("/my/setup", () => new RazorComponentResult<CourseSetupDocument>()).RequireAuthorization(PlayerPortal.Policy);
        app.MapGet("/my/setup/courses", async Task<IResult> (HttpContext context) => await Proxy(context, HttpMethod.Get, "api/community/courses?mine=true", null))
            .RequireAuthorization(PlayerPortal.Policy);
        app.MapGet("/my/setup/courses/{id:guid}/{revision:guid}", (Guid id, Guid revision, HttpContext context) =>
            Proxy(context, HttpMethod.Get, $"api/community/courses/{id}/revisions/{revision}", null)).RequireAuthorization(PlayerPortal.Policy);
        app.MapPost("/my/setup/{action}", async (string action, HttpContext context, IAntiforgery antiforgery) =>
        {
            if (action is not ("course" or "game")) return Results.NotFound();
            var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 15 * 1024 * 1024;
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(new { message = "Your session form expired. Reload before saving." }); }
            try
            {
                using var json = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                // Website saves are always private; publishing requires a separate explicit workflow.
                var body = action == "course"
                    ? JsonSerializer.Serialize(new { package = json.RootElement.GetProperty("package"), published = false })
                    : json.RootElement.GetRawText();
                return await Proxy(context, HttpMethod.Post, action == "course" ? "api/community/courses" : "api/community/games", body);
            }
            catch (JsonException) { return Results.BadRequest(new { message = "The course data could not be read." }); }
            catch (KeyNotFoundException) { return Results.BadRequest(new { message = "The course package is missing." }); }
        }).RequireAuthorization(PlayerPortal.Policy).RequireRateLimiting("player-login");
    }

    private static async Task<IResult> Proxy(HttpContext context, HttpMethod method, string path, string? body)
    {
        var session = await context.AuthenticateAsync(PlayerPortal.Scheme);
        var token = session.Properties?.GetTokenValue("access_token");
        if (string.IsNullOrWhiteSpace(token)) return Results.Unauthorized();
        try
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("CourseSetupApi")
                .SendAsync(request, context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                return Results.Json(new { message = response.StatusCode == System.Net.HttpStatusCode.BadRequest
                    ? "Check the picture, calibration and shot settings. Distances must be 0.1–1000 m."
                    : "The request could not be completed. Check your connection and sign-in before retrying." }, statusCode: (int)response.StatusCode);
            if (method == HttpMethod.Post) context.RequestServices.GetRequiredService<PersonalApiCache>().Invalidate();
            return Results.Content(await response.Content.ReadAsStringAsync(context.RequestAborted), "application/json");
        }
        catch (HttpRequestException) { return Results.Json(new { message = "The server is unavailable. Your setup is still on this page." }, statusCode: 503); }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        { return Results.Json(new { message = "The request timed out. Check My games before retrying game creation." }, statusCode: 504); }
    }
}
