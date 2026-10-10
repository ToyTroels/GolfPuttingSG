using GolfSG.Application.Putting;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using GolfSG.Core;

namespace GolfSG.Tests;

[TestClass]
public sealed class PuttingGameCorrectionTests
{
    [TestMethod]
    public async Task UndoAndResubmitKeepRandomOrderAndSaveEachResultOnce()
    {
        var active = new MemoryActiveRepository();
        var history = new TestRoundRepository();
        var service = new PuttingGameSessionService(history, active);
        service.StartBenchmark(PuttingGame.ShortBenchmark, PuttingDistanceOrder.Random);
        var distances = service.Distances.ToArray();
        var roundId = service.RoundId;
        await service.SubmitAsync(2);
        await service.SubmitAsync(4);
        await service.SubmitAsync(3);

        Assert.AreEqual(3, await service.UndoLastAsync());

        Assert.AreEqual(2, service.CurrentIndex);
        Assert.HasCount(2, service.CompletedPutts);
        Assert.AreEqual(roundId, service.RoundId);
        CollectionAssert.AreEqual(distances, service.Distances.ToArray());
        Assert.AreEqual(distances[2], service.CurrentDistanceMeters);
        Assert.AreEqual(3, active.Session!.PuttsUsed);
        Assert.HasCount(2, active.Session.CompletedPutts);
        var resumed = new PuttingGameSessionService(history, active);
        resumed.Restore(active.Session);
        Assert.AreEqual(2, resumed.CurrentIndex);
        CollectionAssert.AreEqual(distances, resumed.Distances.ToArray());

        await resumed.SubmitAsync(1);
        while (!resumed.IsComplete) await resumed.SubmitAsync(2);

        Assert.HasCount(1, history.Rounds);
        Assert.AreEqual(roundId, history.Rounds[0].Id);
        Assert.HasCount(distances.Length, history.Rounds[0].Holes);
        CollectionAssert.AreEqual(distances, history.Rounds[0].Holes.Select(putt => putt.FirstPuttDistanceMeters).ToArray());
        CollectionAssert.AreEqual(Enumerable.Range(1, distances.Length).ToArray(), history.Rounds[0].Holes.Select(putt => putt.HoleNumber).ToArray());
        Assert.AreEqual(4, history.Rounds[0].Holes[1].Putts);
        Assert.AreEqual(1, history.Rounds[0].Holes[2].Putts);
        Assert.IsNull(active.Session);
    }

    [TestMethod]
    public async Task UndoWithoutRecordedResultsOrAfterCompletionDoesNotSaveOrChangeProgress()
    {
        var active = new MemoryActiveRepository();
        var history = new TestRoundRepository();
        var service = new PuttingGameSessionService(history, active);
        service.StartTraining(1, 1, 2, PuttingTrainingDistanceDistribution.BellCurve);

        Assert.IsNull(await service.UndoLastAsync());
        Assert.AreEqual(0, active.SaveCallCount);
        Assert.AreEqual(0, service.CurrentIndex);

        await service.SubmitAsync(3);
        Assert.IsNull(await service.UndoLastAsync());

        Assert.IsTrue(service.IsComplete);
        Assert.AreEqual(1, service.CurrentIndex);
        Assert.AreEqual(3, service.CompletedPutts[0].Putts);
        Assert.AreEqual(1, history.SaveCallCount);
        Assert.IsNull(active.Session);
    }

    [TestMethod]
    public async Task EditingEarlierBenchmarkResultPreservesProgressCurrentInputAndRandomOrder()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartBenchmark(PuttingGame.ShortBenchmark, PuttingDistanceOrder.Random);
        var distances = service.Distances.ToArray();
        var roundId = service.RoundId;
        await service.SubmitAsync(3);
        await service.SubmitAsync(2);
        var secondPutt = service.CompletedPutts[1];
        var currentDistance = service.CurrentDistanceMeters;

        Assert.IsTrue(await service.EditAsync(0, 1, 4));

