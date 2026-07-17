using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Persistence;

namespace GolfSG.Tests;

[TestClass]
public sealed class RoundFileStoreConcurrencyTests
{
    [TestMethod]
    public async Task ConcurrentSavesPreserveEveryRound()
    {
        using var directory = new TemporaryDirectory();
        var store = new RoundFileStore(directory.Path);
        var rounds = Enumerable.Range(1, 16)
            .Select(index => new Round(
                $"round-{index}",
                new DateTime(2026, 7, 16).AddMinutes(index),
                [StrokesGainedCalculator.BuildHole(1, 2 + index / 10d, 2)],
                RoundTrackingOptions.PuttingOnly,
                1))
            .ToList();

        await Task.WhenAll(rounds.Select(store.SaveRoundAsync));
        var loaded = await store.GetRoundsAsync();

        Assert.HasCount(rounds.Count, loaded);
        CollectionAssert.AreEquivalent(
            rounds.Select(round => round.Id).ToArray(),
            loaded.Select(round => round.Id).ToArray());
    }

    [TestMethod]
    public async Task ConcurrentUpdatesOfSameRoundKeepOneRecord()
    {
        using var directory = new TemporaryDirectory();
        var store = new RoundFileStore(directory.Path);
        var updates = Enumerable.Range(1, 10)
            .Select(putts => new Round(
                "same-round",
                new DateTime(2026, 7, 16),
                [StrokesGainedCalculator.BuildHole(1, 3, Math.Min(putts, 5))],
                RoundTrackingOptions.PuttingOnly,
                1))
            .ToList();

        await Task.WhenAll(updates.Select(store.SaveRoundAsync));
        var loaded = await store.GetRoundsAsync();

        Assert.HasCount(1, loaded);
        Assert.AreEqual("same-round", loaded[0].Id);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"GolfSG-concurrency-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
