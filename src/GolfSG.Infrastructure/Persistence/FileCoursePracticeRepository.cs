using System.Text.Json;
using GolfSG.Application.Courses;

namespace GolfSG.Infrastructure.Persistence;

public sealed class FileCoursePracticeRepository(string directory) : ICoursePracticeRepository
{
    private readonly string path = Path.Combine(directory, "course-practice.json");
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private sealed record Document(int SchemaVersion, CoursePracticeDocument Data);

    public async Task<CoursePracticeDocument> LoadAsync()
    {
        await gate.WaitAsync();
        try { return await ReadAsync(path) ?? await ReadAsync(path + ".bak") ??
            (!File.Exists(path) && !File.Exists(path + ".bak") ? CoursePracticeDocument.Empty : throw new InvalidDataException("Banedata kunne ikke indlæses. Filerne er bevaret.")); }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(CoursePracticeDocument document)
    {
        Validate(document);
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(directory);
            if (File.Exists(path) && await ReadAsync(path) is null && await ReadAsync(path + ".bak") is null)
                throw new InvalidDataException("Banedata kunne ikke indlæses. Filerne er bevaret.");
            if (await ReadAsync(path) is not null) File.Copy(path, path + ".bak", true);
            await using (var stream = File.Create(path + ".tmp"))
                await JsonSerializer.SerializeAsync(stream, new Document(3, document), Options);
            File.Move(path + ".tmp", path, true);
            // Keep the backup current after a successful write; discarded/completed rounds must not reappear.
            File.Copy(path, path + ".bak", true);
        }
        finally { gate.Release(); }
    }

    private static void Validate(CoursePracticeDocument document)
    {
        if (document.Courses is null || document.Courses.Any(c => c is null) || document.Courses.Select(c => c.Id).Distinct().Count() != document.Courses.Count) throw new InvalidDataException();
        foreach (var course in document.Courses) CoursePracticeService.ValidateCourse(course);
        if (document.Active is { } active)
        {
            CoursePracticeService.ValidateCourse(active.Course);
            if (active.ShotOrder is { } order && (order.Count != active.Course.Shots.Count ||
                order.Distinct(StringComparer.Ordinal).Count() != order.Count ||
                order.Any(id => !active.Course.Shots.Any(shot => shot.Id == id))))
                throw new InvalidDataException();
            if (string.IsNullOrWhiteSpace(active.Id) || active.Results is null || active.Results.Count > active.TotalAttempts || active.Input is null ||
                active.Input.Penalties is < 0 or > 10 || !Enum.IsDefined(active.Input.Lie))
                throw new InvalidDataException();
            try { CoursePracticeService.ValidateResults(active.Course, active.Results, active.OrderedShots); }
            catch (ArgumentException ex) { throw new InvalidDataException("Rundens slagresultater er ugyldige.", ex); }
        }
    }

    private static async Task<CoursePracticeDocument?> ReadAsync(string file)
    {
        if (!File.Exists(file)) return null;
        try
        {
            await using var stream = File.OpenRead(file);
            using var json = await JsonDocument.ParseAsync(stream);
            if (json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number &&
                version.TryGetInt32(out var schemaVersion) && schemaVersion > 3)
                throw new NotSupportedException("Banedata er fra en nyere appversion. Filerne er bevaret.");
            var document = json.RootElement.Deserialize<Document>(Options);
            if (document?.SchemaVersion > 3) throw new NotSupportedException("Banedata er fra en nyere appversion. Filerne er bevaret.");
            if (document?.SchemaVersion is not (1 or 2 or 3) || document.Data is null) return null;
            Validate(document.Data);
            return document.Data;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidDataException) { return null; }
    }
}
