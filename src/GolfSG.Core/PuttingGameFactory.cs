using GolfSG.Core.Models;

namespace GolfSG.Core;

internal static class PuttingGameFactory
{
    public static RoundGameInfo CreateRoundGameInfo(PuttingGameDefinition definition) =>
        new(
            definition.Kind switch
            {
                PuttingGameKind.Benchmark => "PuttingBenchmark",
                PuttingGameKind.Custom => "PuttingGame",
                _ => "PuttingGame"
            },
            definition.DisplayName,
            definition.PresetId,
            definition.PresetVersion,
            definition.AttemptCount,
            definition.MinimumDistanceMeters,
            definition.MaximumDistanceMeters,
            definition.ExpectedTotal,
            definition.BenchmarkType,
            definition.StartDistanceMeters,
            definition.EndDistanceMeters,
            definition.DistanceStepMeters,
            definition.PuttsPerDistance,
            definition.DistanceStepDescription);

    public static HolePuttingData BuildPutt(int puttNumber, int distanceFeet, int putts, string mode)
    {
        var distanceMeters = PuttingGameScoring.ToMeters(distanceFeet);
        var expectedPutts = putts > 0 ? PuttingGameScoring.GetExpectedPutts(distanceFeet, mode) : 0;

        return new HolePuttingData(
            puttNumber,
            distanceMeters,
            putts,
            expectedPutts,
            StrokesGainedMath.Calculate(expectedPutts, putts));
    }

    public static HolePuttingData BuildPutt(int puttNumber, double distanceMeters, int putts)
    {
        var expectedPutts = putts > 0 ? PuttingGameScoring.GetExpectedPutts(distanceMeters) : 0;

        return new HolePuttingData(
            puttNumber,
            distanceMeters,
            putts,
            expectedPutts,
            StrokesGainedMath.Calculate(expectedPutts, putts));
    }
}
