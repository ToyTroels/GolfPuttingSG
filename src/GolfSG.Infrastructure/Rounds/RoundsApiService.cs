using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GolfSG.Application.Products;
using GolfSG.Application.Rounds.Sync;

namespace GolfSG.Infrastructure.Rounds;

public sealed class RoundsApiService(HttpClient client) : IRoundsApiService
{
    public Task<IReadOnlyList<RemoteRound>> GetRoundsAsync(Uri server, CancellationToken cancellationToken = default) =>
        ExecuteAsync<IReadOnlyList<RemoteRound>>(async () =>
        {
            using var response = await client.GetAsync(Address(server, "api/rounds"), cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            var rounds = await response.Content.ReadFromJsonAsync(RoundSyncJsonContext.Default.ListRemoteRound,
                cancellationToken).ConfigureAwait(false);
            if (rounds is null) throw new JsonException();
            foreach (var round in rounds) Validate(round);
            if (rounds.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != rounds.Count)
                throw new JsonException();
            return rounds;
        }, cancellationToken);

    public Task<RemoteRound> WriteAsync(Uri server, string id, RoundWriteRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(async () =>
        {
            using var response = await client.PutAsJsonAsync(Address(server, $"api/rounds/{Uri.EscapeDataString(id)}"),
                request, RoundSyncJsonContext.Default.RoundWriteRequest, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            var round = await response.Content.ReadFromJsonAsync(RoundSyncJsonContext.Default.RemoteRound,
                cancellationToken).ConfigureAwait(false);
            Validate(round);
            if (round!.Id != id || round.Version != request.Version || (round.Round is null) != (request.Round is null))
                throw new JsonException();
            return round;
        }, cancellationToken);

    private static Uri Address(Uri server, string path)
    {
        if (!ProductsApiAddress.TryNormalize(server.AbsoluteUri, out var normalized))
            throw new RoundSyncException("Angiv serverens HTTPS-adresse under Indstillinger → Produkter.");
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
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new RoundSyncException("Runden blev ændret på serveren under synkronisering. Prøv igen.");
        if (!response.IsSuccessStatusCode)
            throw new RoundSyncException("Serveren kunne ikke synkronisere runder. Tjek at rundetabellen er oprettet, og prøv igen.");
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await action().ConfigureAwait(false); }
        catch (HttpRequestException ex)
        { throw new RoundSyncException("Kan ikke kontakte serveren. Dine runder er stadig gemt på enheden. Prøv igen senere.", ex); }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        { throw new RoundSyncException("Serveren svarede ikke i tide. Prøv igen senere.", ex); }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        { throw new RoundSyncException("Serveren sendte rundedata, der ikke kunne læses.", ex); }
    }
}
