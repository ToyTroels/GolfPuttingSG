using System.Text.Json;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Persistence;

namespace GolfSG.Tests;

[TestClass]
public sealed class RoundFileStoreTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [TestMethod]
    public async Task SaveRoundWritesActiveFileThroughTemporaryFile()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var round = CreateRound("first", new DateTime(2026, 6, 1), 2);

        await store.SaveRoundAsync(round);

        Assert.IsTrue(File.Exists(directory.RoundsFilePath));
        Assert.IsFalse(File.Exists(directory.TempFilePath));
        var savedRounds = await ReadRoundsAsync(directory.RoundsFilePath);
        Assert.HasCount(1, savedRounds);
        Assert.AreEqual("first", savedRounds[0].Id);
        Assert.AreEqual(1, await ReadSchemaVersionAsync(directory.RoundsFilePath));
    }

    [TestMethod]
    public void ActiveStoragePathPointsToRoundsJsonInConfiguredDirectory()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);

        Assert.AreEqual(directory.RoundsFilePath, store.ActiveStoragePath);
    }

    [TestMethod]
    public async Task SaveRoundKeepsBackupBeforeOverwritingActiveFile()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var firstRound = CreateRound("first", new DateTime(2026, 6, 1), 2);
        var secondRound = CreateRound("second", new DateTime(2026, 6, 2), 1);

        await store.SaveRoundAsync(firstRound);
        await store.SaveRoundAsync(secondRound);

        var backupRounds = await ReadRoundsAsync(directory.BackupFilePath);
        Assert.HasCount(1, backupRounds);
        Assert.AreEqual("first", backupRounds[0].Id);
    }

    [TestMethod]
    public async Task GetRoundsFallsBackToBackupWhenActiveFileIsCorrupt()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var backupRound = CreateRound("backup", new DateTime(2026, 6, 1), 2);
        await WriteRoundsAsync(directory.BackupFilePath, [backupRound]);
        await File.WriteAllTextAsync(directory.RoundsFilePath, "{ not valid json");

        var rounds = await store.GetRoundsAsync();

        Assert.HasCount(1, rounds);
        Assert.AreEqual("backup", rounds[0].Id);
        Assert.IsTrue(store.WasLastReadRecoveredFromBackup);
    }

    [TestMethod]
    public async Task GetRoundsFallsBackToBackupWhenActiveFileIsEmpty()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var backupRound = CreateRound("backup", new DateTime(2026, 6, 1), 2);
        await WriteRoundsAsync(directory.BackupFilePath, [backupRound]);
        await File.WriteAllTextAsync(directory.RoundsFilePath, string.Empty);

        var rounds = await store.GetRoundsAsync();

        Assert.HasCount(1, rounds);
        Assert.AreEqual("backup", rounds[0].Id);
        Assert.IsTrue(store.WasLastReadRecoveredFromBackup);
    }

    [TestMethod]
    public async Task GetRoundsDoesNotReportBackupRecoveryWhenActiveFileLoads()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var activeRound = CreateRound("active", new DateTime(2026, 6, 1), 2);
        await WriteRoundsAsync(directory.RoundsFilePath, [activeRound]);

        var rounds = await store.GetRoundsAsync();

        Assert.HasCount(1, rounds);
        Assert.AreEqual("active", rounds[0].Id);
        Assert.IsFalse(store.WasLastReadRecoveredFromBackup);
    }

    [TestMethod]
    public async Task GetRoundsCanReadLegacyArrayFormat()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var legacyRound = CreateRound("legacy-array", new DateTime(2026, 6, 1), 2);
        await WriteLegacyRoundsArrayAsync(directory.RoundsFilePath, [legacyRound]);

        var rounds = await store.GetRoundsAsync();

        Assert.HasCount(1, rounds);
        Assert.AreEqual("legacy-array", rounds[0].Id);
    }

    [TestMethod]
    public async Task GetRoundsMigratesFromLegacyStorageWhenActiveStorageIsMissing()
    {
        using var activeDirectory = TestDirectory.Create();
        using var legacyDirectory = TestDirectory.Create();
        var store = new RoundFileStore(activeDirectory.Path, [legacyDirectory.Path]);
        var legacyRound = CreateRound("legacy", new DateTime(2026, 6, 1), 2);
        await WriteLegacyRoundsArrayAsync(legacyDirectory.RoundsFilePath, [legacyRound]);

        var rounds = await store.GetRoundsAsync();

        Assert.HasCount(1, rounds);
        Assert.AreEqual("legacy", rounds[0].Id);
        Assert.IsTrue(store.WasLastReadMigratedFromLegacyStorage);
        Assert.AreEqual(legacyDirectory.RoundsFilePath, store.LastMigrationSourcePath);
        Assert.IsTrue(File.Exists(activeDirectory.RoundsFilePath));
        Assert.AreEqual(1, await ReadSchemaVersionAsync(activeDirectory.RoundsFilePath));
    }

    [TestMethod]
    public async Task SaveRoundPreservesUnreadableActiveFileBeforeOverwrite()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        await File.WriteAllTextAsync(directory.RoundsFilePath, "{ not valid json");

        await store.SaveRoundAsync(CreateRound("new", new DateTime(2026, 6, 1), 2));

        Assert.IsTrue(store.WasLastUnreadableActiveFilePreserved);
        Assert.IsFalse(string.IsNullOrWhiteSpace(store.LastPreservedUnreadableFilePath));
        Assert.IsTrue(File.Exists(store.LastPreservedUnreadableFilePath));
        Assert.AreEqual("{ not valid json", await File.ReadAllTextAsync(store.LastPreservedUnreadableFilePath));
        var rounds = await ReadRoundsAsync(directory.RoundsFilePath);
        Assert.HasCount(1, rounds);
        Assert.AreEqual("new", rounds[0].Id);
    }

    [TestMethod]
    public async Task SaveRoundDoesNotOverwriteGoodBackupWithCorruptActiveFile()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        await File.WriteAllTextAsync(directory.RoundsFilePath, "{ not valid json");
        await WriteRoundsAsync(directory.BackupFilePath, [CreateRound("backup", new DateTime(2026, 5, 1), 2)]);

        await store.SaveRoundAsync(CreateRound("new", new DateTime(2026, 6, 1), 1));

        var backupRounds = await ReadRoundsAsync(directory.BackupFilePath);
        Assert.HasCount(1, backupRounds);
        Assert.AreEqual("backup", backupRounds[0].Id);

        var activeRounds = await ReadRoundsAsync(directory.RoundsFilePath);
        Assert.HasCount(2, activeRounds);
        CollectionAssert.AreEqual(new[] { "new", "backup" }, activeRounds.Select(round => round.Id).ToArray());
    }

    [TestMethod]
    public async Task ExportRoundsCopiesVersionedHistoryDocument()
    {
        using var directory = TestDirectory.Create();
        using var exportDirectory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        await store.SaveRoundAsync(CreateRound("exported", new DateTime(2026, 6, 1), 2));
        var exportPath = System.IO.Path.Combine(exportDirectory.Path, "exported-rounds.json");

        await store.ExportRoundsAsync(exportPath);

        var exportedRounds = await ReadRoundsAsync(exportPath);
        Assert.HasCount(1, exportedRounds);
        Assert.AreEqual("exported", exportedRounds[0].Id);
        Assert.AreEqual(1, await ReadSchemaVersionAsync(exportPath));
    }

    [TestMethod]
    public async Task ImportRoundsAcceptsLegacyArrayAndReplacesActiveHistory()
    {
        using var directory = TestDirectory.Create();
        using var importDirectory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        await store.SaveRoundAsync(CreateRound("existing", new DateTime(2026, 5, 1), 2));
        var importPath = System.IO.Path.Combine(importDirectory.Path, "rounds.json");
        await WriteLegacyRoundsArrayAsync(importPath, [CreateRound("imported", new DateTime(2026, 6, 1), 1)]);

        await store.ImportRoundsAsync(importPath);

        var rounds = await store.GetRoundsAsync();
        Assert.HasCount(1, rounds);
        Assert.AreEqual("imported", rounds[0].Id);
        Assert.AreEqual(1, await ReadSchemaVersionAsync(directory.RoundsFilePath));
    }

    [TestMethod]
    public async Task SaveAndDeletePreserveRemainingRounds()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var oldestRound = CreateRound("oldest", new DateTime(2026, 5, 1), 2);
        var middleRound = CreateRound("middle", new DateTime(2026, 6, 1), 1);
        var newestRound = CreateRound("newest", new DateTime(2026, 6, 15), 3);

        await store.SaveRoundAsync(oldestRound);
        await store.SaveRoundAsync(middleRound);
        await store.SaveRoundAsync(newestRound);
        await store.DeleteRoundAsync(middleRound.Id);

        var rounds = await store.GetRoundsAsync();
        Assert.HasCount(2, rounds);
        CollectionAssert.AreEqual(new[] { "newest", "oldest" }, rounds.Select(round => round.Id).ToArray());
    }

    [TestMethod]
    public async Task SaveRoundPersistsConfiguredHoleCountAndEarlyFinish()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var round = new Round(
            "partial",
            new DateTime(2026, 6, 1),
            [StrokesGainedCalculator.BuildHole(1, 2.4, 2)],
            RoundTrackingOptions.PuttingOnly,
            9,
            EndedEarly: true);

        await store.SaveRoundAsync(round);

        var rounds = await store.GetRoundsAsync();
        Assert.HasCount(1, rounds);
        Assert.AreEqual(9, rounds[0].ConfiguredHoleCount);
        Assert.IsTrue(rounds[0].EndedEarly);
        Assert.AreEqual(1, rounds[0].CompletedHoleCount);
        Assert.IsFalse(rounds[0].IsCompletedNormally);
    }

    [TestMethod]
    public async Task SaveRoundPersistsPuttingGameMetadata()
    {
        using var directory = TestDirectory.Create();
        var store = new RoundFileStore(directory.Path);
        var definition = PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalLadderBenchmark);
        var round = new Round(
            "benchmark",
            new DateTime(2026, 6, 1),
            [PuttingGame.BuildPutt(1, definition.DistancesMeters[0], 2)],
            new RoundTrackingOptions(true, false, false, true, PuttingGame.NormalLadderBenchmark),
            1,
            false,
            PuttingGame.CreateRoundGameInfo(definition));

        await store.SaveRoundAsync(round);

        var rounds = await store.GetRoundsAsync();
        Assert.HasCount(1, rounds);
        var gameInfo = rounds[0].GameInfo;
        Assert.IsNotNull(gameInfo);
        Assert.AreEqual("PuttingBenchmark", gameInfo.Type);
        Assert.AreEqual("Normal ladder benchmark", gameInfo.DisplayName);
        Assert.AreEqual(PuttingGame.NormalLadderBenchmarkPresetId, gameInfo.PresetId);
        Assert.AreEqual(PuttingGame.BenchmarkPresetVersion, gameInfo.PresetVersion);
        Assert.AreEqual(PuttingBenchmarkType.Ladder, gameInfo.BenchmarkType);
        Assert.AreEqual(30, gameInfo.AttemptCount);
        Assert.AreEqual(1, gameInfo.StartDistanceMeters);
        Assert.AreEqual(6, gameInfo.EndDistanceMeters);
        Assert.AreEqual(1, gameInfo.DistanceStepMeters);
        Assert.AreEqual(5, gameInfo.PuttsPerDistance);
    }

    private static Round CreateRound(string id, DateTime date, int putts) => new(
        id,
        date,
        [StrokesGainedCalculator.BuildHole(1, 2.4, putts)],
        RoundTrackingOptions.PuttingOnly);

    private static async Task WriteRoundsAsync(string path, IReadOnlyList<Round> rounds)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, new RoundHistoryDocument(1, rounds), JsonOptions);
    }

    private static async Task WriteLegacyRoundsArrayAsync(string path, IReadOnlyList<Round> rounds)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, rounds, JsonOptions);
    }

    private static async Task<IReadOnlyList<Round>> ReadRoundsAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return document.RootElement.Deserialize<List<Round>>(JsonOptions) ?? [];
        }

        return document.RootElement.GetProperty("rounds").Deserialize<List<Round>>(JsonOptions) ?? [];
    }

    private static async Task<int> ReadSchemaVersionAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("schemaVersion").GetInt32();
    }

    private sealed record RoundHistoryDocument(
        int SchemaVersion,
        IReadOnlyList<Round> Rounds);

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public string RoundsFilePath => System.IO.Path.Combine(Path, "rounds.json");

        public string BackupFilePath => RoundsFilePath + ".bak";

        public string TempFilePath => System.IO.Path.Combine(Path, "rounds.json.tmp");

        public static TestDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GolfSG.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TestDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
