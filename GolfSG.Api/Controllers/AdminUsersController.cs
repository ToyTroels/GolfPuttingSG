using GolfSG.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GolfSG.Server.Infrastructure;
using System.Security.Claims;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(AuthenticationSchemes = "Identity.Bearer", Policy = SuperAdminAccess.Policy)]
[EnableRateLimiting("accounts")]
public sealed class AdminUsersController(UserManager<IdentityUser> users, IConfiguration configuration,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int offset = 0, [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit < 1 || limit > 100) return BadRequest();
        return Ok(await users.ListAccountsAsync(offset, limit, cancellationToken));
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        // Administrators cannot lock themselves or other provisioned administrators out.
        if (configuration.GetSection("Auth:SuperAdminAccountIds").Get<string[]>()?
            .Contains(id, StringComparer.Ordinal) == true) return Conflict();
        var result = await users.SetLockoutEnabledAsync(user, true);
        if (!result.Succeeded) return Problem(statusCode: 409, title: "Account update failed.");
        result = await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!result.Succeeded) return Problem(statusCode: 409, title: "Account update failed.");
        return await Revoke(user, "disabled");
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(string id)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        // Rotate first so enabling never restores an old session.
        var result = await users.UpdateSecurityStampAsync(user);
        if (result.Succeeded) result = await users.ResetAccessFailedCountAsync(user);
        if (result.Succeeded) result = await users.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded) return Problem(statusCode: 409, title: "Account update failed.");
        Audit(user, "enabled");
        return NoContent();
    }

    [HttpPost("{id}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : await Revoke(user, "sessions revoked");
    }

    private async Task<IActionResult> Revoke(IdentityUser user, string action)
    {
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
            return Problem(statusCode: 409, title: "Session revocation failed.");
        Audit(user, action);
        return NoContent();
    }

    private void Audit(IdentityUser user, string action) => logger.LogInformation(
        "Super admin {ActorId} {Action} account {AccountId}",
        User.FindFirstValue(ClaimTypes.NameIdentifier), action, user.Id);
}
