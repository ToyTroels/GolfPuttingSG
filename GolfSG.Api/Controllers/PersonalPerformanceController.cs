using System.Security.Claims;
using GolfSG.Application.Server;
using GolfSG.Application.Rounds.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/me/performance")]
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
[EnableRateLimiting("accounts")]
public sealed class PersonalPerformanceController(IPersonalPerformance performance) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 } id) return Unauthorized();
        return Ok(await performance.OverviewAsync(id, cancellationToken));
    }
    [HttpGet("rounds/{roundId}")]
    public async Task<IActionResult> Round(string roundId, CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { Length: > 0 } id) return Unauthorized();
        if (!RoundSyncValidation.IsValidId(roundId)) return BadRequest();
        var round = await performance.DetailAsync(id, roundId, cancellationToken);
        return round is null ? NotFound() : Ok(round);
    }
}
