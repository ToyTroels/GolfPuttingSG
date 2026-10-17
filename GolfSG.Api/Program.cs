using GolfSG.Application.Server;
using GolfSG.Application.Courses;
using GolfSG.Server.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using System.Threading.RateLimiting;

namespace GolfSG.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        void ValidateAdminConfiguration()
        {
            if (builder.Configuration.GetValue<bool>("AdminAuth:Enabled"))
            {
                foreach (var key in new[] { "Authority", "Issuer", "Audience", "MfaClaim", "MfaValue" })
                    if (string.IsNullOrWhiteSpace(builder.Configuration[$"AdminAuth:{key}"]))
                        throw new InvalidOperationException($"AdminAuth:{key} is required.");
                foreach (var key in new[] { "Authority", "Issuer" })
                    if (!Uri.TryCreate(builder.Configuration[$"AdminAuth:{key}"], UriKind.Absolute, out var uri) || uri.Scheme != "https")
                        throw new InvalidOperationException($"AdminAuth:{key} must use HTTPS.");
                if (builder.Configuration.GetSection("AdminAuth:Subjects").Get<string[]>() is not { Length: > 0 })
                    throw new InvalidOperationException("Provision at least one AdminAuth:Subjects entry.");
            }
        }
        builder.Services.AddControllers();
        builder.Services.AddScoped<ServerRoundService>();
        builder.Services.AddScoped<CourseCommunityService>();
        builder.Services.AddServerInfrastructure(builder.Configuration);
        builder.Services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme)
            .AddJwtBearer(GolfSG.Security.AdminAccess.Scheme, options =>
            {
                options.Authority = builder.Configuration["AdminAuth:Authority"];
                options.Audience = builder.Configuration["AdminAuth:Audience"];
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = true;
                options.TokenValidationParameters.ValidateAudience = true;
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidIssuer = builder.Configuration["AdminAuth:Issuer"];
                options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
            });
        builder.Services.AddAuthorization(options => options.AddPolicy(GolfSG.Security.AdminAccess.Policy,
            policy => policy.AddAuthenticationSchemes(GolfSG.Security.AdminAccess.Scheme)
                .RequireAuthenticatedUser().RequireAssertion(context =>
                    (builder.Environment.IsStaging() || builder.Environment.IsDevelopment()) &&
                    GolfSG.Security.AdminAccess.IsAllowed(context.User, builder.Configuration, requireScope: true))));
        builder.Services.AddAuthorization(options => options.AddPolicy(
            GolfSG.Api.Auth.SuperAdminAccess.Policy, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => GolfSG.Api.Auth.SuperAdminAccess.IsAllowed(context.User, builder.Configuration))));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddAuthorization(options => options.AddPolicy(GolfSG.Security.AdminAccess.PasswordResetPolicy,
            policy => policy.AddAuthenticationSchemes(GolfSG.Security.AdminAccess.Scheme).RequireAuthenticatedUser()
                .RequireAssertion(context => (builder.Environment.IsStaging() || builder.Environment.IsDevelopment()) &&
                    GolfSG.Security.AdminAccess.CanResetPasswords(context.User, builder.Configuration, DateTimeOffset.UtcNow))));
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName(
            builder.Environment.IsStaging() ? "GolfSG.Api.Staging" : "GolfSG.Api");
        if (builder.Configuration["Auth:DataProtectionKeysPath"] is { Length: > 0 } keysPath)
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
            if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi();
        }
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("game-codes", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many account requests. Try again shortly.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();

        var app = builder.Build();
        ValidateAdminConfiguration();
        app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/auth") ||
                context.Request.Path.StartsWithSegments("/api/me") ||
                context.Request.Path.StartsWithSegments("/api/admin"))
                context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });

        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var signIns = context.RequestServices.GetRequiredService<SignInManager<IdentityUser>>();
                var users = context.RequestServices.GetRequiredService<UserManager<IdentityUser>>();
                var user = await signIns.ValidateSecurityStampAsync(context.User);
                if (user is null || await users.IsLockedOutAsync(user))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
            await next(context);
        });
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        app.Run();
    }
}
