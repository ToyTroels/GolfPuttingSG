using System.Net.Http.Headers;
using System.Security.Claims;
using GolfSG.Security;
using GolfSG.Admin.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace GolfSG.Admin;

public static class PasswordAdmin
{
    public static async Task<bool> ValidateSession(HttpContext context, ClaimsPrincipal principal, AuthenticationProperties properties)
    {
        if (!AdminAccess.IsAllowed(principal, context.RequestServices.GetRequiredService<IConfiguration>(), false)) return false;
        var identity = await ReadIdentity(context, properties.GetTokenValue("access_token"));
        return identity is not null && identity.Id == principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static async Task<Account?> ReadIdentity(HttpContext context, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi");
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, context.RequestAborted);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<Account>(context.RequestAborted) : null;
        }
        catch (HttpRequestException) { return null; }
        catch (System.Text.Json.JsonException) { return null; }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { return null; }
    }

    public static void MapPasswordAdmin(this WebApplication app)
    {
        app.MapGet("/login", (HttpContext context) =>
        {
            if (!AdminAccess.UsesPassword(app.Configuration) ||
                (context.User.Identity?.IsAuthenticated == true && context.Request.Query["reauth"] != "1"))
                return (IResult)Results.Redirect("/");
            return new RazorComponentResult<AdminLoginDocument>(new
            {
                Failed = context.Request.Query.ContainsKey("failed"),
                Expired = context.Request.Query.ContainsKey("expired"),
                Reauthenticate = context.Request.Query["reauth"] == "1"
            });
        }).AllowAnonymous();
        app.MapPost("/login", async (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients) =>
        {
            if (!AdminAccess.UsesPassword(app.Configuration)) return Results.NotFound();
            // Back can restore the anonymous form after its antiforgery token has
            // become invalid for the signed-in identity. Keep the valid session.
            if (context.User.Identity?.IsAuthenticated == true && context.Request.Query["reauth"] != "1")
                return Results.Redirect("/");
            var failedLogin = context.Request.Query["reauth"] == "1" ? "/login?reauth=1&failed=1" : "/login?failed=1";
            var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 4096;
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                return Results.Redirect(context.Request.Query["reauth"] == "1"
                    ? "/login?reauth=1&expired=1" : "/login?expired=1");
            }
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var username = form["username"].ToString().Trim();
            var password = form["password"].ToString();
            if (username.Length is < 1 or > 50 || password.Length is < 12 or > 128) return Results.Redirect(failedLogin);
            try
            {
                using var response = await clients.CreateClient("AdminApi").PostAsJsonAsync("api/auth/dashboard-login", new { username, password }, context.RequestAborted);
                if (!response.IsSuccessStatusCode) return Results.Redirect(failedLogin);
                var tokens = await response.Content.ReadFromJsonAsync<Tokens>(context.RequestAborted);
                if (tokens is null || tokens.ExpiresIn <= 0) return Results.Redirect(failedLogin);
                var account = await ReadIdentity(context, tokens.AccessToken);
                if (account is null) return Results.Redirect(failedLogin);
                var principal = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, account.Id), new Claim(ClaimTypes.Name, account.Username),
                    new Claim(AdminAccess.PasswordClaim, "true"),
                    new Claim("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture))], CookieAuthenticationDefaults.AuthenticationScheme));
                if (!AdminAccess.IsAllowed(principal, app.Configuration, false)) return Results.Redirect(failedLogin);
                var properties = new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Min(900, tokens.ExpiresIn)) };
                properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken }]);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
                return Results.Redirect("/");
            }
            catch (HttpRequestException) { return Results.Redirect(failedLogin); }
            catch (System.Text.Json.JsonException) { return Results.Redirect(failedLogin); }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { return Results.Redirect(failedLogin); }
        }).AllowAnonymous().RequireRateLimiting("player-login");
    }
    private sealed record Tokens(string AccessToken, int ExpiresIn);
    private sealed record Account(string Id, string Username);
}
