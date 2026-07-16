using GolfSG.Core.Models;

namespace GolfSG.Services;

public interface IRoundRepository
{
    string ActiveStoragePath { get; }
    bool WasLastReadRecoveredFromBackup { get; }
    bool WasLastReadMigratedFromLegacyStorage { get; }
    string? LastMigrationSourcePath { get; }
    bool WasLastUnreadableActiveFilePreserved { get; }
    string? LastPreservedUnreadableFilePath { get; }
    Task<IReadOnlyList<Round>> GetRoundsAsync();
    Task<Round?> GetRoundAsync(string roundId);
    Task SaveRoundAsync(Round round);
    Task DeleteRoundAsync(string roundId);
    Task ExportRoundsAsync(string destinationPath);
    Task ImportRoundsAsync(string sourcePath);
}
