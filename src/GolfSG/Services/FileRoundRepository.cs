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

    public FileRoundRepository()
    {
        filePath = Path.Combine(FileSystem.AppDataDirectory, "rounds.json");
    }

    public async Task<IReadOnlyList<Round>> GetRoundsAsync()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        var rounds = await JsonSerializer.DeserializeAsync<List<Round>>(stream, JsonOptions);
        return rounds?
            .OrderByDescending(round => round.Date)
            .ToList() ?? [];
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
            rounds.Add(round);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, rounds.OrderByDescending(r => r.Date).ToList(), JsonOptions);
    }

    public async Task DeleteRoundAsync(string roundId)
    {
        var rounds = (await GetRoundsAsync())
            .Where(round => round.Id != roundId)
            .OrderByDescending(round => round.Date)
            .ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, rounds, JsonOptions);
    }
}
