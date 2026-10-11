using GolfSG.Application.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController(IServerConnectionCheck connection) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var available = await connection.IsReadyAsync(HttpContext.RequestAborted);
        return StatusCode(available ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            new { service = "GolfSG.Api", databaseAvailable = available });
    }
}
