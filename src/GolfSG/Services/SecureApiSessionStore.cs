using GolfSG.Application.Accounts;

namespace GolfSG.Services;

public sealed class SecureApiSessionStore : ISecureApiSessionStore
{
    private const string Key = "golfsg.api.session.v1";
    public Task<string?> ReadAsync() => SecureStorage.Default.GetAsync(Key);
    public Task WriteAsync(string sessionJson) => SecureStorage.Default.SetAsync(Key, sessionJson);
    public Task DeleteAsync() { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}
