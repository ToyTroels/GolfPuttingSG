using System.Text.Json;
using GolfSG.Core.Models;

namespace GolfSG.Core.Persistence;

public sealed class RoundFileStore
{
    private const string RoundsFileName = "rounds.json";
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly string backupFilePath;
    private readonly IReadOnlyList<string> legacyFilePaths;
    private readonly SemaphoreSlim mutationLock = new(1, 1);

    public RoundFileStore(string appDataDirectory, IEnumerable<string>? legacyAppDataDirectories = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);

        filePath = Path.Combine(appDataDirectory, RoundsFileName);
        backupFilePath = filePath + ".bak";
        legacyFilePaths = (legacyAppDataDirectories ?? [])
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(directory => Path.Combine(directory, RoundsFileName))
            .Where(path => !string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public string ActiveStoragePath => filePath;

    public string BackupStoragePath => backupFilePath;

    public bool WasLastReadRecoveredFromBackup { get; private set; }

    public bool WasLastReadMigratedFromLegacyStorage { get; private set; }

    public string? LastMigrationSourcePath { get; private set; }

    public bool WasLastUnreadableActiveFilePreserved { get; private set; }

    public string? LastPreservedUnreadableFilePath { get; private set; }

    public async Task<IReadOnlyList<Round>> GetRoundsAsync()
    {
        return await Task.Run(async () =>
        {
            ResetReadStatus();

            var activeRead = await ReadRoundsAsync(filePath);
            if (activeRead.Rounds is not null)
            {
                return SortNewestFirst(activeRead.Rounds, reverseTies: true);
            }

            var backupRead = await ReadRoundsAsync(backupFilePath);
            if (backupRead.Rounds is not null)
            {
                WasLastReadRecoveredFromBackup = true;
                return SortNewestFirst(backupRead.Rounds, reverseTies: true);
            }

            var migratedRounds = await TryMigrateFromLegacyStorageAsync();
            if (migratedRounds is not null)
            {
                return SortNewestFirst(migratedRounds, reverseTies: true);
            }

            return [];
        });
    }

    public async Task<Round?> GetRoundAsync(string roundId)
    {
        var rounds = await GetRoundsAsync();
        return rounds.FirstOrDefault(round => round.Id == roundId);
    }

    public async Task SaveRoundAsync(Round round)
    {
        await mutationLock.WaitAsync();
        try
        {
            await Task.Run(async () =>
            {
                var loadResult = await LoadRoundsForMutationAsync();
                var rounds = loadResult.Rounds.ToList();
                var existingIndex = rounds.FindIndex(existing => existing.Id == round.Id);
                if (existingIndex >= 0)
                {
                    rounds[existingIndex] = round;
                }
                else
                {
                    rounds.Insert(0, round);
                }

                await PreserveUnreadableActiveFileBeforeOverwriteAsync(loadResult);
                await WriteRoundsAsync(SortNewestFirst(rounds, reverseTies: false));
            });
        }
        finally
        {
            mutationLock.Release();
        }
    }

    public async Task DeleteRoundAsync(string roundId)
    {
        await mutationLock.WaitAsync();
        try
        {
            await Task.Run(async () =>
            {
                var loadResult = await LoadRoundsForMutationAsync();
                var rounds = SortNewestFirst(
                    loadResult.Rounds
                        .Where(round => round.Id != roundId)
                        .ToList(),
                    reverseTies: false);

                await PreserveUnreadableActiveFileBeforeOverwriteAsync(loadResult);
                await WriteRoundsAsync(rounds);
            });
        }
        finally
        {
            mutationLock.Release();
        }
    }

    public Task ExportRoundsAsync(string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        if (!File.Exists(filePath))
        {
            return File.WriteAllTextAsync(destinationPath, string.Empty);
        }

        File.Copy(filePath, destinationPath, overwrite: true);
        return Task.CompletedTask;
    }

    public async Task ImportRoundsAsync(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        await mutationLock.WaitAsync();
        try
        {
            await Task.Run(async () =>
            {
                var importRead = await ReadRoundsAsync(sourcePath);
                if (importRead.Rounds is null)
                {
                    throw new InvalidDataException("The selected rounds file could not be read.");
                }

                await WriteRoundsAsync(SortNewestFirst(importRead.Rounds, reverseTies: false));
            });
        }
        finally
        {
            mutationLock.Release();
        }
    }

    private async Task<RoundLoadResult> LoadRoundsForMutationAsync()
    {
        ResetReadStatus();

        var activeRead = await ReadRoundsAsync(filePath);
        if (activeRead.Rounds is not null)
        {
            return new RoundLoadResult(activeRead.Rounds, activeRead);
        }

        var backupRead = await ReadRoundsAsync(backupFilePath);
        if (backupRead.Rounds is not null)
        {
            WasLastReadRecoveredFromBackup = true;
            return new RoundLoadResult(backupRead.Rounds, activeRead);
        }

        var migratedRounds = await TryMigrateFromLegacyStorageAsync();
        return new RoundLoadResult(migratedRounds ?? [], activeRead);
    }

    private void ResetReadStatus()
    {
        WasLastReadRecoveredFromBackup = false;
        WasLastReadMigratedFromLegacyStorage = false;
        LastMigrationSourcePath = null;
        WasLastUnreadableActiveFilePreserved = false;
        LastPreservedUnreadableFilePath = null;
    }

    private async Task<IReadOnlyList<Round>?> TryMigrateFromLegacyStorageAsync()
    {
        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            return null;
        }

        foreach (var legacyFilePath in legacyFilePaths)
        {
            var legacyRead = await ReadRoundsAsync(legacyFilePath);
            if (legacyRead.Rounds is null)
            {
                continue;
            }

            await WriteRoundsAsync(SortNewestFirst(legacyRead.Rounds, reverseTies: false));
            WasLastReadMigratedFromLegacyStorage = true;
            LastMigrationSourcePath = legacyFilePath;
            return legacyRead.Rounds;
        }

        return null;
    }

