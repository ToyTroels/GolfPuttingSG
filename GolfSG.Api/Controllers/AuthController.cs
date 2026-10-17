using System.Security.Claims;
using GolfSG.Api.Auth;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("accounts")]
[RequestSizeLimit(32 * 1024)]
public sealed class AuthController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIns,
    IOptionsMonitor<BearerTokenOptions> bearerOptions, TimeProvider timeProvider) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AccountIdentityDto>> Register(AccountCredentialsRequest request)
    {
        var user = new IdentityUser { UserName = request.Username.Trim() };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            var errors = created.Errors.GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }
        return Ok(new AccountIdentityDto(user.Id, user.UserName!));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(AccountCredentialsRequest request)
    {
        signIns.AuthenticationScheme = IdentityConstants.BearerScheme;
        var result = await signIns.PasswordSignInAsync(request.Username.Trim(), request.Password,
            isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new ProblemDetails { Title = "Sign-in failed. Check your credentials or try again later." });
        // The framework's bearer handler has already written the access/refresh token response.
        return new EmptyResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var ticket = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector
            .Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expiresUtc || timeProvider.GetUtcNow() >= expiresUtc ||
            await signIns.ValidateSecurityStampAsync(ticket.Principal) is not { } user ||
            await users.IsLockedOutAsync(user))
            return Unauthorized();

        var principal = await signIns.CreateUserPrincipalAsync(user);
        return SignIn(principal, IdentityConstants.BearerScheme);
    }

    [HttpPost("admin-login")]
    [AllowAnonymous]
    public async Task<IActionResult> AdminLogin(AccountCredentialsRequest request,
        [FromServices] IConfiguration configuration)
    {
        // Admin sessions require a dedicated strong-password login even in Debug.
        // Ordinary app access/refresh tokens never receive this claim.
        if (!SuperAdminAccess.HasStrongPassword(request.Password)) return Unauthorized();
        var user = await users.FindByNameAsync(request.Username.Trim());
        if (user is null || !SuperAdminAccess.IsProvisioned(user.Id, configuration)) return Unauthorized();
        var result = await signIns.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized();
        var principal = await signIns.CreateUserPrincipalAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(SuperAdminAccess.AuthenticationClaim, "true"));
        return SignIn(principal, IdentityConstants.BearerScheme);
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "Identity.Bearer")]
    public ActionResult<AccountIdentityDto> Me()
    {
        if (User.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
            return Unauthorized();

        var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = identity.Name;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        return Ok(new AccountIdentityDto(userId, username));
    }
}
