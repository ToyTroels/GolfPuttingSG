using GolfSG.Application.Putting;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class PuttingGameSessionRegressionTests
{
    [TestMethod]
    public void PresetGameUsesFeetBackedDistancesAndNormalizedTarget()
    {
        var service = new PuttingGameSessionService(new TestRoundRepository());

        service.Start(PuttingGame.TourRoundMode);

        Assert.AreEqual(PuttingGame.TourRoundMode, service.Mode);
        Assert.IsFalse(service.DistancesAreMeters);
        Assert.AreEqual(PuttingGame.TargetPutts, service.TargetPutts, 0.001);
        Assert.AreEqual(
            DistanceConversions.FeetToMeters(service.Distances[0]),
            service.CurrentDistanceMeters,
            0.001);
    }

    [TestMethod]
    public async Task StartingAnotherGameClearsPreviousProgress()
    {
        var service = new PuttingGameSessionService(new TestRoundRepository());
        service.StartTraining(3, 1, 3, PuttingTrainingDistanceDistribution.BellCurve);
        await service.SubmitAsync(2);
        Assert.AreEqual(1, service.CurrentIndex);
        Assert.HasCount(1, service.CompletedPutts);

        service.Start(PuttingGame.TourRoundMode);

        Assert.AreEqual(0, service.CurrentIndex);
        Assert.IsEmpty(service.CompletedPutts);
        Assert.IsFalse(service.IsComplete);
        Assert.IsFalse(service.DistancesAreMeters);
    }

    [TestMethod]
    public async Task LadderBenchmarkSavePreservesPresetMetadata()
    {
        var repository = new TestRoundRepository();
        var service = new PuttingGameSessionService(repository);
        service.StartLadderBenchmark(PuttingGame.ShortLadderBenchmark);

        PuttingGameSubmission result = new(false, null);
        while (!result.IsComplete)
        {
            result = await service.SubmitAsync(2);
        }

        Assert.IsNotNull(repository.SavedRound);
        Assert.AreEqual("PuttingBenchmark", repository.SavedRound.GameInfo?.Type);
        Assert.AreEqual(PuttingBenchmarkType.Ladder, repository.SavedRound.GameInfo?.BenchmarkType);
        Assert.AreEqual(PuttingGame.ShortLadderBenchmarkPresetId, repository.SavedRound.GameInfo?.PresetId);
        Assert.HasCount(service.Distances.Count, repository.SavedRound.Holes);
    }

    [TestMethod]
    public async Task OverlappingFinalSubmitDoesNotAddOrSaveTwice()
    {
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new TestRoundRepository
        {
            SaveHandler = async _ =>
            {
                saveStarted.SetResult();
                await allowSave.Task;
            }
        };
        var service = new PuttingGameSessionService(repository);
        service.StartTraining(1, 1, 2, PuttingTrainingDistanceDistribution.BellCurve);

        var firstSubmit = service.SubmitAsync(2);
        await saveStarted.Task;
        await service.SubmitAsync(2);

        Assert.AreEqual(1, service.CurrentIndex);
        Assert.HasCount(1, service.CompletedPutts);
        Assert.AreEqual(1, repository.SaveCallCount);

        allowSave.SetResult();
        var completed = await firstSubmit;
        Assert.IsTrue(completed.IsComplete);
    }

    [TestMethod]
    public async Task CompletedSessionReturnsSameRoundWithoutSavingAgain()
    {
        var repository = new TestRoundRepository();
        var service = new PuttingGameSessionService(repository);
        service.StartTraining(1, 1, 2, PuttingTrainingDistanceDistribution.BellCurve);

        var first = await service.SubmitAsync(1);
        var repeated = await service.SubmitAsync(5);

        Assert.IsTrue(first.IsComplete);
        Assert.IsTrue(repeated.IsComplete);
        Assert.AreEqual(first.RoundId, repeated.RoundId);
        Assert.AreEqual(1, repository.SaveCallCount);
        Assert.HasCount(1, service.CompletedPutts);
    }

    [TestMethod]
    public async Task CurrentDistanceIsUnavailableAfterCompletion()
    {
        var service = new PuttingGameSessionService(new TestRoundRepository());
        service.StartTraining(1, 1, 2, PuttingTrainingDistanceDistribution.BellCurve);
        await service.SubmitAsync(2);

        Assert.ThrowsExactly<InvalidOperationException>(() => _ = service.CurrentDistanceMeters);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = service.CurrentExpectedPutts);
    }
}
