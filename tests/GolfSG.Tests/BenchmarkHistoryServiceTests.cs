using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class BenchmarkHistoryServiceTests
{
    [TestMethod]
    public void SummarizeGroupsBenchmarkAttemptsByPresetId()
    {
        var bellCurveDefinition = PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalBenchmark);
        var ladderDefinition = PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalLadderBenchmark);
        var rounds = new[]
        {
            CreateBenchmarkRound("bell-1", new DateTime(2026, 7, 1), bellCurveDefinition, 1.0),
            CreateBenchmarkRound("bell-2", new DateTime(2026, 7, 5), bellCurveDefinition, 2.5),
            CreateBenchmarkRound("ladder-1", new DateTime(2026, 7, 3), ladderDefinition, -0.5),
            CreatePuttingGameRound("training", new DateTime(2026, 7, 4), 9.9)
        };

        var summaries = BenchmarkHistoryService.Summarize(rounds);

        Assert.HasCount(2, summaries);
        var bellCurve = summaries.Single(summary => summary.PresetId == PuttingGame.NormalBenchmarkPresetId);
        Assert.AreEqual(2, bellCurve.Attempts);
        Assert.AreEqual("bell-2", bellCurve.LatestAttempt.RoundId);
        Assert.AreEqual("bell-2", bellCurve.BestAttempt.RoundId);
        Assert.AreEqual(1.5, bellCurve.ImprovementFromFirstToLatest, 0.001);

        var ladder = summaries.Single(summary => summary.PresetId == PuttingGame.NormalLadderBenchmarkPresetId);
        Assert.AreEqual(PuttingBenchmarkType.Ladder, ladder.BenchmarkType);
        Assert.AreEqual(30, ladder.AttemptCount);
        Assert.AreEqual(1, ladder.StartDistanceMeters);
        Assert.AreEqual(6, ladder.EndDistanceMeters);
        Assert.AreEqual(5, ladder.PuttsPerDistance);
    }

    [TestMethod]
    public void SummarizeCalculatesAverageOfLatestThreeAttempts()
    {
        var definition = PuttingGame.GetBenchmarkDefinition(PuttingGame.ShortBenchmark);
        var rounds = new[]
        {
            CreateBenchmarkRound("oldest", new DateTime(2026, 7, 1), definition, 100),
            CreateBenchmarkRound("third", new DateTime(2026, 7, 2), definition, 3),
            CreateBenchmarkRound("second", new DateTime(2026, 7, 3), definition, 6),
            CreateBenchmarkRound("latest", new DateTime(2026, 7, 4), definition, 9)
        };

        var summary = BenchmarkHistoryService.Summarize(rounds).Single();

        Assert.AreEqual("latest", summary.LatestAttempt.RoundId);
        Assert.AreEqual(6, summary.AverageLastThreeScore, 0.001);
        CollectionAssert.AreEqual(
            new[] { "latest", "second", "third", "oldest" },
            summary.RecentAttempts.Select(attempt => attempt.RoundId).ToArray());
    }

    [TestMethod]
    public void SummarizeIgnoresRoundsWithoutBenchmarkMetadata()
    {
        var rounds = new[]
        {
            new Round(
                "legacy",
                new DateTime(2026, 7, 1),
                [new HolePuttingData(1, 2, 1, 0, 1)],
                RoundTrackingOptions.PuttingGame,
                ConfiguredHoleCount: 1,
                EndedEarly: false,
                GameInfo: null)
        };

        var summaries = BenchmarkHistoryService.Summarize(rounds);

        Assert.HasCount(0, summaries);
    }

    private static Round CreateBenchmarkRound(
        string id,
        DateTime date,
        PuttingGameDefinition definition,
        double score) =>
        new(
            id,
            date,
            [new HolePuttingData(1, definition.DistancesMeters[0], 2, 0, score)],
            new RoundTrackingOptions(true, false, false, true),
            ConfiguredHoleCount: 1,
            EndedEarly: false,
            PuttingGame.CreateRoundGameInfo(definition));

    private static Round CreatePuttingGameRound(string id, DateTime date, double score)
    {
        var definition = PuttingGame.CreateCustomDefinition([2]);
        return new Round(
            id,
            date,
            [new HolePuttingData(1, 2, 1, 0, score)],
            new RoundTrackingOptions(true, false, false, true),
            ConfiguredHoleCount: 1,
            EndedEarly: false,
            PuttingGame.CreateRoundGameInfo(definition));
    }
}
