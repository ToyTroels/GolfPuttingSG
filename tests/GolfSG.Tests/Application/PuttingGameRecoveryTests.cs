using GolfSG.Application.Putting;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Infrastructure.Persistence;

namespace GolfSG.Tests;

[TestClass]
public sealed class PuttingGameRecoveryTests
{
    [TestMethod]
    public async Task RestartRestoresExactRandomOrderProgressInputAndIdentity()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new FileActivePuttingGameRepository(folder);
            var history = new TestRoundRepository();
            var original = new PuttingGameSessionService(history, repository);
            original.StartBenchmark(PuttingGame.ShortBenchmark, PuttingDistanceOrder.Random);
            var model = new PuttingGameViewModel(original, activeRepository: repository);
            model.Resume(original.Capture(2));
            await model.SubmitAsync();
            model.PuttsUsed = 4;
            await model.FlushAutosaveAsync();

            var saved = await new FileActivePuttingGameRepository(folder).GetAsync();
            Assert.IsNotNull(saved);
            var restarted = new PuttingGameSessionService(history, repository);
            var resumed = new PuttingGameViewModel(restarted, activeRepository: repository);
            resumed.Resume(saved);
            Assert.AreEqual(original.RoundId, restarted.RoundId);
            CollectionAssert.AreEqual(original.Distances.ToArray(), restarted.Distances.ToArray());
            Assert.AreEqual(1, restarted.CurrentIndex);
            Assert.AreEqual(4, resumed.PuttsUsed);
            Assert.AreEqual(original.Definition.PresetId, restarted.Definition.PresetId);
            Assert.AreEqual(original.CurrentDistanceMeters, restarted.CurrentDistanceMeters);
            Assert.IsTrue(resumed.IsActive);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task CorruptActiveFileRecoversBackupAndDiscardRemovesBoth()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new FileActivePuttingGameRepository(folder);
            var service = new PuttingGameSessionService(new TestRoundRepository(), repository);
            service.Start(PuttingGame.TourRoundMode);
            await repository.SaveAsync(service.Capture(2));
            await service.SubmitAsync(3);
            await File.WriteAllTextAsync(repository.ActiveStoragePath, "broken json");
            var recovered = await repository.GetAsync();
            Assert.IsNotNull(recovered);
            Assert.AreEqual(service.RoundId, recovered.RoundId);
            Assert.HasCount(0, recovered.CompletedPutts);
            await repository.DeleteAsync();
            Assert.IsNull(await repository.GetAsync());
            Assert.IsFalse(File.Exists(repository.ActiveStoragePath + ".bak"));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [TestMethod]
    public async Task FailedProgressSaveDoesNotAdvanceOrLoseRecordedInput()
    {
        var active = new MemoryActiveRepository { FailSave = true };
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.StartTraining(3, 1, 3, PuttingTrainingDistanceDistribution.BellCurve);
        await Assert.ThrowsAsync<IOException>(() => service.SubmitAsync(4));
        Assert.AreEqual(0, service.CurrentIndex);
        Assert.HasCount(0, service.CompletedPutts);
        active.FailSave = false;
        await service.SubmitAsync(4);
        Assert.AreEqual(1, service.CurrentIndex);
        Assert.AreEqual(4, active.Session!.CompletedPutts[0].Putts);
    }

    [TestMethod]
    public async Task FailedFinalSaveKeepsDraftAndRetryCompletesOnlyOneRound()
    {
        var active = new MemoryActiveRepository();
        var history = new TestRoundRepository { SaveException = new IOException("disk full") };
        var service = new PuttingGameSessionService(history, active);
        service.StartTraining(2, 1, 3, PuttingTrainingDistanceDistribution.BellCurve);
        await service.SubmitAsync(1);
        await Assert.ThrowsAsync<IOException>(() => service.SubmitAsync(3));
        Assert.AreEqual(1, service.CurrentIndex);
        Assert.IsNotNull(active.Session);
        history.SaveException = null;
        var result = await service.SubmitAsync(3);
        Assert.IsTrue(result.IsComplete);
        Assert.IsNull(active.Session);
        Assert.HasCount(1, history.Rounds);
        Assert.HasCount(2, history.Rounds[0].Holes);
    }

