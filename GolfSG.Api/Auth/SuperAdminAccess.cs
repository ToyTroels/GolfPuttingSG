using System.Security.Claims;

namespace GolfSG.Api.Auth;

public static class SuperAdminAccess
{
    public const string Policy = "SuperAdmin";
    public const string AuthenticationClaim = "golfsg:admin-authenticated";

    public static bool IsProvisioned(string id, IConfiguration configuration) =>
        configuration.GetSection("Auth:SuperAdminAccountIds").Get<string[]>()?
            .Contains(id, StringComparer.Ordinal) == true;

    public static bool HasStrongPassword(string password) => password.Length is >= 12 and <= 128 &&
        password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit) &&
        password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c));

    // Provision existing account IDs through server configuration, never registration input.
    public static bool IsAllowed(ClaimsPrincipal principal, IConfiguration configuration)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return principal.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(id) &&
            principal.HasClaim(AuthenticationClaim, "true") && IsProvisioned(id, configuration);
    }
}