        Assert.AreEqual(2, service.CurrentIndex);
        Assert.HasCount(2, service.CompletedPutts);
        Assert.AreEqual(roundId, service.RoundId);
        Assert.AreEqual(currentDistance, service.CurrentDistanceMeters);
        CollectionAssert.AreEqual(distances, service.Distances.ToArray());
        Assert.AreEqual(PuttingGame.BuildPutt(1, distances[0], 1), service.CompletedPutts[0]);
        Assert.AreEqual(secondPutt, service.CompletedPutts[1]);
        Assert.AreEqual(4, active.Session!.PuttsUsed);
        var resumed = new PuttingGameSessionService(new TestRoundRepository(), active);
        resumed.Restore(active.Session);
        Assert.AreEqual(2, resumed.CurrentIndex);
        Assert.AreEqual(service.CompletedPutts[0], resumed.CompletedPutts[0]);
        CollectionAssert.AreEqual(distances, resumed.Distances.ToArray());
    }

    [TestMethod]
    public async Task EditingFeetBackedTourResultUsesTourScoring()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.Start(PuttingGame.TourRoundMode);
        await service.SubmitAsync(4);
        var original = service.CompletedPutts[0];

        Assert.IsTrue(await service.EditAsync(0, 1, 3));

        Assert.IsFalse(service.DistancesAreMeters);
        Assert.AreEqual(PuttingGame.BuildPutt(1, (int)service.Distances[0], 1, PuttingGame.TourRoundMode), service.CompletedPutts[0]);
        Assert.AreEqual(original.ExpectedPutts, service.CompletedPutts[0].ExpectedPutts, 0.000001);
        Assert.AreEqual(original.FirstPuttDistanceMeters, service.CompletedPutts[0].FirstPuttDistanceMeters, 0.000001);
        Assert.AreEqual(original.StrokesGainedPutting + 3, service.CompletedPutts[0].StrokesGainedPutting, 0.000001);
        Assert.AreEqual(3, active.Session!.PuttsUsed);
    }

    [TestMethod]
    public async Task EditingCompletedBenchmarkUpdatesSameRoundAndPreservesSavedDateAndMetadata()
    {
        var active = new MemoryActiveRepository();
        var history = new TestRoundRepository();
        var service = new PuttingGameSessionService(history, active);
        service.StartLadderBenchmark(PuttingGame.ShortLadderBenchmark);
        while (!service.IsComplete) await service.SubmitAsync(2);
        var original = history.Rounds.Single();
        var historicalDate = new DateTime(2024, 6, 15, 13, 42, 0);
        await history.SaveRoundAsync(original with { Date = historicalDate });
        var activeSaveCalls = active.SaveCallCount;

        Assert.IsTrue(await service.EditAsync(1, 4, 2));

        Assert.HasCount(1, history.Rounds);
        var updated = history.Rounds.Single();
        Assert.AreEqual(original.Id, updated.Id);
        Assert.AreEqual(historicalDate, updated.Date);
        Assert.AreEqual(original.GameInfo, updated.GameInfo);
        Assert.AreEqual(original.TrackingOptions, updated.TrackingOptions);
        Assert.AreEqual(original.ConfiguredHoleCount, updated.ConfiguredHoleCount);
        Assert.AreEqual(original.EndedEarly, updated.EndedEarly);
        Assert.HasCount(original.Holes.Count, updated.Holes);
        Assert.AreEqual(original.Holes[0], updated.Holes[0]);
        Assert.AreEqual(original.Holes[2], updated.Holes[2]);
        Assert.AreEqual(PuttingGame.BuildPutt(2, service.Distances[1], 4), updated.Holes[1]);
        Assert.AreEqual(updated.Holes[1], service.CompletedPutts[1]);
        Assert.AreEqual(service.Distances.Count, service.CurrentIndex);
        Assert.IsTrue(service.IsComplete);
        Assert.AreEqual(activeSaveCalls, active.SaveCallCount);
        Assert.IsNull(active.Session);
    }

    [TestMethod]
    public async Task FailedUndoSaveRestoresRecordedResultAndRetryCanUndo()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartBenchmark(PuttingGame.ShortBenchmark, PuttingDistanceOrder.Random);
        await service.SubmitAsync(4);
        await service.SubmitAsync(3);
        var results = service.CompletedPutts.ToArray();
        var saved = active.Session;
        var currentDistance = service.CurrentDistanceMeters;
        active.SaveException = new IOException("disk full");

        await Assert.ThrowsAsync<IOException>(() => service.UndoLastAsync());

        Assert.AreEqual(2, service.CurrentIndex);
        CollectionAssert.AreEqual(results, service.CompletedPutts.ToArray());
        Assert.AreEqual(currentDistance, service.CurrentDistanceMeters);
        Assert.AreSame(saved, active.Session);
        active.SaveException = null;
        Assert.AreEqual(3, await service.UndoLastAsync());
        Assert.AreEqual(1, service.CurrentIndex);
        Assert.AreEqual(4, service.CompletedPutts.Single().Putts);
    }

    [TestMethod]
    public async Task FailedActiveEditSaveRestoresResultAndPersistedInput()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(3, 1, 4, PuttingTrainingDistanceDistribution.Random);
        await service.SubmitAsync(3);
        var original = service.CompletedPutts[0];
        var saved = active.Session;
        active.SaveException = new IOException("disk full");

        await Assert.ThrowsAsync<IOException>(() => service.EditAsync(0, 1, 5));

        Assert.AreEqual(1, service.CurrentIndex);
        Assert.AreEqual(original, service.CompletedPutts.Single());
        Assert.AreSame(saved, active.Session);
        active.SaveException = null;
        Assert.IsTrue(await service.EditAsync(0, 1, 5));
        Assert.AreEqual(1, service.CompletedPutts.Single().Putts);
        Assert.AreEqual(5, active.Session!.PuttsUsed);
    }

    [TestMethod]
    public async Task FailedCompletedEditSaveLeavesHistoryAndServiceResultsUnchanged()
    {
        var history = new TestRoundRepository();
        var service = new PuttingGameSessionService(history);
        service.StartTraining(2, 1, 4, PuttingTrainingDistanceDistribution.Random);
        await service.SubmitAsync(3);
        await service.SubmitAsync(2);
        var original = history.Rounds.Single();
        history.SaveException = new IOException("disk full");

        await Assert.ThrowsAsync<IOException>(() => service.EditAsync(0, 1, 2));

        Assert.IsTrue(service.IsComplete);
        Assert.AreSame(original, history.Rounds.Single());
        CollectionAssert.AreEqual(original.Holes.ToArray(), service.CompletedPutts.ToArray());
        history.SaveException = null;
        Assert.IsTrue(await service.EditAsync(0, 1, 2));
        Assert.HasCount(1, history.Rounds);
        Assert.AreEqual(original.Date, history.Rounds.Single().Date);
        Assert.AreEqual(1, history.Rounds.Single().Holes[0].Putts);
    }

    [TestMethod]
    public async Task OverlappingCorrectionAndSubmitActionsAreIgnoredDuringEditSave()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(3, 1, 4, PuttingTrainingDistanceDistribution.Random);
        await service.SubmitAsync(2);
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        active.SaveHandler = async () =>
        {
            saveStarted.SetResult();
            await allowSave.Task;
        };

        var edit = service.EditAsync(0, 4, 5);
        await saveStarted.Task;
        try
        {
            Assert.IsNull(await service.UndoLastAsync());
            Assert.IsFalse(await service.EditAsync(0, 1, 2));
            Assert.IsFalse((await service.SubmitAsync(3)).IsComplete);
            Assert.AreEqual(1, service.CurrentIndex);
            Assert.HasCount(1, service.CompletedPutts);
            Assert.AreEqual(2, active.SaveCallCount);
        }
        finally { allowSave.SetResult(); }

        Assert.IsTrue(await edit);
        Assert.AreEqual(4, service.CompletedPutts.Single().Putts);
        Assert.AreEqual(5, active.Session!.PuttsUsed);
    }

    [TestMethod]
    public async Task InvalidEditCannotChangeProgressOrPersistResults()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(3, 1, 4, PuttingTrainingDistanceDistribution.Random);
        await service.SubmitAsync(3);
        var saved = active.Session;
        var original = service.CompletedPutts.Single();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EditAsync(-1, 2, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EditAsync(1, 2, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EditAsync(0, 0, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EditAsync(0, 6, 2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EditAsync(0, 2, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EditAsync(0, 2, 6));

        Assert.AreEqual(1, service.CurrentIndex);
        Assert.AreEqual(original, service.CompletedPutts.Single());
        Assert.AreSame(saved, active.Session);
        Assert.AreEqual(1, active.SaveCallCount);
    }

    [TestMethod]
    public async Task ViewModelUndoRestoresPreviousInputAndUpdatesReviewAndTotals()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(4, 1, 4, PuttingTrainingDistanceDistribution.Random);
        var model = new PuttingGameViewModel(service, activeRepository: active);
        model.Resume(service.Capture(2));
        model.PuttsUsed = 4;
        await model.SubmitAsync();
        model.PuttsUsed = 3;
        await model.SubmitAsync();
        model.PuttsUsed = 5;

        Assert.IsTrue(await model.UndoLastAsync());

        Assert.AreEqual(3, model.PuttsUsed);
        Assert.AreEqual("4", model.TotalPuttsText);
        Assert.AreEqual("Putt 2 af 4", model.ProgressText);
        Assert.HasCount(1, model.RecordedPutts);
        Assert.AreEqual(4, model.RecordedPutts.Single().Putts);
        Assert.IsTrue(model.CanUndo);
        Assert.IsTrue(model.CanReview);
        Assert.AreEqual(3, active.Session!.PuttsUsed);
        model.PuttsUsed = 1;
        await model.SubmitAsync();
        Assert.HasCount(2, model.RecordedPutts);
        Assert.AreEqual("5", model.TotalPuttsText);
        Assert.AreEqual(1, model.RecordedPutts[1].Putts);
    }

    [TestMethod]
    public async Task ViewModelCorrectionBlocksOverlappingActionsAndPreservesCurrentInput()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(3, 1, 4, PuttingTrainingDistanceDistribution.Random);
        var model = new PuttingGameViewModel(service, activeRepository: active);
        model.Resume(service.Capture(3));
        await model.SubmitAsync();
        model.PuttsUsed = 5;
        var saveStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        active.SaveHandler = async () =>
        {
            saveStarted.TrySetResult();
            await allowSave.Task;
        };

        var edit = model.EditPuttAsync(0, 1);
        await saveStarted.Task;
        try
        {
            Assert.IsTrue(model.IsBusy);
            Assert.IsFalse(model.CanSubmit);
            Assert.IsFalse(model.CanUndo);
            Assert.IsFalse(model.CanReview);
            model.PuttsUsed = 2;
            Assert.AreEqual(5, model.PuttsUsed);
            Assert.IsFalse(await model.UndoLastAsync());
            Assert.IsFalse(await model.EditPuttAsync(0, 4));
            Assert.IsNull(await model.SubmitAsync());
            var pendingSaveCount = active.SaveCallCount;
            await model.FlushAutosaveAsync();
            Assert.AreEqual(pendingSaveCount, active.SaveCallCount);
            await Assert.ThrowsAsync<InvalidOperationException>(() => model.DiscardAsync());
        }
        finally { allowSave.SetResult(); }

        Assert.IsTrue(await edit);
        Assert.IsFalse(model.IsBusy);
        Assert.AreEqual(5, model.PuttsUsed);
        Assert.AreEqual("1", model.TotalPuttsText);
        Assert.AreEqual(1, model.RecordedPutts.Single().Putts);
        Assert.AreEqual("Putt 2 af 3", model.ProgressText);
        Assert.AreEqual(5, active.Session!.PuttsUsed);
    }

    [TestMethod]
    public async Task ViewModelFailedCorrectionRetainsReviewResultAndAllowsRetry()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(3, 1, 4, PuttingTrainingDistanceDistribution.Random);
        var model = new PuttingGameViewModel(service, activeRepository: active);
        model.Resume(service.Capture(3));
        await model.SubmitAsync();
        model.PuttsUsed = 5;
        var correctionSaves = 0;
        active.SaveHandler = () =>
        {
            if (++correctionSaves == 2) throw new IOException("disk full");
            return Task.CompletedTask;
        };

        Assert.IsFalse(await model.EditPuttAsync(0, 1));

        Assert.IsTrue(model.HasError);
        Assert.IsFalse(model.IsBusy);
        Assert.AreEqual(5, model.PuttsUsed);
        Assert.AreEqual("3", model.TotalPuttsText);
        Assert.AreEqual(3, model.RecordedPutts.Single().Putts);
        Assert.AreEqual(3, active.Session!.CompletedPutts.Single().Putts);
        Assert.IsTrue(model.CanReview);
        active.SaveHandler = null;
        Assert.IsTrue(await model.EditPuttAsync(0, 1));
        Assert.IsFalse(model.HasError);
        Assert.AreEqual(1, model.RecordedPutts.Single().Putts);
        Assert.AreEqual(5, model.PuttsUsed);
    }

    [TestMethod]
    public async Task DiscardBlocksCorrectionsUntilDraftDeletionFinishes()
    {
        var active = new MemoryActiveRepository();
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(3, 1, 4, PuttingTrainingDistanceDistribution.Random);
        var model = new PuttingGameViewModel(service, activeRepository: active);
        model.Resume(service.Capture(2));
        await model.SubmitAsync();
        var deleteStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDelete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        active.DeleteHandler = async () =>
        {
            deleteStarted.SetResult();
            await allowDelete.Task;
        };
        var discard = model.DiscardAsync();
        await deleteStarted.Task;
        try
        {
            Assert.IsTrue(model.IsBusy);
            Assert.IsFalse(model.CanReview);
            Assert.IsFalse(await model.EditPuttAsync(0, 1));
            Assert.IsFalse(await model.UndoLastAsync());
            Assert.IsNull(await model.SubmitAsync());
        }
        finally { allowDelete.SetResult(); }
        await discard;
        Assert.IsNull(active.Session);
        Assert.IsFalse(model.IsActive);
    }

    private sealed class MemoryActiveRepository : IActivePuttingGameRepository
    {
        public ActivePuttingGameSession? Session { get; private set; }
        public int SaveCallCount { get; private set; }
        public Exception? SaveException { get; set; }
        public Func<Task>? SaveHandler { get; set; }
        public Func<Task>? DeleteHandler { get; set; }

        public Task<ActivePuttingGameSession?> GetAsync() => Task.FromResult(Session);

        public async Task SaveAsync(ActivePuttingGameSession session)
        {
            SaveCallCount++;
            if (SaveException is not null) throw SaveException;
            if (SaveHandler is not null) await SaveHandler();
            Session = session;
        }

        public async Task DeleteAsync()
        {
            if (DeleteHandler is not null) await DeleteHandler();
            Session = null;
        }
    }
}