    private static async Task<RoundReadResult> ReadRoundsAsync(string path)
    {
        if (!File.Exists(path))
        {
            return new RoundReadResult(null, false);
        }

        if (new FileInfo(path).Length == 0)
        {
            return new RoundReadResult(null, true);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var rounds = document.RootElement.Deserialize<List<Round>>(JsonOptions);
                return new RoundReadResult(rounds, false);
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("rounds", out var roundsElement) &&
                roundsElement.ValueKind == JsonValueKind.Array)
            {
                var rounds = roundsElement.Deserialize<List<Round>>(JsonOptions);
                return new RoundReadResult(rounds, false);
            }

            return new RoundReadResult(null, true);
        }
        catch (JsonException)
        {
            return new RoundReadResult(null, true);
        }
        catch (IOException)
        {
            return new RoundReadResult(null, true);
        }
    }

    private async Task WriteRoundsAsync(IReadOnlyList<Round> rounds)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        if (File.Exists(filePath) &&
            new FileInfo(filePath).Length > 0 &&
            (await ReadRoundsAsync(filePath)).Rounds is not null)
        {
            File.Copy(filePath, backupFilePath, overwrite: true);
        }

        var tempFilePath = Path.Combine(directory, $"{Path.GetFileName(filePath)}.tmp");
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, new RoundHistoryDocument(CurrentSchemaVersion, rounds), JsonOptions);
        }

        File.Move(tempFilePath, filePath, overwrite: true);
    }

    private async Task PreserveUnreadableActiveFileBeforeOverwriteAsync(RoundLoadResult loadResult)
    {
        if (!loadResult.ActiveRead.WasUnreadable ||
            !File.Exists(filePath) ||
            new FileInfo(filePath).Length == 0)
        {
            return;
        }

        var preservedFilePath = Path.Combine(
            Path.GetDirectoryName(filePath)!,
            $"{Path.GetFileName(filePath)}.unreadable.{DateTime.UtcNow:yyyyMMddHHmmssfff}");

        await using (var source = File.OpenRead(filePath))
        await using (var destination = File.Create(preservedFilePath))
        {
            await source.CopyToAsync(destination);
        }

        WasLastUnreadableActiveFilePreserved = true;
        LastPreservedUnreadableFilePath = preservedFilePath;
    }

    private static List<Round> SortNewestFirst(IReadOnlyList<Round> rounds, bool reverseTies)
    {
        var indexedRounds = rounds
            .Select((round, index) => new { Round = round, Index = index })
            .OrderByDescending(item => item.Round.Date);

        return (reverseTies
                ? indexedRounds.ThenByDescending(item => item.Index)
                : indexedRounds.ThenBy(item => item.Index))
            .Select(item => item.Round)
            .ToList();
    }

    private sealed record RoundHistoryDocument(
        int SchemaVersion,
        IReadOnlyList<Round> Rounds);

    private sealed record RoundReadResult(
        IReadOnlyList<Round>? Rounds,
        bool WasUnreadable);

    private sealed record RoundLoadResult(
        IReadOnlyList<Round> Rounds,
        RoundReadResult ActiveRead);
}
