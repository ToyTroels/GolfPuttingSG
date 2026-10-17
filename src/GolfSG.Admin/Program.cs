using GolfSG.Admin.Components;
using GolfSG.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Antiforgery;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace GolfSG.Admin;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config = builder.Configuration;
        void ValidateConfiguration()
        {
            if (config.GetValue<bool>("AdminAuth:Enabled"))
            {
                if (!builder.Environment.IsStaging() && !builder.Environment.IsDevelopment())
                    throw new InvalidOperationException("The admin dashboard is currently restricted to staging/development.");
                foreach (var key in new[] { "Authority", "Issuer", "ClientId", "ClientSecret", "ApiScope", "MfaClaim", "MfaValue" })
                    if (string.IsNullOrWhiteSpace(config[$"AdminAuth:{key}"]))
                        throw new InvalidOperationException($"AdminAuth:{key} is required.");
                foreach (var key in new[] { "Authority", "Issuer" })
                    if (!Uri.TryCreate(config[$"AdminAuth:{key}"], UriKind.Absolute, out var identityUri) || identityUri.Scheme != "https")
                        throw new InvalidOperationException($"AdminAuth:{key} must use HTTPS.");
                if (string.IsNullOrWhiteSpace(config["AllowedHosts"]) || config["AllowedHosts"]!.Contains('*'))
                    throw new InvalidOperationException("Configure explicit AllowedHosts for the admin service.");
                if (!Uri.TryCreate(config["AdminApi:BaseUrl"], UriKind.Absolute, out var api) || api.Scheme != "https" ||
                    !string.IsNullOrEmpty(api.UserInfo) || api.AbsolutePath != "/" || !string.IsNullOrEmpty(api.Query) || !string.IsNullOrEmpty(api.Fragment))
                    throw new InvalidOperationException("AdminApi:BaseUrl must be an HTTPS origin.");
                if (config.GetSection("AdminAuth:Subjects").Get<string[]>() is not { Length: > 0 })
                    throw new InvalidOperationException("Provision at least one AdminAuth:Subjects entry.");
            }
        }
        var keys = builder.Services.AddDataProtection().SetApplicationName("GolfSG.Admin.Staging");
        if (config["AdminAuth:DataProtectionKeysPath"] is { Length: > 0 } keyPath)
        {
            keys.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
            if (OperatingSystem.IsWindows()) keys.ProtectKeysWithDpapi();
        }
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = AdminAccess.Scheme;
        }).AddCookie(options =>
        {
            options.Cookie.Name = "__Host-GolfSG.Admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.AccessDeniedPath = "/access-denied";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = async context =>
            {
                if (!AdminAccess.IsAllowed(context.Principal!, config, false, false) ||
                    !context.Principal!.HasClaim("golfsg:api-mfa-verified", "true"))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync();
                }
            };
        }).AddOpenIdConnect(AdminAccess.Scheme, options =>
        {
            options.Authority = config["AdminAuth:Authority"];
            options.ClientId = config["AdminAuth:ClientId"] ?? "disabled-admin";
            if (!config.GetValue<bool>("AdminAuth:Enabled"))
                options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration { Issuer = "https://disabled.invalid" };
            options.ClientSecret = config["AdminAuth:ClientSecret"];
            options.ResponseType = "code";
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;
            options.SaveTokens = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            if (config["AdminAuth:ApiScope"] is { Length: > 0 } scope) options.Scope.Add(scope);
            if (config["AdminAuth:PasswordResetScope"] is { Length: > 0 } resetScope) options.Scope.Add(resetScope);
            options.TokenValidationParameters.ValidIssuer = config["AdminAuth:Issuer"];
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.MaxAge = "900";
                if (context.Properties.Items.ContainsKey("password-reset-reauth"))
                {
                    context.ProtocolMessage.Prompt = "login";
                    context.ProtocolMessage.MaxAge = "0";
                }
                if (config["AdminAuth:MfaClaim"] == "acrs")
                    context.ProtocolMessage.SetParameter("claims", JsonSerializer.Serialize(new
                    {
                        access_token = new { acrs = new { essential = true, value = config["AdminAuth:MfaValue"] } }
                    }));
                return Task.CompletedTask;
            };
            options.Events.OnTokenValidated = async context =>
            {
                if (!AdminAccess.IsAllowed(context.Principal!, config, false, false) ||
                    context.TokenEndpointResponse?.AccessToken is not { Length: > 0 } token)
                {
                    context.Fail("Admin access denied.");
                    return;
                }
                // The resource API validates signature, audience, scope, allowlist and MFA
                // on the access token. ID tokens may use different claim shapes.
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/status");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var response = await context.HttpContext.RequestServices.GetRequiredService<IHttpClientFactory>()
                        .CreateClient("AdminApi").SendAsync(request, context.HttpContext.RequestAborted);
                    if (!response.IsSuccessStatusCode) { context.Fail("Admin access denied."); return; }
                }
                catch (HttpRequestException) { context.Fail("Admin access unavailable."); return; }
                catch (OperationCanceledException) { context.Fail("Admin access unavailable."); return; }
                ((ClaimsIdentity)context.Principal!.Identity!).AddClaim(new Claim("golfsg:api-mfa-verified", "true"));
                context.Properties!.IsPersistent = false;
                context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15);
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/access-denied");
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy(AdminAccess.Policy,
            policy => policy.RequireAuthenticatedUser().RequireAssertion(c =>
                AdminAccess.IsAllowed(c.User, config, false, false) && c.User.HasClaim("golfsg:api-mfa-verified", "true"))));
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in config.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        builder.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-GolfSG.Admin.Antiforgery";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
        });
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<Microsoft.AspNetCore.Authentication.Cookies.ITicketStore, ServerTicketStore>();
        builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<Microsoft.AspNetCore.Authentication.Cookies.ITicketStore>((options, store) => options.SessionStore = store);
        builder.Services.AddRazorComponents();
        builder.Services.AddHttpContextAccessor();
        builder.AddPlayerPortal();
        builder.Services.AddHttpClient("AdminApi", client =>
        {
            client.BaseAddress = new Uri(config["AdminApi:BaseUrl"] ?? "https://localhost/");
            client.Timeout = TimeSpan.FromSeconds(120);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        var app = builder.Build();
        ValidateConfiguration();
        app.ValidatePlayerPortal();
        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.UseExceptionHandler(error => error.Run(async context =>
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("The dashboard could not complete the request.");
        }));
        app.UseHttpsRedirection();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'none'; style-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            var personal = context.Request.Path.StartsWithSegments("/my");
            var playerEnabled = config.GetValue<bool>("PlayerPortal:Enabled");
            if ((personal && !playerEnabled) || (!personal && !config.GetValue<bool>("AdminAuth:Enabled") && !(playerEnabled && context.Request.Path == "/admin.css")))
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Admin dashboard is not configured.");
                return;
            }
            await next(context);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseAntiforgery();
        app.MapPlayerPortal();
        app.MapGet("/access-denied", () => Results.Text("Admin access denied.", statusCode: 403)).AllowAnonymous();
        app.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/signed-out");
        }).RequireAuthorization(AdminAccess.Policy);
        app.MapGet("/signed-out", () => Results.Text("You are signed out. Close this browser window.")).AllowAnonymous();
        app.MapGet("/password-reset-sign-in", (string id) =>
        {
            if (id.Length > 128) return (IResult)Results.BadRequest();
            var properties = new AuthenticationProperties { RedirectUri = $"/users/{Uri.EscapeDataString(id)}/reset-password" };
            properties.Items["password-reset-reauth"] = "true";
            return Results.Challenge(properties, [AdminAccess.Scheme]);
        }).RequireAuthorization(AdminAccess.Policy);
        app.MapPost("/users/{id}/reset-password/submit", async (string id, HttpContext context, IAntiforgery antiforgery,
            IHttpClientFactory clients, IMemoryCache cache) =>
        {
            if (id.Length > 128) return Results.BadRequest();
            var bodyLimit = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (bodyLimit is { IsReadOnly: false }) bodyLimit.MaxRequestBodySize = 4096;
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var password = form["newPassword"].ToString();
            var username = form["confirmUsername"].ToString();
            var returnUrl = $"/users/{Uri.EscapeDataString(id)}/reset-password";
            if (password.Length is < 12 or > 128 || username.Length is < 1 or > 50 ||
                password != form["confirmPassword"].ToString() || form["confirmed"] != "true")
                return Results.Redirect(returnUrl + "?result=failed");
            var token = await context.GetTokenAsync("access_token");
            if (string.IsNullOrEmpty(token)) return Results.Forbid();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"api/admin/password-resets/{Uri.EscapeDataString(id)}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new { confirmUsername = username, newPassword = password });
                using var response = await clients.CreateClient("AdminApi").SendAsync(request, context.RequestAborted);
                if (!response.IsSuccessStatusCode) return Results.Redirect(returnUrl + "?result=failed");
                var receipt = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                var actor = context.User.FindFirst(config["AdminAuth:SubjectClaim"] ?? "sub")!.Value;
                cache.Set("password-reset-" + receipt, new PasswordResetOutcome(id, actor), TimeSpan.FromMinutes(2));
                return Results.Redirect(returnUrl + "?receipt=" + receipt);
            }
            catch (HttpRequestException) { return Results.Redirect(returnUrl + "?result=failed"); }
            catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested) { return Results.Redirect(returnUrl + "?result=failed"); }
        }).RequireAuthorization(AdminAccess.Policy);
        app.MapStaticAssets();
        app.MapRazorComponents<App>().RequireAuthorization(AdminAccess.Policy);
        app.Run();
    }
}
