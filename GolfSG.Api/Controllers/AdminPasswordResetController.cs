using System.ComponentModel.DataAnnotations;
using GolfSG.Api.Auth;
using GolfSG.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GolfSG.Api.Controllers;

public sealed class AdminPasswordResetRequest
{
    [Required, StringLength(50)] public string ConfirmUsername { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string NewPassword { get; init; } = "";
}

[ApiController]
[Route("api/admin/password-resets")]
[Authorize(Policy = AdminAccess.PasswordResetPolicy)]
[EnableRateLimiting("accounts")]
[RequestSizeLimit(4096)]
public sealed class AdminPasswordResetController(UserManager<IdentityUser> users, IConfiguration configuration,
    ILogger<AdminPasswordResetController> logger) : ControllerBase
{
    [HttpGet("access")]
    public IActionResult Access() => NoContent();

    [HttpPost("{id}")]
    public async Task<IActionResult> Reset(string id, AdminPasswordResetRequest request)
    {
        var actor = User.FindFirst(AdminAccess.SubjectClaim(configuration))?.Value;
        if (id.Length > 128 || !SuperAdminAccess.HasStrongPassword(request.NewPassword))
            return BadRequest(new ProblemDetails { Title = "Use 12–128 characters with uppercase, lowercase, a number and a symbol." });
        if (SuperAdminAccess.IsProvisioned(id, configuration) ||
            configuration.GetSection("AdminAuth:Subjects").Get<string[]>()?.Contains(id, StringComparer.Ordinal) == true) return Conflict(new ProblemDetails { Title = "Protected administrator accounts cannot be reset here." });
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (!string.Equals(user.UserName, request.ConfirmUsername, StringComparison.Ordinal))
            return Conflict(new ProblemDetails { Title = "The username confirmation does not match the current account." });
        // Token never leaves the API. Identity updates the password hash and security
        // stamp together, invalidating previous app access and refresh tokens.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        logger.LogInformation("Admin {Actor} password reset for account {Target}: {Outcome}", actor, id,
            result.Succeeded ? "succeeded" : "rejected");
        return result.Succeeded ? NoContent() : Conflict(new ProblemDetails { Title = "Password reset failed. Refresh the account and try again." });
    }
}
