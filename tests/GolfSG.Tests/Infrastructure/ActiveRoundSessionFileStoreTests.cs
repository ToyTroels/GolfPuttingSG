using GolfSG.Application.Rounds;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Persistence;

namespace GolfSG.Tests;

[TestClass]
public sealed class ActiveRoundSessionFileStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadRoundTripsDraftAndResumeHole()
    {
        using var directory = TestDirectory.Create();
        var store = new ActiveRoundSessionFileStore(directory.Path);
        var session = CreateSession("active", 4);

        await store.SaveAsync(session);
        var loaded = await store.GetAsync();

        Assert.IsNotNull(loaded);
        Assert.AreEqual("active", loaded.Draft.Id);
        Assert.AreEqual(4, loaded.CurrentHoleNumber);
        Assert.AreEqual(9, loaded.Draft.ConfiguredHoleCount);
        Assert.AreEqual(2.4, loaded.Draft.Holes[0].FirstPuttDistanceMeters, 0.001);
        Assert.IsTrue(File.Exists(directory.ActiveFilePath));
        Assert.IsFalse(File.Exists(directory.TempFilePath));
    }

    [TestMethod]
    public async Task LoadFallsBackToBackupWhenActiveFileIsCorrupt()
    {
        using var directory = TestDirectory.Create();
        var store = new ActiveRoundSessionFileStore(directory.Path);
        await store.SaveAsync(CreateSession("first", 2));
        await store.SaveAsync(CreateSession("second", 3));
        await File.WriteAllTextAsync(directory.ActiveFilePath, "{ invalid json");

        var loaded = await store.GetAsync();

        Assert.IsNotNull(loaded);
        Assert.AreEqual("first", loaded.Draft.Id);
        Assert.AreEqual(2, loaded.CurrentHoleNumber);
    }

    [TestMethod]
    public async Task DeleteRemovesActiveBackupAndTemporaryFiles()
    {
        using var directory = TestDirectory.Create();
        var store = new ActiveRoundSessionFileStore(directory.Path);
        await store.SaveAsync(CreateSession("first", 1));
        await store.SaveAsync(CreateSession("second", 2));
        await File.WriteAllTextAsync(directory.TempFilePath, "stale");

        await store.DeleteAsync();

        Assert.IsNull(await store.GetAsync());
        Assert.IsFalse(File.Exists(directory.ActiveFilePath));
        Assert.IsFalse(File.Exists(directory.BackupFilePath));
        Assert.IsFalse(File.Exists(directory.TempFilePath));
    }

    private static ActiveRoundSession CreateSession(string id, int currentHoleNumber)
    {
        var holes = Enumerable.Range(1, 9)
            .Select(number => number == 1
                ? StrokesGainedCalculator.BuildHole(number, 2.4, 2)
                : StrokesGainedCalculator.BuildHole(number, 0, 0))
            .ToList();
        return new ActiveRoundSession(
            new RoundDraft(
                id,
                new DateTime(2026, 7, 25),
                holes,
                RoundTrackingOptions.PuttingOnly,
                9),
            currentHoleNumber,
            new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero));
    }

    private sealed class TestDirectory : IDisposable
    {
        private TestDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }
        public string ActiveFilePath => System.IO.Path.Combine(Path, "active-round.json");
        public string BackupFilePath => ActiveFilePath + ".bak";
        public string TempFilePath => ActiveFilePath + ".tmp";

        public static TestDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GolfSG.Tests",
                Guid.NewGuid().ToString("N"));
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
