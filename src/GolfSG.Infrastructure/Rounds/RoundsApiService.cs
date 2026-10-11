using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using GolfSG.Application.Products;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Application.Accounts;

namespace GolfSG.Infrastructure.Rounds;

public sealed class RoundsApiService(HttpClient client, IApiAccountService accounts) : IRoundsApiService
{
    public Task<IReadOnlyList<RemoteRound>> GetRoundsAsync(ApiSession session, CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<RemoteRound>>(async () =>
        {
            using var request = await CreateRequestAsync(session, HttpMethod.Get, "api/rounds", cancellationToken).ConfigureAwait(false);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            var rounds = await response.Content.ReadFromJsonAsync(RoundSyncJsonContext.Default.ListRemoteRound,
                cancellationToken).ConfigureAwait(false);
            if (rounds is null) throw new JsonException();
            foreach (var round in rounds) Validate(round);
            if (rounds.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != rounds.Count)
                throw new JsonException();
            return rounds;
        }, cancellationToken);

    public Task<RemoteRound> WriteAsync(ApiSession session, string id, RoundWriteRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
        {
            using var message = await CreateRequestAsync(session, HttpMethod.Put, $"api/rounds/{Uri.EscapeDataString(id)}", cancellationToken).ConfigureAwait(false);
            message.Content = JsonContent.Create(request, RoundSyncJsonContext.Default.RoundWriteRequest);
            using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            var round = await response.Content.ReadFromJsonAsync(RoundSyncJsonContext.Default.RemoteRound,
                cancellationToken).ConfigureAwait(false);
            Validate(round);
            if (round!.Id != id || round.Version != request.Version || (round.Round is null) != (request.Round is null))
                throw new JsonException();
            return round;
        }, cancellationToken);

    private async Task<HttpRequestMessage> CreateRequestAsync(ApiSession pinned, HttpMethod method, string path, CancellationToken token)
    {
        var current = await accounts.GetSessionAsync(pinned.Server, token).ConfigureAwait(false);
        if (current.SessionId != pinned.SessionId || current.AccountId != pinned.AccountId || current.ServerAddress != pinned.ServerAddress)
            throw new ApiAccountException("Kontoen blev ændret. Log ind og synkroniser igen.");
        return await accounts.WithSessionAsync(pinned, () =>
        {
            var request = new HttpRequestMessage(method, Address(current.Server, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
            return Task.FromResult(request);
        }, token).ConfigureAwait(false);
    }

    private static Uri Address(Uri server, string path)
    {
        if (!ProductsApiAddress.TryNormalize(server.AbsoluteUri, out var normalized))
            throw new RoundSyncException("Angiv serverens HTTPS-adresse under Indstillinger → Konto og server.");
        return new Uri(normalized!, path);
    }

    private static void Validate(RemoteRound? round)
    {
        if (round is null || !RoundSyncValidation.IsValidId(round.Id) || round.Version == Guid.Empty ||
            round.Round is { } payload && payload.Id != round.Id) throw new JsonException();
        if (round.Round is { } data) RoundSyncValidation.Validate(data);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new RoundSyncException("Din login-session er udløbet. Log ind igen via Konto og login på startskærmen.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new RoundSyncException("Runden blev ændret på serveren under synkronisering. Prøv igen.");
        if (!response.IsSuccessStatusCode)
            throw new RoundSyncException("Serveren kunne ikke synkronisere runder. Tjek at rundetabellen er oprettet, og prøv igen.");
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await action().ConfigureAwait(false); }
        catch (ApiAccountException ex) { throw new RoundSyncException(ex.Message, ex); }
        catch (HttpRequestException ex)
        { throw new RoundSyncException("Kan ikke kontakte serveren. Dine runder er stadig gemt på enheden. Prøv igen senere.", ex); }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        { throw new RoundSyncException("Serveren svarede ikke i tide. Prøv igen senere.", ex); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        { throw new RoundSyncException("Serveren sendte rundedata, der ikke kunne læses.", ex); }
    }
}
