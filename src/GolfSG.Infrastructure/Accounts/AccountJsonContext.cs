using System.Text.Json;
using System.Text.Json.Serialization;
using GolfSG.Application.Accounts;

namespace GolfSG.Infrastructure.Accounts;

internal sealed record AccountCredentials(string Username, string Password)
{ public override string ToString() => "Account credentials"; }
internal sealed record AccountRefresh(string RefreshToken)
{ public override string ToString() => "Account refresh"; }
internal sealed record AccountTokens([property: JsonRequired] string TokenType,
    [property: JsonRequired] string AccessToken, [property: JsonRequired] long ExpiresIn,
    [property: JsonRequired] string RefreshToken)
{ public override string ToString() => "Account tokens"; }
internal sealed record AccountIdentity([property: JsonRequired] string Id, [property: JsonRequired] string Username);
internal sealed record ApiHealth([property: JsonRequired] string Service, [property: JsonRequired] bool DatabaseAvailable);
internal sealed record AccountValidationErrors(Dictionary<string, string[]>? Errors);
internal sealed record AccountBinding([property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string ServerAddress, [property: JsonRequired] string AccountId);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApiSession))]
[JsonSerializable(typeof(AccountCredentials))]
[JsonSerializable(typeof(AccountRefresh))]
[JsonSerializable(typeof(AccountTokens))]
[JsonSerializable(typeof(AccountIdentity))]
[JsonSerializable(typeof(ApiHealth))]
[JsonSerializable(typeof(AccountValidationErrors))]
[JsonSerializable(typeof(AccountBinding))]
internal partial class AccountJsonContext : JsonSerializerContext;
