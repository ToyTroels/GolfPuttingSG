using GolfSG.Application.Common;
using GolfSG.Application.Putting;
using GolfSG.Application.Rounds;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Services;

namespace GolfSG.Tests;

[TestClass]
public sealed class ApplicationServiceTests
{
    [TestMethod]
    public async Task RoundApplicationService_CreatesSummarizesAndSavesPartialRound()
    {
        var repository = new RecordingRoundRepository();
        var service = new RoundApplicationService(repository);
        var completedHole = StrokesGainedCalculator.BuildHole(1, 2, 2);
        var draft = new RoundDraft(
            "round-1",
            new DateTime(2026, 7, 16),
            [completedHole],
            RoundTrackingOptions.PuttingOnly,
            9);

        var summary = service.Summarize(draft);
        var saved = await service.SaveAsync(draft);

        Assert.AreEqual(2, summary.TotalPutts);
        Assert.IsTrue(saved.EndedEarly);
        Assert.AreEqual(9, saved.ConfiguredHoleCount);
        Assert.AreSame(saved, repository.SavedRound);
    }

    [TestMethod]
    public async Task RoundApplicationService_LoadsExistingRoundAndClampsHoleCount()
    {
        var existing = Round.Empty() with { Id = "existing" };
        var repository = new RecordingRoundRepository(existing);
        var service = new RoundApplicationService(repository);

        var loaded = await service.LoadAsync(existing.Id);

        Assert.AreSame(existing, loaded);
        Assert.AreEqual(1, service.ClampHoleCount(-10));
        Assert.AreEqual(12, service.ClampHoleCount(9, highestEnteredHoleNumber: 12));
        Assert.AreEqual(36, service.ClampHoleCount(100));
    }

    [TestMethod]
    public async Task PuttingGameSessionService_ProgressesAndPersistsCompletedGame()
    {
        var repository = new RecordingRoundRepository();
        var service = new PuttingGameSessionService(repository);
        service.StartTraining(
            puttCount: 2,
            minimumDistanceMeters: 1,
            maximumDistanceMeters: 2,
            PuttingTrainingDistanceDistribution.BellCurve);

        var first = await service.SubmitAsync(1);
        var second = await service.SubmitAsync(2);

        Assert.IsFalse(first.IsComplete);
        Assert.IsTrue(second.IsComplete);
        Assert.AreEqual(service.RoundId, second.RoundId);
        Assert.HasCount(2, service.CompletedPutts);
        Assert.IsNotNull(repository.SavedRound);
        Assert.HasCount(2, repository.SavedRound.Holes);
        Assert.AreEqual("PuttingGame", repository.SavedRound.GameInfo?.Type);
    }

    [TestMethod]
    public async Task PuttingGameSessionService_RollsBackFinalPuttWhenSaveFails()
    {
        var service = new PuttingGameSessionService(new RecordingRoundRepository { FailSave = true });
        service.StartTraining(
            puttCount: 1,
            minimumDistanceMeters: 2,
            maximumDistanceMeters: 2,
            PuttingTrainingDistanceDistribution.BellCurve);

        await Assert.ThrowsExactlyAsync<IOException>(() => service.SubmitAsync(2));

        Assert.IsFalse(service.IsComplete);
        Assert.AreEqual(0, service.CurrentIndex);
        Assert.IsEmpty(service.CompletedPutts);
    }

    [TestMethod]
    public async Task AsyncActionGate_RejectsOverlappingActionAndAllowsNextAction()
    {
        var gate = new AsyncActionGate();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = gate.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
        });
        await entered.Task;

        var overlapping = await gate.RunAsync(() => Task.CompletedTask);
        release.SetResult();
        var firstCompleted = await first;
        var next = await gate.RunAsync(() => Task.CompletedTask);

        Assert.IsFalse(overlapping);
        Assert.IsTrue(firstCompleted);
        Assert.IsTrue(next);
    }

    private sealed class RecordingRoundRepository(Round? round = null) : IRoundRepository
    {
        public bool FailSave { get; init; }
        public Round? SavedRound { get; private set; }
        public string ActiveStoragePath => string.Empty;
        public bool WasLastReadRecoveredFromBackup => false;
        public bool WasLastReadMigratedFromLegacyStorage => false;
        public string? LastMigrationSourcePath => null;
        public bool WasLastUnreadableActiveFilePreserved => false;
        public string? LastPreservedUnreadableFilePath => null;

        public Task<IReadOnlyList<Round>> GetRoundsAsync() =>
            Task.FromResult<IReadOnlyList<Round>>(round is null ? [] : [round]);

        public Task<Round?> GetRoundAsync(string roundId) =>
            Task.FromResult(round?.Id == roundId ? round : null);

        public Task SaveRoundAsync(Round value)
        {
            if (FailSave)
            {
                throw new IOException("Save failed.");
            }

            SavedRound = value;
            return Task.CompletedTask;
        }

        public Task DeleteRoundAsync(string roundId) => Task.CompletedTask;
        public Task ExportRoundsAsync(string destinationPath) => Task.CompletedTask;
        public Task ImportRoundsAsync(string sourcePath) => Task.CompletedTask;
    }
}
