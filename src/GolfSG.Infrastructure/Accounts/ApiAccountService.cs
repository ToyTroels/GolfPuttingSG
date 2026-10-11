using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GolfSG.Application.Accounts;
using GolfSG.Application.Products;

namespace GolfSG.Infrastructure.Accounts;

public sealed class ApiAccountService(HttpClient client, ISecureApiSessionStore secureStore,
    IProductsApiSettings settings, string directory) : IApiAccountService
{
    private readonly SemaphoreSlim stateGate = new(1, 1);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private long generation;
    private int signedOut;
    public string BindingPath { get; } = Path.Combine(directory, "account-binding.json");
    public const string SignInRequired = "Åbn Konto og login på startskærmen for at logge ind og synkronisere.";
    private const string DifferentAccount = "Denne installations rundehistorik er knyttet til en anden konto eller server. Log ind med den oprindelige konto. Brug en separat installation til en anden konto.";

    public Task<ApiConnectionStatus> CheckConnectionAsync(Uri server, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            // A connection check carries no account credentials and does not read or change installation data.
            var normalized = Normalize(server);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(normalized, "api/health"));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is not HttpStatusCode.OK and not HttpStatusCode.ServiceUnavailable)
                throw ConnectionError(response.StatusCode);
            if (!IsJson(response)) throw ConnectionError(response.StatusCode);
            ApiHealth? health;
            try
            {
                health = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.ApiHealth, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException) { throw ConnectionError(response.StatusCode); }
            if (health is null || health.Service != "GolfSG.Api" ||
                response.StatusCode == HttpStatusCode.OK && !health.DatabaseAvailable ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable && health.DatabaseAvailable)
                throw ConnectionError(response.StatusCode);
            return new ApiConnectionStatus(health.DatabaseAvailable);
        }, cancellationToken);

    public Task<ApiSession?> LoadAccountAsync(Uri server, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            var normalized = Normalize(server);
            await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureAddress(normalized);
                var binding = await ReadBindingAsync().ConfigureAwait(false);
                if (binding is not null && binding.ServerAddress != normalized.AbsoluteUri)
                    throw new ApiAccountException(DifferentAccount);
                var session = await ReadSessionAsync().ConfigureAwait(false);
                if (Volatile.Read(ref signedOut) != 0) return null;
                if (session is null) return null;
                EnsureBinding(binding, session);
                if (session.ServerAddress != normalized.AbsoluteUri) throw new ApiAccountException(DifferentAccount);
                return session;
            }
            finally { stateGate.Release(); }
        }, cancellationToken);

    public Task<ApiSession> RegisterAsync(Uri server, string username, string password,
        CancellationToken cancellationToken = default) => AuthenticateAsync(server, username, password, true, cancellationToken);

    public Task<ApiSession> SignInAsync(Uri server, string username, string password,
        CancellationToken cancellationToken = default) => AuthenticateAsync(server, username, password, false, cancellationToken);

    private Task<ApiSession> AuthenticateAsync(Uri server, string username, string password, bool register,
        CancellationToken token) => ExecuteAsync(async () =>
        {
            var normalized = Normalize(server);
            EnsureAddress(normalized);
            username = username.Trim();
            if (username.Length is < 3 or > 50 || username.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-' and not '.'))
                throw new ApiAccountException("Brugernavnet skal være 3–50 tegn: bogstaver, tal, punktum, bindestreg eller understregning.");
            if (string.IsNullOrWhiteSpace(password)) throw new ApiAccountException("Indtast din adgangskode.");
            if (register && (password.Length < ApiPasswordPolicy.MinimumLength || password.Length > ApiPasswordPolicy.MaximumLength))
                throw new ApiAccountException("Adgangskoden skal være " + ApiPasswordPolicy.RegistrationDescription);
            var attempt = Interlocked.Increment(ref generation);
            // Reject another server before sending any password to it.
            await stateGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var binding = await ReadBindingAsync().ConfigureAwait(false);
                if (binding is not null && binding.ServerAddress != normalized.AbsoluteUri)
                    throw new ApiAccountException(DifferentAccount);
                if (register && binding is not null)
                    throw new ApiAccountException("Denne installation er allerede knyttet til en konto. Brug Log ind med din oprindelige konto.");
            }
            finally { stateGate.Release(); }
            var credentials = new AccountCredentials(username, password);
            if (register)
            {
                using var registered = await client.PostAsJsonAsync(new Uri(normalized, "api/auth/register"), credentials,
                    AccountJsonContext.Default.AccountCredentials, token).ConfigureAwait(false);
                if (!registered.IsSuccessStatusCode)
                    throw await RegistrationErrorAsync(registered, token).ConfigureAwait(false);
                EnsureAttempt(attempt, normalized);
            }
            using var response = await client.PostAsJsonAsync(new Uri(normalized, "api/auth/login"), credentials,
                AccountJsonContext.Default.AccountCredentials, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw response.StatusCode == HttpStatusCode.Unauthorized
                ? new ApiAccountException("Brugernavn eller adgangskode er forkert. Prøv igen.")
                : AuthError(response.StatusCode);
            var tokens = await ReadTokensAsync(response, token).ConfigureAwait(false);
            EnsureAttempt(attempt, normalized);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(normalized, "api/auth/me"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            using var identityResponse = await client.SendAsync(request, token).ConfigureAwait(false);
            if (!identityResponse.IsSuccessStatusCode) throw identityResponse.StatusCode == HttpStatusCode.Unauthorized
                ? new ApiAccountException("Serveren kunne ikke bekræfte din konto. Prøv at logge ind igen.")
                : AuthError(identityResponse.StatusCode);
            var identity = await identityResponse.Content.ReadFromJsonAsync(AccountJsonContext.Default.AccountIdentity, token).ConfigureAwait(false);
            if (identity is null || string.IsNullOrWhiteSpace(identity.Id) || string.IsNullOrWhiteSpace(identity.Username)) throw new JsonException();
            var candidate = new ApiSession(Guid.NewGuid(), identity.Id, identity.Username, normalized.AbsoluteUri,
                tokens.AccessToken, tokens.RefreshToken, DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn));
            await stateGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                EnsureAttempt(attempt, normalized);
                var binding = await ReadBindingAsync().ConfigureAwait(false);
                if (binding is not null && (binding.ServerAddress != candidate.ServerAddress || binding.AccountId != candidate.AccountId))
                    throw new ApiAccountException(DifferentAccount);
                if (binding is null) await WriteBindingAsync(new(1, candidate.ServerAddress, candidate.AccountId)).ConfigureAwait(false);
                await SaveSessionAsync(candidate).ConfigureAwait(false);
                Volatile.Write(ref signedOut, 0);
                return candidate;
            }
            finally { stateGate.Release(); }
        }, token);

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref generation);
        Volatile.Write(ref signedOut, 1);
        return ExecuteAsync(async () =>
        {
            await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await secureStore.DeleteAsync().ConfigureAwait(false); return true; }
            finally { stateGate.Release(); }
        }, cancellationToken);
    }

    public Task<ApiSession> GetSessionAsync(Uri server, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var attempt = Volatile.Read(ref generation);
                var session = await LoadAccountAsync(server, cancellationToken).ConfigureAwait(false)
                    ?? throw new ApiAccountException(SignInRequired);
                // Allow for the two-minute HTTP window while an idle API starts and responds.
                if (session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2)) return session;
                using var response = await client.PostAsJsonAsync(new Uri(session.Server, "api/auth/refresh"),
                    new AccountRefresh(session.RefreshToken), AccountJsonContext.Default.AccountRefresh, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw response.StatusCode == HttpStatusCode.Unauthorized
                    ? new ApiAccountException("Din login-session er udløbet. Log ind igen via Konto og login på startskærmen.")
                    : AuthError(response.StatusCode);
                var tokens = await ReadTokensAsync(response, cancellationToken).ConfigureAwait(false);
                var updated = session with { AccessToken = tokens.AccessToken, RefreshToken = tokens.RefreshToken,
                    ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn) };
                await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    EnsureAttempt(attempt, session.Server);
                    await EnsureCurrentAsync(session).ConfigureAwait(false);
                    await SaveSessionAsync(updated).ConfigureAwait(false);
                    return updated;
                }
                finally { stateGate.Release(); }
            }
            finally { refreshGate.Release(); }
        }, cancellationToken);

    public Task<T> WithSessionAsync<T>(ApiSession session, Func<Task<T>> action, CancellationToken cancellationToken = default) =>
        ExecuteAsync(async () =>
        {
            await stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnsureCurrentAsync(session).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return await action().ConfigureAwait(false);
            }
            finally { stateGate.Release(); }
        }, cancellationToken);

    private async Task EnsureCurrentAsync(ApiSession pinned)
    {
        EnsureAddress(pinned.Server);
        var current = await ReadSessionAsync().ConfigureAwait(false);
        if (Volatile.Read(ref signedOut) != 0 || current is null || current.SessionId != pinned.SessionId || current.AccountId != pinned.AccountId || current.ServerAddress != pinned.ServerAddress)
            throw new ApiAccountException("Kontoen blev ændret eller logget ud. Log ind og synkroniser igen.");
        EnsureBinding(await ReadBindingAsync().ConfigureAwait(false), current);
    }

    private void EnsureAddress(Uri server)
    {
        if (!ProductsApiAddress.TryNormalize(settings.BaseAddress, out var current) || current != server)
            throw new ApiAccountException("Serveradressen blev ændret. Vælg din kontos server under Indstillinger → Konto og server.");
    }

    private void EnsureAttempt(long attempt, Uri server)
    {
        if (Volatile.Read(ref generation) != attempt) throw new ApiAccountException("Login blev afbrudt. Prøv igen.");
        EnsureAddress(server);
    }

    private static Uri Normalize(Uri server) => ProductsApiAddress.TryNormalize(server.AbsoluteUri, out var normalized)
        ? normalized! : throw new ApiAccountException("Angiv en gyldig HTTPS-serveradresse.");

    private static void EnsureBinding(AccountBinding? binding, ApiSession session)
    {
        if (binding is null || binding.AccountId != session.AccountId || binding.ServerAddress != session.ServerAddress)
            throw new ApiAccountException(DifferentAccount);
    }

    private async Task<ApiSession?> ReadSessionAsync()
    {
        var json = await secureStore.ReadAsync().ConfigureAwait(false);
        if (json is null) return null;
        var session = JsonSerializer.Deserialize(json, AccountJsonContext.Default.ApiSession);
        if (session is null || session.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(session.AccountId) ||
            string.IsNullOrWhiteSpace(session.Username) || string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken) || !ProductsApiAddress.TryNormalize(session.ServerAddress, out var server) ||
            server!.AbsoluteUri != session.ServerAddress || session.ExpiresAt == default) throw new JsonException();
        return session;
    }

    private Task SaveSessionAsync(ApiSession session) =>
        secureStore.WriteAsync(JsonSerializer.Serialize(session, AccountJsonContext.Default.ApiSession));

    private async Task<AccountBinding?> ReadBindingAsync()
    {
        if (!File.Exists(BindingPath)) return null;
        var binding = JsonSerializer.Deserialize(await File.ReadAllTextAsync(BindingPath).ConfigureAwait(false), AccountJsonContext.Default.AccountBinding);
        if (binding is null || binding.SchemaVersion != 1 || string.IsNullOrWhiteSpace(binding.AccountId) ||
            !ProductsApiAddress.TryNormalize(binding.ServerAddress, out var address) || address!.AbsoluteUri != binding.ServerAddress)
            throw new JsonException();
        return binding;
    }

    private async Task WriteBindingAsync(AccountBinding binding)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BindingPath)!);
        var temporary = BindingPath + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(binding, AccountJsonContext.Default.AccountBinding);
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                         4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, BindingPath, overwrite: false);
    }

    private static async Task<AccountTokens> ReadTokensAsync(HttpResponseMessage response, CancellationToken token)
    {
        var tokens = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.AccountTokens, token).ConfigureAwait(false);
        if (tokens is null || !string.Equals(tokens.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(tokens.AccessToken) || string.IsNullOrWhiteSpace(tokens.RefreshToken) ||
            tokens.ExpiresIn is < 1 or > 86400) throw new JsonException();
        return tokens;
    }

    private static bool IsJson(HttpResponseMessage response) =>
        string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(response.Content.Headers.ContentType?.MediaType, "application/problem+json", StringComparison.OrdinalIgnoreCase);

    private static ApiAccountException ConnectionError(HttpStatusCode status) => new(status switch
    {
        HttpStatusCode.NotFound => "Forbindelsestjekket findes ikke (HTTP 404). Kontrollér serveradressen og genudgiv den nyeste GolfSG.Api.",
        HttpStatusCode.MethodNotAllowed => "Serveradressen accepterer ikke forbindelsestjekket (HTTP 405). Kontrollér API-adressen og IIS-konfigurationen.",
        HttpStatusCode.TooManyRequests => "Serveren har modtaget for mange forespørgsler (HTTP 429). Vent lidt, og prøv igen.",
        _ => $"Adressen svarede ikke med den forventede GolfSG API-status (HTTP {(int)status}). Kontrollér serveradressen, IIS og at den nyeste GolfSG.Api er udgivet."
    });

    private static ApiAccountException AuthError(HttpStatusCode status) => new(status switch
    {
        HttpStatusCode.NotFound => "Serveren mangler konto-API'et (HTTP 404). Kontrollér serveradressen og genudgiv den nyeste GolfSG.Api.",
        HttpStatusCode.MethodNotAllowed => "Serveradressen accepterer ikke denne kontohandling (HTTP 405). Kontrollér API-adressen og IIS-konfigurationen.",
        HttpStatusCode.TooManyRequests => "For mange loginforsøg (HTTP 429). Vent lidt, og prøv igen.",
        _ when (int)status >= 500 => $"Serverfejl (HTTP {(int)status}). Kontrollér IIS-loggen og API'ens databaseopsætning.",
        _ => $"Serveren kunne ikke fuldføre kontohandlingen (HTTP {(int)status}). Kontrollér oplysningerne og serveradressen."
    });

    private static async Task<ApiAccountException> RegistrationErrorAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.StatusCode is not HttpStatusCode.BadRequest and not HttpStatusCode.Conflict) return AuthError(response.StatusCode);
        // Display only our translations of known Identity codes. Server descriptions, stack traces and input values are ignored.
        if (IsJson(response))
        {
            try
            {
                var validation = await response.Content.ReadFromJsonAsync(AccountJsonContext.Default.AccountValidationErrors, token).ConfigureAwait(false);
                if (validation?.Errors is { } errors)
                {
                    var messages = errors.Keys.Select(code => code switch
                    {
                        "DuplicateUserName" => "Brugernavnet er allerede i brug. Vælg et andet.",
                        "InvalidUserName" => "Brugernavnet er ikke gyldigt. Brug bogstaver, tal, punktum, bindestreg eller understregning.",
                        "PasswordTooShort" => "Adgangskoden er for kort til serverens krav.",
                        "PasswordRequiresNonAlphanumeric" => "Adgangskoden skal indeholde et specialtegn.",
                        "PasswordRequiresDigit" => "Adgangskoden skal indeholde et tal.",
                        "PasswordRequiresLower" => "Adgangskoden skal indeholde et lille bogstav.",
                        "PasswordRequiresUpper" => "Adgangskoden skal indeholde et stort bogstav.",
                        "PasswordRequiresUniqueChars" => "Adgangskoden skal indeholde flere forskellige tegn.",
                        _ => null
                    }).OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
                    if (messages.Length > 0) return new(string.Join(" ", messages));
                }
            }
            catch (JsonException) { }
        }
        return new("Kontoen kunne ikke oprettes. Brugernavnet kan være optaget, eller adgangskoden opfylder ikke serverens krav.");
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try { return await action().ConfigureAwait(false); }
        catch (ApiAccountException) { throw; }
        catch (HttpRequestException ex) { throw new ApiAccountException("Kan ikke kontakte serveren. Dine lokale runder er bevaret.", ex); }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested) { throw new ApiAccountException("Serveren svarede ikke i tide. Prøv igen.", ex); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { throw new ApiAccountException("Kontodata kunne ikke læses eller gemmes sikkert. Dine lokale runder er bevaret. Prøv at logge ind igen.", ex); }
    }
}
