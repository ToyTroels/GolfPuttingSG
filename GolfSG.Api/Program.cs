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
        builder.Services.AddControllers();
        builder.Services.AddScoped<ServerRoundService>();
        builder.Services.AddScoped<CourseCommunityService>();
        builder.Services.AddServerInfrastructure(builder.Configuration);
        builder.Services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);
        builder.Services.AddAuthorization(options => options.AddPolicy(
            GolfSG.Api.Auth.SuperAdminAccess.Policy, policy => policy.RequireAuthenticatedUser()
                .RequireAssertion(context => GolfSG.Api.Auth.SuperAdminAccess.IsAllowed(context.User, builder.Configuration))));
        builder.Services.AddSingleton(TimeProvider.System);
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
        app.UseExceptionHandler();
        if (!app.Environment.IsDevelopment()) app.UseHsts();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/auth") ||
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
