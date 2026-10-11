using System.Text.Json.Serialization;

namespace GolfSG.Application.Accounts;

// Passwords are used only for the request. A stored session contains opaque server tokens.
public sealed record ApiSession(Guid SessionId, string AccountId, string Username, string ServerAddress,
    string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    [JsonIgnore] public Uri Server => new(ServerAddress);
    [JsonIgnore] public string SyncScope => ServerAddress + "|" + AccountId;
    // Avoid accidentally printing bearer credentials through record.ToString().
    public override string ToString() => $"{Username} ({ServerAddress})";
}

public interface IApiAccountService
{
    Task<ApiConnectionStatus> CheckConnectionAsync(Uri server, CancellationToken cancellationToken = default);
    Task<ApiSession?> LoadAccountAsync(Uri server, CancellationToken cancellationToken = default);
    Task<ApiSession> RegisterAsync(Uri server, string username, string password, CancellationToken cancellationToken = default);
    Task<ApiSession> SignInAsync(Uri server, string username, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task<ApiSession> GetSessionAsync(Uri server, CancellationToken cancellationToken = default);
    // Serializes a short local operation with logout; no network work belongs in this callback.
    Task<T> WithSessionAsync<T>(ApiSession session, Func<Task<T>> action, CancellationToken cancellationToken = default);
}

public sealed record ApiConnectionStatus(bool DatabaseAvailable);

public interface ISecureApiSessionStore
{
    Task<string?> ReadAsync();
    Task WriteAsync(string sessionJson);
    Task DeleteAsync();
}

public sealed class ApiAccountException(string message, Exception? innerException = null) : Exception(message, innerException);
