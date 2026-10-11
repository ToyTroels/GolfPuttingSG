using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GolfSG.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
[Route("api/rounds")]
public sealed class RoundsController(ServerRoundService rounds) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RemoteRound>>> GetAll(CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId) return Unauthorized();
        return Ok(await rounds.GetAllAsync(userId, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RemoteRound>> GetById(string id, CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId) return Unauthorized();
        var round = await rounds.GetByIdAsync(userId, id, cancellationToken);
        return round is null ? NotFound() : Ok(round);
    }

    [HttpPut("{id}")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<RemoteRound>> Write(string id, RoundWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } userId) return Unauthorized();
        var result = await rounds.WriteAsync(userId, id, request, cancellationToken);
        return result.Status switch
        {
            ServerRoundWriteStatus.Written => Ok(result.Round),
            ServerRoundWriteStatus.InvalidIdentity => BadRequest(new ProblemDetails { Title = "Invalid round ID or version." }),
            ServerRoundWriteStatus.InvalidData => BadRequest(new ProblemDetails { Title = "Invalid round data." }),
            ServerRoundWriteStatus.Conflict => Conflict(new ProblemDetails { Title = "The round changed. Read its current version and retry." }),
            _ => throw new InvalidOperationException("Unknown round write result.")
        };
    }
}
