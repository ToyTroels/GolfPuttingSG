using GolfSG.Application.Services;

namespace GolfSG.Infrastructure.Persistence;

// File pickers can supply Android content URIs. Import their streams through the same merge pipeline as file imports.
public sealed class RoundImportService(IRoundRepository repository, string temporaryDirectory)
{
    public async Task ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectory);
        if (!source.CanRead) throw new ArgumentException("Importstrømmen skal kunne læses.", nameof(source));
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(temporaryDirectory);
        var path = Path.Combine(temporaryDirectory, "golfsg-import-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(target, 81920, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            await repository.ImportRoundsAsync(path).ConfigureAwait(false);
        }
        finally
        {
            // Never dispose the caller's source stream. A failed cleanup can be left to the app's cache cleanup.
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
