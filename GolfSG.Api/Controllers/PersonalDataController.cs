using System.Security.Claims;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/me/data/{section}")]
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
[EnableRateLimiting("accounts")]
public sealed class PersonalDataController(IPersonalData data) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string section, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 } id) return Unauthorized();
        if (section is not ("rounds" or "courses" or "games")) return NotFound();
        if (offset is < 0 or > 100000) return BadRequest();
        return Ok(await data.ReadAsync(id, section, offset, cancellationToken));
    }
}
