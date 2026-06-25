using System.Text.Json;
using GolfSG.Core.Models;

namespace GolfSG.Services;

public sealed class FileRoundRepository : IRoundRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly string backupFilePath;

    public FileRoundRepository()
    {
        filePath = Path.Combine(FileSystem.AppDataDirectory, "rounds.json");
        backupFilePath = filePath + ".bak";
    }

    public async Task<IReadOnlyList<Round>> GetRoundsAsync()
    {
        var rounds = await ReadRoundsOrDefaultAsync(filePath) ??
            await ReadRoundsOrDefaultAsync(backupFilePath);
        return SortNewestFirst(rounds ?? [], reverseTies: true);
    }

    public async Task<Round?> GetRoundAsync(string roundId)
    {
        var rounds = await GetRoundsAsync();
        return rounds.FirstOrDefault(round => round.Id == roundId);
    }

    public async Task SaveRoundAsync(Round round)
    {
        var rounds = (await GetRoundsAsync()).ToList();
        var existingIndex = rounds.FindIndex(existing => existing.Id == round.Id);
        if (existingIndex >= 0)
        {
            rounds[existingIndex] = round;
        }
        else
        {
            rounds.Insert(0, round);
        }

        await WriteRoundsAsync(SortNewestFirst(rounds, reverseTies: false));
    }

    public async Task DeleteRoundAsync(string roundId)
    {
        var rounds = SortNewestFirst(
            (await GetRoundsAsync())
                .Where(round => round.Id != roundId)
                .ToList(),
            reverseTies: false);

        await WriteRoundsAsync(rounds);
    }

    private static async Task<List<Round>?> ReadRoundsOrDefaultAsync(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<Round>>(stream, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task WriteRoundsAsync(IReadOnlyList<Round> rounds)
    {
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            File.Copy(filePath, backupFilePath, overwrite: true);
        }

        var tempFilePath = Path.Combine(directory, $"{Path.GetFileName(filePath)}.tmp");
        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, rounds, JsonOptions);
        }

        File.Move(tempFilePath, filePath, overwrite: true);
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
}
