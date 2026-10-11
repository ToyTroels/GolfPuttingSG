using GolfSG.Application.Accounts;
using GolfSG.Application.Server;
using GolfSG.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Server.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.")));
        services.AddScoped<IServerRoundStore, PostgresRoundStore>();
        services.AddScoped<IServerProductStore, PostgresProductStore>();
        services.AddScoped<IServerConnectionCheck, PostgresConnectionCheck>();
        services.AddIdentityCore<IdentityUser>(options =>
            {
                var allowSimplePasswords = configuration.GetValue<bool>("Auth:AllowSimplePasswordsForLocalTesting");
                var requireComplexity = ApiPasswordPolicy.RequiresComplexity && !allowSimplePasswords;
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-";
                options.SignIn.RequireConfirmedEmail = false;
                options.Password.RequiredLength = allowSimplePasswords ? 1 : ApiPasswordPolicy.MinimumLength;
                options.Password.RequireDigit = requireComplexity;
                options.Password.RequireLowercase = requireComplexity;
                options.Password.RequireUppercase = requireComplexity;
                options.Password.RequireNonAlphanumeric = requireComplexity;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();
        return services;
    }
}
