using GolfSG.Application.Accounts;
using GolfSG.Application.Products;

namespace GolfSG.Tests.Infrastructure.TestDoubles;

internal sealed class TestApiAccountService(IProductsApiSettings? settings = null) : IApiAccountService
{
    private readonly Guid sessionId = Guid.NewGuid();
    public bool SignedOut { get; set; }
    public string AccountId { get; set; } = "test-user";
    public string AccessToken { get; set; } = "test-access-token";
    public Exception? LoadAccountFailure { get; set; }
    public ApiConnectionStatus ConnectionStatus { get; set; } = new(true);
    public List<Uri> CheckedServers { get; } = [];
    public Func<Uri, CancellationToken, Task<ApiConnectionStatus>>? ConnectionCheck { get; set; }

    public ApiSession Session(Uri server) => new(sessionId, AccountId, "tester", server.AbsoluteUri,
        AccessToken, "test-refresh-token", DateTimeOffset.UtcNow.AddHours(1));
    public Task<ApiSession?> LoadAccountAsync(Uri server, CancellationToken cancellationToken = default) =>
        LoadAccountFailure is null ? Task.FromResult<ApiSession?>(SignedOut ? null : Session(server))
            : Task.FromException<ApiSession?>(LoadAccountFailure);
    public Task<ApiConnectionStatus> CheckConnectionAsync(Uri server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CheckedServers.Add(server);
        return ConnectionCheck?.Invoke(server, cancellationToken) ?? Task.FromResult(ConnectionStatus);
    }
    public Task<ApiSession> GetSessionAsync(Uri server, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Check(Session(server));
        return Task.FromResult(Session(server));
    }
    public Task<ApiSession> RegisterAsync(Uri server, string username, string password, CancellationToken cancellationToken = default) => SignInAsync(server, username, password, cancellationToken);
    public Task<ApiSession> SignInAsync(Uri server, string username, string password, CancellationToken cancellationToken = default)
    { SignedOut = false; return Task.FromResult(Session(server) with { Username = username }); }
    public Task SignOutAsync(CancellationToken cancellationToken = default) { SignedOut = true; return Task.CompletedTask; }
    public Task<T> WithSessionAsync<T>(ApiSession session, Func<Task<T>> action, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); Check(session); return action(); }
    private void Check(ApiSession session)
    {
        if (SignedOut || session.AccountId != AccountId) throw new ApiAccountException("Log ind igen.");
        if (settings is not null && (!ProductsApiAddress.TryNormalize(settings.BaseAddress, out var address) || address!.AbsoluteUri != session.ServerAddress))
            throw new ApiAccountException("Serveradressen blev ændret.");
    }
}
