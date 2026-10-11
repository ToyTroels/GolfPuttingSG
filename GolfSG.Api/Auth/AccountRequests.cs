using System.ComponentModel.DataAnnotations;

namespace GolfSG.Api.Auth;

public sealed class AccountCredentialsRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9_.-]+$")]
    public string Username { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
    [Required]
    [StringLength(16384)]
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed record AccountIdentityDto(string Id, string Username);
