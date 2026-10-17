using System.Reflection;
using GolfSG.Application.Server;
using GolfSG.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/admin/status")]
[Authorize(Policy = AdminAccess.Policy)]
[EnableRateLimiting("accounts")]
public sealed class AdminStatusController(IAdminDatabaseStatus database, IConfiguration configuration,
    IWebHostEnvironment environment, ILogger<AdminStatusController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var status = await database.ReadAsync(cancellationToken);
        var commit = configuration["RENDER_GIT_COMMIT"];
        if (commit is not { Length: 40 } || !commit.All(char.IsAsciiHexDigit)) commit = null;
        logger.LogInformation("Admin status read by {Subject}", User.FindFirst(configuration["AdminAuth:SubjectClaim"] ?? "sub")?.Value);
        return Ok(new
        {
            environment = environment.EnvironmentName,
            apiVersion = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            commit,
            databaseAvailable = status.Available,
            databaseVersion = status.Version,
            compatibility = "Unknown — cross-release certification is not implemented"
        });
    }
}
