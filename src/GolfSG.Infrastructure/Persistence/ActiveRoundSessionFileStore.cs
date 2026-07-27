using System.Text.Json;
using GolfSG.Application.Rounds;

namespace GolfSG.Infrastructure.Persistence;

public sealed class ActiveRoundSessionFileStore
{
    private const string FileName = "active-round.json";
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly string backupFilePath;
    private readonly SemaphoreSlim mutationLock = new(1, 1);

    public ActiveRoundSessionFileStore(string appDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);
        filePath = Path.Combine(appDataDirectory, FileName);
        backupFilePath = filePath + ".bak";
    }

    public string ActiveStoragePath => filePath;

    public async Task<ActiveRoundSession?> GetAsync()
    {
        var active = await ReadAsync(filePath);
        return active ?? await ReadAsync(backupFilePath);
    }

    public async Task SaveAsync(ActiveRoundSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

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
                    new ActiveRoundDocument(CurrentSchemaVersion, session),
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

    private static async Task<ActiveRoundSession?> ReadAsync(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<ActiveRoundDocument>(stream, JsonOptions);
            return document?.Session;
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

    private sealed record ActiveRoundDocument(
        int SchemaVersion,
        ActiveRoundSession Session);
}
