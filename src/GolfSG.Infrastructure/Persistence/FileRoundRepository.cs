using GolfSG.Core.Models;
using GolfSG.Services;

namespace GolfSG.Infrastructure.Persistence;

public sealed class FileRoundRepository : IRoundRepository
{
    private readonly RoundFileStore store;

    public FileRoundRepository(string appDataDirectory) : this(appDataDirectory, [])
    {
    }

    public FileRoundRepository(string appDataDirectory, IEnumerable<string> legacyAppDataDirectories)
    {
        store = new RoundFileStore(appDataDirectory, legacyAppDataDirectories);
    }

    public string ActiveStoragePath => store.ActiveStoragePath;
    public bool WasLastReadRecoveredFromBackup => store.WasLastReadRecoveredFromBackup;
    public bool WasLastReadMigratedFromLegacyStorage => store.WasLastReadMigratedFromLegacyStorage;
    public string? LastMigrationSourcePath => store.LastMigrationSourcePath;
    public bool WasLastUnreadableActiveFilePreserved => store.WasLastUnreadableActiveFilePreserved;
    public string? LastPreservedUnreadableFilePath => store.LastPreservedUnreadableFilePath;

    public Task<IReadOnlyList<Round>> GetRoundsAsync() => store.GetRoundsAsync();
    public Task<Round?> GetRoundAsync(string roundId) => store.GetRoundAsync(roundId);
    public Task SaveRoundAsync(Round round) => store.SaveRoundAsync(round);
    public Task DeleteRoundAsync(string roundId) => store.DeleteRoundAsync(roundId);
    public Task ExportRoundsAsync(string destinationPath) => store.ExportRoundsAsync(destinationPath);
    public Task ImportRoundsAsync(string sourcePath) => store.ImportRoundsAsync(sourcePath);
}
