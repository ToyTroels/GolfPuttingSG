using System.Security.Claims;
using GolfSG.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/admin/session")]
[Authorize(Policy = AdminAccess.Policy)]
[EnableRateLimiting("admin-reads")]
public sealed class AdminSessionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        username = User.Identity?.Name
    });
}
