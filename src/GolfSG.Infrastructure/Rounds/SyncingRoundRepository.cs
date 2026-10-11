using GolfSG.Application.Services;
using GolfSG.Application.Rounds.Sync;
using GolfSG.Core.Models;

namespace GolfSG.Infrastructure.Rounds;

// All round input still writes to the existing file store without contacting the server.
public sealed class SyncingRoundRepository(IRoundRepository local, RoundSyncStateStore state) : IRoundRepository
{
    internal IRoundRepository Local => local;
    internal SemaphoreSlim MutationGate { get; } = new(1, 1);
    public string ActiveStoragePath => local.ActiveStoragePath;
    public bool WasLastReadRecoveredFromBackup => local.WasLastReadRecoveredFromBackup;
    public bool WasLastReadMigratedFromLegacyStorage => local.WasLastReadMigratedFromLegacyStorage;
    public string? LastMigrationSourcePath => local.LastMigrationSourcePath;
    public bool WasLastUnreadableActiveFilePreserved => local.WasLastUnreadableActiveFilePreserved;
    public string? LastPreservedUnreadableFilePath => local.LastPreservedUnreadableFilePath;
    public Task<IReadOnlyList<Round>> GetRoundsAsync() => local.GetRoundsAsync();
    public Task<Round?> GetRoundAsync(string roundId) => local.GetRoundAsync(roundId);
    public Task ExportRoundsAsync(string destinationPath) => local.ExportRoundsAsync(destinationPath);

    public async Task SaveRoundAsync(Round round)
    {
        await MutationGate.WaitAsync().ConfigureAwait(false);
        try { await local.SaveRoundAsync(round).ConfigureAwait(false); }
        finally { MutationGate.Release(); }
    }

    public async Task DeleteRoundAsync(string roundId)
    {
        await MutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            // Persist the intent first, including rounds never uploaded. A lost upload response is safe.
            // If the file deletion fails, the still-present local round takes precedence at the next sync.
            if (RoundSyncValidation.IsValidId(roundId))
            {
                var document = await state.LoadAsync().ConfigureAwait(false);
                document.DeletedIds.Add(roundId);
                await state.SaveAsync(document).ConfigureAwait(false);
            }
            await local.DeleteRoundAsync(roundId).ConfigureAwait(false);
        }
        finally { MutationGate.Release(); }
    }

    public async Task ImportRoundsAsync(string sourcePath)
    {
        await MutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var document = await state.LoadAsync().ConfigureAwait(false);
            var previousIds = (await local.GetRoundsAsync().ConfigureAwait(false)).Select(round => round.Id)
                .ToHashSet(StringComparer.Ordinal);
            await local.ImportRoundsAsync(sourcePath).ConfigureAwait(false);
            var restoredIds = (await local.GetRoundsAsync().ConfigureAwait(false))
                .Where(round => !previousIds.Contains(round.Id) && document.DeletedIds.Contains(round.Id))
                .Select(round => round.Id).ToArray();
            if (restoredIds.Length == 0) return;
            foreach (var id in restoredIds)
            {
                document.DeletedIds.Remove(id);
                // An explicitly restored export is new local work even if it matches a previously synced copy.
                // Old hashes must not allow a retained server deletion to remove the restored round again.
                foreach (var hashes in document.Servers.Values) hashes.Remove(id);
            }
            await state.SaveAsync(document).ConfigureAwait(false);
        }
        finally { MutationGate.Release(); }
    }
}
