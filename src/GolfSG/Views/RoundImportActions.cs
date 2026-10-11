using GolfSG.Application.Rounds.Sync;
using GolfSG.Infrastructure.Persistence;
using Microsoft.Maui.Storage;

namespace GolfSG.Views;

internal static class RoundImportActions
{
    public static async Task<bool> ImportAsync(Page page, RoundImportService importer, Func<Task>? onImported = null,
        CancellationToken cancellationToken = default)
    {
        var imported = false;
        await page.RunActionOnceAsync(async () =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Vælg en eksporteret GolfSG-fil (rounds.json)" });
                if (file is null) return;
                cancellationToken.ThrowIfCancellationRequested();
                await using var input = await file.OpenReadAsync();
                await importer.ImportAsync(input, cancellationToken);
                imported = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
            catch (RoundSyncException ex)
            {
                if (!cancellationToken.IsCancellationRequested) await page.DisplayAlertAsync("Import fejlede", ex.Message, "OK");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                if (!cancellationToken.IsCancellationRequested)
                    await page.DisplayAlertAsync("Import fejlede", "Filen kunne ikke importeres. Vælg en rounds.json-fil eksporteret fra GolfSG. Din eksisterende historik er bevaret.", "OK");
            }
            if (!imported || cancellationToken.IsCancellationRequested) return;
            var message = "Runderne er gemt på enheden. Runder med samme ID beholder deres gemte version. Log ind og vælg Synkroniser i Historik for at sende dem til din konto.";
            try { if (onImported is not null) await onImported(); }
            catch (Exception) { message += "\nHistorikken kunne ikke opdateres på skærmen. Åbn Historik igen for at se runderne."; }
            if (!cancellationToken.IsCancellationRequested) await page.DisplayAlertAsync("Import fuldført", message, "OK");
        });
        return imported;
    }
}
