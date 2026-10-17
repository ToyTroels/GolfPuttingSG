using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace GolfSG.Security;

// Shared authorization rules, evaluated from current configuration on every request.
public static class AdminAccess
{
    public const string Policy = "AdminDashboard";
    public const string Scheme = "AdminOidc";
    public const string PasswordResetPolicy = "AdminPasswordReset";
    public const string PasswordResetScope = "admin.users.password-reset";

    public static bool CanResetPasswords(ClaimsPrincipal user, IConfiguration configuration, DateTimeOffset now)
    {
        if (!IsAllowed(user, configuration, true)) return false;
        var identity = user.Identities.First(i => i.IsAuthenticated);
        var subject = identity.FindFirst(configuration["AdminAuth:SubjectClaim"] ?? "sub")?.Value;
        if (configuration.GetSection("AdminAuth:PasswordResetSubjects").Get<string[]>()?.Contains(subject, StringComparer.Ordinal) != true ||
            !identity.FindAll("scope").Concat(identity.FindAll("scp")).Any(c => c.Value.Split(' ').Contains(PasswordResetScope, StringComparer.Ordinal)) ||
            !long.TryParse(identity.FindFirst("auth_time")?.Value, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var authenticatedAt)) return false;
        var age = now.ToUnixTimeSeconds() - authenticatedAt;
        return age is >= -30 and <= 300;
    }

    public static bool IsAllowed(ClaimsPrincipal user, IConfiguration configuration, bool requireScope, bool requireMfa = true)
    {
        var identity = user.Identities.FirstOrDefault(i => i.IsAuthenticated);
        if (identity is null || !configuration.GetValue<bool>("AdminAuth:Enabled")) return false;
        var issuer = configuration["AdminAuth:Issuer"];
        var subject = identity.FindFirst(configuration["AdminAuth:SubjectClaim"] ?? "sub")?.Value;
        var mfaClaim = configuration["AdminAuth:MfaClaim"];
        var mfaValue = configuration["AdminAuth:MfaValue"];
        if (string.IsNullOrWhiteSpace(issuer) || identity.FindFirst("iss")?.Value != issuer ||
            string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(mfaClaim) ||
            string.IsNullOrWhiteSpace(mfaValue) || (requireMfa && !identity.HasClaim(mfaClaim, mfaValue)) ||
            configuration.GetSection("AdminAuth:Subjects").Get<string[]>()?.Contains(subject, StringComparer.Ordinal) != true)
            return false;
        return !requireScope || identity.FindAll("scope").Concat(identity.FindAll("scp"))
            .Any(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("admin.read", StringComparer.Ordinal));
    }
}
