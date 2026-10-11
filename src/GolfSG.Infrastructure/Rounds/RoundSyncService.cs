using System.Security.Cryptography;
using System.Text.Json;
using GolfSG.Application.Products;
using GolfSG.Application.Accounts;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Core.Models;

namespace GolfSG.Infrastructure.Rounds;

public sealed class RoundSyncService(SyncingRoundRepository repository, RoundSyncStateStore stateStore,
    IRoundsApiService api, IProductsApiSettings settings, IApiAccountService accounts) : IRoundSyncService
{
    private readonly SemaphoreSlim syncGate = new(1, 1);

    public async Task<RoundSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!ProductsApiAddress.TryNormalize(settings.BaseAddress, out var server))
            throw new RoundSyncException("Angiv serverens HTTPS-adresse under Indstillinger → Konto og server.");

        await syncGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Pin the verified account and server for the entire sync. Logout stops local mutations.
            var session = await accounts.GetSessionAsync(server!, cancellationToken).ConfigureAwait(false);
            var remote = (await api.GetRoundsAsync(session, cancellationToken).ConfigureAwait(false))
                .ToDictionary(r => r.Id, StringComparer.Ordinal);
            var local = await repository.GetRoundsAsync().ConfigureAwait(false);
            var ids = local.Select(r => r.Id).Union(remote.Keys, StringComparer.Ordinal).ToArray();
            var uploaded = 0;
            var downloaded = 0;
            var deleted = 0;

            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (round, syncedHash, wasDeleted) = await ReadLocalAsync(session, id, cancellationToken).ConfigureAwait(false);
                remote.TryGetValue(id, out var cloud);

                if (round is not null)
                {
                    RoundSyncValidation.Validate(round);
                    var hash = Hash(round);
                    var remoteHash = cloud?.Round is { } remoteRound ? Hash(remoteRound) : null;
                    if (cloud?.Round is not null && hash == remoteHash)
                    {
                        await AcknowledgeAsync(session, id, hash).ConfigureAwait(false);
                    }
                    else if (cloud is not null && syncedHash == hash && !wasDeleted)
                    {
                        // Download a server change only if this device has no unsynced edits.
                        if (await ApplyRemoteAsync(session, cloud, hash, cancellationToken).ConfigureAwait(false))
                        {
                            if (cloud.Round is null) deleted++; else downloaded++;
                        }
                    }
                    else
                    {
                        // A first sync or changes on both sides retain the local round. CAS guards the read/write gap.
                        await api.WriteAsync(session, id, new(Guid.NewGuid(), cloud?.Version, round), cancellationToken)
                            .ConfigureAwait(false);
                        await AcknowledgeAsync(session, id, hash).ConfigureAwait(false);
                        uploaded++;
                    }
                }
                else if (cloud?.Round is not null)
                {
                    if (wasDeleted)
                    {
                        await api.WriteAsync(session, id, new(Guid.NewGuid(), cloud.Version, null), cancellationToken)
                            .ConfigureAwait(false);
                        await AcknowledgeAsync(session, id, null).ConfigureAwait(false);
                        deleted++;
                    }
                    else if (await ApplyRemoteAsync(session, cloud, null, cancellationToken).ConfigureAwait(false))
                    {
                        downloaded++;
                    }
                }
            }

            return await accounts.WithSessionAsync(session, () => Task.FromResult(new RoundSyncResult(uploaded, downloaded, deleted)), cancellationToken).ConfigureAwait(false);
        }
        catch (ApiAccountException ex) { throw new RoundSyncException(ex.Message, ex); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { throw new RoundSyncException("Synkronisering kunne ikke fuldføres. Dine lokale runder er bevaret. Prøv igen senere.", ex); }
        finally { syncGate.Release(); }
    }

    private Task<(Round? Round, string? Hash, bool Deleted)> ReadLocalAsync(ApiSession session, string id, CancellationToken token) =>
        accounts.WithSessionAsync(session, () => ReadLocalCoreAsync(session, id, token), token);

    private Task<bool> ApplyRemoteAsync(ApiSession session, RemoteRound cloud, string? hash, CancellationToken token) =>
        accounts.WithSessionAsync(session, () => ApplyRemoteCoreAsync(session, cloud, hash, token), token);

    private Task AcknowledgeAsync(ApiSession session, string id, string? hash) =>
        accounts.WithSessionAsync(session, async () => { await AcknowledgeCoreAsync(session, id, hash).ConfigureAwait(false); return true; });

    private async Task<(Round? Round, string? Hash, bool Deleted)> ReadLocalCoreAsync(ApiSession session, string id, CancellationToken token)
    {
        await repository.MutationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = await stateStore.LoadAsync().ConfigureAwait(false);
            string? hash = null;
            if (state.Servers.TryGetValue(session.SyncScope, out var hashes)) hashes.TryGetValue(id, out hash);
            return (await repository.Local.GetRoundAsync(id).ConfigureAwait(false), hash, state.DeletedIds.Contains(id));
        }
        finally { repository.MutationGate.Release(); }
    }

    private async Task<bool> ApplyRemoteCoreAsync(ApiSession session, RemoteRound cloud, string? expectedLocalHash, CancellationToken token)
    {
        await repository.MutationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = await stateStore.LoadAsync().ConfigureAwait(false);
            var current = await repository.Local.GetRoundAsync(cloud.Id).ConfigureAwait(false);
            // Saving/deleting while a request is running must never be undone by its older snapshot.
            if ((current is null ? null : Hash(current)) != expectedLocalHash ||
                current is null && state.DeletedIds.Contains(cloud.Id)) return false;

            if (cloud.Round is null) await repository.Local.DeleteRoundAsync(cloud.Id).ConfigureAwait(false);
            else await repository.Local.SaveRoundAsync(cloud.Round).ConfigureAwait(false);
            SetHash(state, session, cloud.Id, cloud.Round is null ? null : Hash(cloud.Round));
            state.DeletedIds.Remove(cloud.Id);
            await stateStore.SaveAsync(state).ConfigureAwait(false);
            return true;
        }
        finally { repository.MutationGate.Release(); }
    }

    private async Task AcknowledgeCoreAsync(ApiSession session, string id, string? hash)
    {
        await repository.MutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = await stateStore.LoadAsync().ConfigureAwait(false);
            var unchanged = state.Servers.TryGetValue(session.SyncScope, out var existing) &&
                existing.TryGetValue(id, out var oldHash) && oldHash == hash;
            SetHash(state, session, id, hash);
            // Do not clear a deletion made while the upload was in flight.
            var clearedDeletion = await repository.Local.GetRoundAsync(id).ConfigureAwait(false) is not null &&
                state.DeletedIds.Remove(id);
            if (!unchanged || clearedDeletion) await stateStore.SaveAsync(state).ConfigureAwait(false);
        }
        finally { repository.MutationGate.Release(); }
    }

    private static void SetHash(RoundSyncState state, ApiSession session, string id, string? hash)
    {
        if (!state.Servers.TryGetValue(session.SyncScope, out var hashes))
            state.Servers[session.SyncScope] = hashes = new(StringComparer.Ordinal);
        hashes[id] = hash;
    }

    private static string Hash(Round round) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(round, RoundSyncJsonContext.Default.Round)));
}