    [TestMethod]
    public async Task DiscardWaitsForQueuedInputSavesAndDoesNotResurrectDraft()
    {
        var allowSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = new MemoryActiveRepository { SaveHandler = () => allowSave.Task };
        var service = new PuttingGameSessionService(new TestRoundRepository(), active);
        service.Start(PuttingGame.TourRoundMode);
        var model = new PuttingGameViewModel(service, activeRepository: active);
        model.Resume(service.Capture(2));
        model.PuttsUsed = 4;
        var discard = model.DiscardAsync();
        Assert.IsFalse(discard.IsCompleted);
        allowSave.SetResult();
        await discard;
        Assert.IsNull(active.Session);
        Assert.IsFalse(model.IsActive);
    }

    [TestMethod]
    public async Task AutosaveFailurePreventsCloseUntilRetrySucceeds()
    {
        var active = new MemoryActiveRepository { FailSave = true };
        var model = new PuttingGameViewModel(new TestRoundRepository(), activeRepository: active);
        model.Start(PuttingGame.TourRoundMode);
        model.PuttsUsed = 5;
        await Assert.ThrowsAsync<IOException>(() => model.FlushAutosaveAsync());
        Assert.IsTrue(model.HasError);
        Assert.IsTrue(model.IsActive);
        active.FailSave = false;
        await model.FlushAutosaveAsync();
        Assert.IsFalse(model.HasError);
        Assert.AreEqual(5, active.Session!.PuttsUsed);
    }

    [TestMethod]
    public async Task CleanupFailureCanRetryFinalSaveWithoutDuplicatingHistory()
    {
        var active = new MemoryActiveRepository { FailDelete = true };
        var history = new TestRoundRepository();
        var service = new PuttingGameSessionService(history, active);
        service.StartTraining(2, 1, 3, PuttingTrainingDistanceDistribution.BellCurve);
        await service.SubmitAsync(2);
        await Assert.ThrowsAsync<IOException>(() => service.SubmitAsync(3));
        Assert.IsFalse(service.IsComplete);
        Assert.IsNotNull(active.Session);
        active.FailDelete = false;
        await service.SubmitAsync(3);
        Assert.HasCount(1, history.Rounds);
        Assert.IsNull(active.Session);
    }

    [TestMethod]
    public async Task TourAndLadderRecoveryPreserveTheirScoringAndDistances()
    {
        foreach (var ladder in new[] { false, true })
        {
            var active = new MemoryActiveRepository();
            var original = new PuttingGameSessionService(new TestRoundRepository(), active);
            if (ladder) original.StartLadderBenchmark(PuttingGame.ShortLadderBenchmark);
            else original.Start(PuttingGame.TourRoundMode);
            await original.SubmitAsync(2);
            var restored = new PuttingGameSessionService(new TestRoundRepository(), active);
            restored.Restore(active.Session!);
            Assert.AreEqual(original.DistancesAreMeters, restored.DistancesAreMeters);
            Assert.AreEqual(original.TargetPutts, restored.TargetPutts);
            Assert.AreEqual(original.CurrentExpectedPutts, restored.CurrentExpectedPutts);
            Assert.AreEqual(original.Definition.BenchmarkType, restored.Definition.BenchmarkType);
        }
    }

    private sealed class MemoryActiveRepository : IActivePuttingGameRepository
    {
        public ActivePuttingGameSession? Session { get; private set; }
        public bool FailSave { get; set; }
        public bool FailDelete { get; set; }
        public Func<Task>? SaveHandler { get; set; }
        public Task<ActivePuttingGameSession?> GetAsync() => Task.FromResult(Session);
        public async Task SaveAsync(ActivePuttingGameSession session)
        {
            if (FailSave) throw new IOException("disk full");
            if (SaveHandler is not null) await SaveHandler();
            Session = session;
        }
        public Task DeleteAsync()
        {
            if (FailDelete) throw new IOException("disk full");
            Session = null;
            return Task.CompletedTask;
        }
    }
}
