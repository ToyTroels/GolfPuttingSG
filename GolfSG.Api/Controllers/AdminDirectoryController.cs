using GolfSG.Application.Server;
using GolfSG.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/admin/directory/{section}")]
[Authorize(Policy = AdminAccess.Policy)]
[EnableRateLimiting("admin-reads")]
public sealed class AdminDirectoryController(IAdminDirectory directory, IConfiguration configuration,
    ILogger<AdminDirectoryController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string section, [FromQuery] string? search = null,
        [FromQuery] int offset = 0, [FromQuery] int limit = 25, CancellationToken cancellationToken = default)
    {
        if (section is not ("users" or "courses" or "games")) return NotFound();
        if (search?.Length > 100 || offset is < 0 or > 100000 || limit is < 1 or > 100) return BadRequest();
        var page = await directory.ReadAsync(section, search?.Trim() ?? "", offset, limit, cancellationToken);
        // Deliberately omit search terms and returned account data from audit logs.
        logger.LogInformation("Admin {Subject} read directory {Section} at offset {Offset}",
            User.FindFirst(AdminAccess.SubjectClaim(configuration))?.Value, section, offset);
        return Ok(page);
    }
}
