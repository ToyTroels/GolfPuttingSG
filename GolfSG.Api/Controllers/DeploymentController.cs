using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/deployment")]
public sealed class DeploymentController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var commit = configuration["RENDER_GIT_COMMIT"];
        if (commit is not { Length: 40 } || !commit.All(char.IsAsciiHexDigit)) commit = null;
        return Ok(new { service = "GolfSG.Api", commit });
    }
}
