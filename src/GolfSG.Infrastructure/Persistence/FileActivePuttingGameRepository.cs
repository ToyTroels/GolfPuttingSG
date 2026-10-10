using System.Text.Json;
using GolfSG.Application.Putting;
using GolfSG.Application.Services;

namespace GolfSG.Infrastructure.Persistence;

public sealed class FileActivePuttingGameRepository : IActivePuttingGameRepository
{
    private const string FileName = "active-putting-game.json";
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly string backupFilePath;
    private readonly SemaphoreSlim mutationLock = new(1, 1);

    public FileActivePuttingGameRepository(string appDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);
        filePath = Path.Combine(appDataDirectory, FileName);
        backupFilePath = filePath + ".bak";
    }

    public string ActiveStoragePath => filePath;

    public async Task<ActivePuttingGameSession?> GetAsync()
    {
        await mutationLock.WaitAsync();
        try
        {
            var active = await ReadAsync(filePath);
            return active ?? await ReadAsync(backupFilePath);
        }
        finally { mutationLock.Release(); }
    }

    public async Task SaveAsync(ActivePuttingGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsValid) throw new InvalidDataException("Invalid unfinished putting game.");

        await mutationLock.WaitAsync();
        try
        {
            var directory = Path.GetDirectoryName(filePath)!;
            Directory.CreateDirectory(directory);

            if (await ReadAsync(filePath) is not null)
            {
                File.Copy(filePath, backupFilePath, overwrite: true);
            }

            var tempFilePath = filePath + ".tmp";
            await using (var stream = File.Create(tempFilePath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new ActivePuttingGameDocument(CurrentSchemaVersion, session),
                    JsonOptions);
            }

            File.Move(tempFilePath, filePath, overwrite: true);
        }
        finally
        {
            mutationLock.Release();
        }
    }

    public async Task DeleteAsync()
    {
        await mutationLock.WaitAsync();
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
            }

            var tempFilePath = filePath + ".tmp";
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
        finally
        {
            mutationLock.Release();
        }
    }

    private static async Task<ActivePuttingGameSession?> ReadAsync(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<ActivePuttingGameDocument>(stream, JsonOptions);
            return document is { SchemaVersion: CurrentSchemaVersion } && document.Session?.IsValid == true ? document.Session : null;
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

    private sealed record ActivePuttingGameDocument(
        int SchemaVersion,
        ActivePuttingGameSession Session);
}
