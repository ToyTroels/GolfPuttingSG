using System.Text.Json;
using System.Text.Json.Serialization;
using GolfSG.Application.Rounds.Sync;

namespace GolfSG.Infrastructure.Rounds;

// Access is serialized by SyncingRoundRepository.MutationGate. No network operation holds that gate.
public sealed class RoundSyncStateStore(string directory)
{
    public string Path { get; } = System.IO.Path.Combine(directory, "round-sync.json");

    internal async Task<RoundSyncState> LoadAsync()
    {
        if (!File.Exists(Path)) return new();
        try
        {
            var json = await File.ReadAllTextAsync(Path).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize(json, RoundSyncStateJsonContext.Default.RoundSyncState);
            if (state is null || state.SchemaVersion != 1 || state.Servers is null || state.DeletedIds is null ||
                state.DeletedIds.Any(id => !RoundSyncValidation.IsValidId(id)) ||
                state.Servers.Any(server => server.Value is null || server.Value.Any(entry =>
                    !RoundSyncValidation.IsValidId(entry.Key) || entry.Value is { } hash &&
                    (hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c))))))
                throw new JsonException();
            return state;
        }
        catch (JsonException ex)
        {
            // An empty replacement could restore deleted rounds or lose pending work. Fail closed.
            throw new RoundSyncException("Synkroniseringsdata på enheden kunne ikke læses. Dine lokale runder er bevaret.", ex);
        }
    }

    internal async Task SaveAsync(RoundSyncState state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, RoundSyncStateJsonContext.Default.RoundSyncState);
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None,
                         4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.WriteAsync(bytes).ConfigureAwait(false);
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporary, Path, overwrite: true);
    }
}

internal sealed class RoundSyncState
{
    [JsonRequired] public int SchemaVersion { get; init; } = 1;
    [JsonRequired] public Dictionary<string, Dictionary<string, string?>> Servers { get; init; } = new(StringComparer.Ordinal);
    [JsonRequired] public HashSet<string> DeletedIds { get; init; } = new(StringComparer.Ordinal);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RoundSyncState))]
internal partial class RoundSyncStateJsonContext : JsonSerializerContext;
