using System.Net.Http.Headers;
using System.Security.Claims;
using GolfSG.Admin.Components;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace GolfSG.Admin;

public static class PlayerPortal
{
    public const string Scheme = "PlayerSession";
    public const string Policy = "PersonalData";

    public static void AddPlayerPortal(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<PersonalApiCache>();
        builder.Services.AddHttpClient("PlayerApi", client =>
        {
            client.BaseAddress = new Uri(builder.Configuration["PlayerPortal:ApiBaseUrl"] ?? "https://localhost:7151/");
            client.Timeout = TimeSpan.FromMinutes(2);
            client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        builder.Services.AddAuthentication().AddCookie(Scheme, options =>
        {
            options.Cookie.Name = "__Host-GolfSG.Player";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/my/login";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = async context =>
            {
                var token = context.Properties.GetTokenValue("access_token");
                var identity = await ReadIdentity(context.HttpContext, token);
                if (identity is null || identity.Id != context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier))
                    context.RejectPrincipal();
            };
        });
        builder.Services.AddOptions<CookieAuthenticationOptions>(Scheme).Configure<ITicketStore>((options, store) => options.SessionStore = store);
        builder.Services.AddAuthorization(options => options.AddPolicy(Policy, policy => policy.AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser()));
        builder.Services.AddRateLimiter(options => options.AddPolicy("player-login", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
    }

    public static void ValidatePlayerPortal(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("PlayerPortal:Enabled")) return;
        if (!app.Environment.IsDevelopment() && !app.Environment.IsStaging()) throw new InvalidOperationException("Personal portal currently requires staging/development.");
        if (string.IsNullOrWhiteSpace(app.Configuration["AllowedHosts"]) || app.Configuration["AllowedHosts"]!.Contains('*'))
            throw new InvalidOperationException("Set explicit AllowedHosts for the personal portal.");
        if (!Uri.TryCreate(app.Configuration["PlayerPortal:ApiBaseUrl"], UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.AbsolutePath != "/" || uri.UserInfo != "" || uri.Query != "" || uri.Fragment != "")
            throw new InvalidOperationException("PlayerPortal:ApiBaseUrl must be an HTTPS origin.");
    }

    public static void MapPlayerPortal(this WebApplication app)
    {
        app.MapGet("/my/login", (HttpContext context) => (IResult)new RazorComponentResult<MyDataDocument>(new
        { Login = true, Error = context.Request.Query.ContainsKey("failed") ? "Sign-in failed or the server is unavailable. Try again." : null })).AllowAnonymous();
        app.MapPost("/my/login", async (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients) =>
        {
            LimitBody(context);
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            var form = await context.Request.ReadFormAsync();
            var username = form["username"].ToString().Trim();
            var password = form["password"].ToString();
            if (username.Length is < 1 or > 50 || password.Length is < 1 or > 128) return Results.Redirect("/my/login?failed=1");
            try
            {
                using var response = await clients.CreateClient("PlayerApi").PostAsJsonAsync("api/auth/login", new { username, password }, context.RequestAborted);
                if (!response.IsSuccessStatusCode) return Results.Redirect("/my/login?failed=1");
                var tokens = await response.Content.ReadFromJsonAsync<PlayerToken>(context.RequestAborted);
                if (tokens is null || tokens.ExpiresIn <= 0 || string.IsNullOrWhiteSpace(tokens.AccessToken)) return Results.Redirect("/my/login?failed=1");
                var identity = await ReadIdentity(context, tokens.AccessToken);
                if (identity is null) return Results.Redirect("/my/login?failed=1");
                var properties = new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Min(900, tokens.ExpiresIn)) };
                properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken }]);
                await context.SignInAsync(Scheme, new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, identity.Id), new Claim(ClaimTypes.Name, identity.Username)], Scheme)), properties);
                return Results.Redirect("/my");
            }
            catch (HttpRequestException) { return Results.Redirect("/my/login?failed=1"); }
            catch (System.Text.Json.JsonException) { return Results.Redirect("/my/login?failed=1"); }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { return Results.Redirect("/my/login?failed=1"); }
        }).AllowAnonymous().RequireRateLimiting("player-login");
        app.MapPost("/my/logout", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            await context.SignOutAsync(Scheme);
            return Results.Redirect("/my/login");
        }).RequireAuthorization(Policy);
        app.MapGet("/my", async (HttpContext context, IHttpClientFactory clients) =>
        {
            var section = context.Request.Query["section"].ToString();
            if (section == "") section = "rounds";
            var rawOffset = context.Request.Query["offset"].ToString();
            var offset = 0;
            if (section is not ("rounds" or "courses" or "games") || (rawOffset != "" && !int.TryParse(rawOffset, out offset)) || offset is < 0 or > 100000)
                return (IResult)Results.BadRequest();
            PersonalDataPage? page = null;
            try
            {
                page = await CachedRead<PersonalDataPage>(context, $"api/me/data/{section}?offset={offset}");
            }
            catch (HttpRequestException) { }
            catch (System.Text.Json.JsonException) { }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { }
            return new RazorComponentResult<MyDataDocument>(new
            {
                Section = section,
                Page = page,
                Offset = offset,
                Username = context.User.Identity?.Name ?? "",
                Error = page is null ? "Your data could not be loaded. Try again or sign in again." : null
            });
        }).RequireAuthorization(Policy);
        app.MapGet("/my/rounds/{id}/export", async (string id, HttpContext context, IHttpClientFactory clients) =>
        {
            if (id.Length > 100) return Results.BadRequest();
            try
            {
                using var request = await ApiRequest(context, HttpMethod.Get, "api/rounds/" + Uri.EscapeDataString(id));
                using var response = await clients.CreateClient("PlayerApi").SendAsync(request, context.RequestAborted);
                if (!response.IsSuccessStatusCode) return Results.NotFound();
                return Results.File(await response.Content.ReadAsByteArrayAsync(context.RequestAborted), "application/json", "my-round.json");
            }
            catch (HttpRequestException) { return Results.StatusCode(503); }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { return Results.StatusCode(503); }
        }).RequireAuthorization(Policy);
        app.MapGet("/my/performance", (Func<HttpContext, Task<IResult>>)(context => Performance(context, null))).RequireAuthorization(Policy);
        app.MapGet("/my/rounds/{id}", (string id, HttpContext context) => Performance(context, id)).RequireAuthorization(Policy);
    }

    private static async Task<IResult> Performance(HttpContext context, string? id)
    {
        if (id is not null && (id.Length is < 1 or > 100 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            return Results.BadRequest();
        PersonalPerformance? overview = null;
        PersonalRoundDetail? detail = null;
        try
        {
            if (id is null) overview = await CachedRead<PersonalPerformance>(context, "api/me/performance");
            else detail = await CachedRead<PersonalRoundDetail>(context, "api/me/performance/rounds/" + Uri.EscapeDataString(id));
        }
        catch (HttpRequestException) { }
        catch (System.Text.Json.JsonException) { }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { }
        return new RazorComponentResult<PersonalPerformanceDocument>(new { Preferences = DashboardPreferences.Read(context, context.User.FindFirstValue(ClaimTypes.NameIdentifier)!), Data = overview, Detail = detail, RoundPage = id is not null,
            Error = overview is null ? "Your performance could not be loaded. Check your connection or sign in again." : null })
            { StatusCode = id is null ? (overview is null ? 503 : 200) : (detail is null ? 404 : 200) };
    }

    private static async Task<T?> CachedRead<T>(HttpContext context, string path) where T : class
    {
        var session = await context.AuthenticateAsync(Scheme);
        var token = session.Properties?.GetTokenValue("access_token") ?? throw new InvalidOperationException("Missing player token.");
        var owner = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing player identity.");
        var key = PersonalApiCache.Key(owner, token, path);
        return await context.RequestServices.GetRequiredService<PersonalApiCache>().GetAsync<T>(key, async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("PlayerApi").SendAsync(request, context.RequestAborted);
            if (!response.IsSuccessStatusCode) return (null, 0);
            var bytes = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
            var value = System.Text.Json.JsonSerializer.Deserialize<T>(bytes, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            return (value, bytes.Length);
        }, context.Request.Query["refresh"] == "1", context.RequestAborted);
    }
    private static void LimitBody(HttpContext context)
    {
        var limit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = 4096;
    }
    private static async Task<HttpRequestMessage> ApiRequest(HttpContext context, HttpMethod method, string path)
    {
        var session = await context.AuthenticateAsync(Scheme);
        var token = session.Properties?.GetTokenValue("access_token") ?? throw new InvalidOperationException("Missing player token.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
    private static async Task<PlayerIdentity?> ReadIdentity(HttpContext context, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("PlayerApi").SendAsync(request, context.RequestAborted);
            var identity = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<PlayerIdentity>(context.RequestAborted) : null;
            return identity is { Id.Length: > 0, Username.Length: > 0 } ? identity : null;
        }
        catch (HttpRequestException) { return null; }
        catch (System.Text.Json.JsonException) { return null; }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { return null; }
    }
    private sealed record PlayerToken(string AccessToken, int ExpiresIn);
    private sealed record PlayerIdentity(string Id, string Username);
}
