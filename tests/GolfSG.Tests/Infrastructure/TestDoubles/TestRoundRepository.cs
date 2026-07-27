using GolfSG.Core.Models;
using GolfSG.Application.Services;

namespace GolfSG.Tests;

internal sealed class TestRoundRepository(IEnumerable<Round>? rounds = null) : IRoundRepository
{
    public List<Round> Rounds { get; } = rounds?.ToList() ?? [];
    public Round? SavedRound { get; private set; }
    public int SaveCallCount { get; private set; }
    public Exception? GetRoundException { get; set; }
    public Exception? SaveException { get; set; }
    public Func<Round, Task>? SaveHandler { get; set; }

    public string ActiveStoragePath => string.Empty;
    public bool WasLastReadRecoveredFromBackup => false;
    public bool WasLastReadMigratedFromLegacyStorage => false;
    public string? LastMigrationSourcePath => null;
    public bool WasLastUnreadableActiveFilePreserved => false;
    public string? LastPreservedUnreadableFilePath => null;

    public Task<IReadOnlyList<Round>> GetRoundsAsync() =>
        Task.FromResult<IReadOnlyList<Round>>(Rounds.ToList());

    public Task<Round?> GetRoundAsync(string roundId)
    {
        if (GetRoundException is not null)
        {
            return Task.FromException<Round?>(GetRoundException);
        }

        return Task.FromResult(Rounds.FirstOrDefault(round => round.Id == roundId));
    }

    public async Task SaveRoundAsync(Round round)
    {
        SaveCallCount++;
        if (SaveException is not null)
        {
            throw SaveException;
        }

        if (SaveHandler is not null)
        {
            await SaveHandler(round);
        }

        SavedRound = round;
        var index = Rounds.FindIndex(existing => existing.Id == round.Id);
        if (index >= 0)
        {
            Rounds[index] = round;
        }
        else
        {
            Rounds.Add(round);
        }
    }

    public Task DeleteRoundAsync(string roundId)
    {
        Rounds.RemoveAll(round => round.Id == roundId);
        return Task.CompletedTask;
    }

    public Task ExportRoundsAsync(string destinationPath) => Task.CompletedTask;
    public Task ImportRoundsAsync(string sourcePath) => Task.CompletedTask;
}
