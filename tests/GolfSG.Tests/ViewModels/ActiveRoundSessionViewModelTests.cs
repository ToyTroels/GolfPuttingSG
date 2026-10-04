using GolfSG.Application.Rounds;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class ActiveRoundSessionViewModelTests
{
    [TestMethod]
    public async Task FlushDuringBlockedSavePersistsNewerEditsAndHole()
    {
        var repository = new DelayedActiveSessionRepository();
        var viewModel = new RoundInputViewModel(new TestRoundRepository(), activeRoundSessionRepository: repository);
        await viewModel.StartRoundAsync();
        viewModel.Holes[0].FirstPuttDistanceMeters = 2;
        repository.BlockNextSave = true;
        var oldSave = viewModel.FlushAutosaveAsync();
        try
        {
            await repository.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.Holes[0].FirstPuttDistanceMeters = 4;
            viewModel.SetCurrentHole(3);
            var latestSave = viewModel.FlushAutosaveAsync();
            Assert.IsFalse(latestSave.IsCompleted);
            repository.ReleaseSave.TrySetResult();
            await Task.WhenAll(oldSave, latestSave).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsNotNull(repository.Session);
            Assert.AreEqual(3, repository.Session.CurrentHoleNumber);
            Assert.AreEqual(4, repository.Session.Draft.Holes[0].FirstPuttDistanceMeters);
        }
        finally
        {
            repository.ReleaseSave.TrySetResult();
            await oldSave;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EndingRoundDuringBlockedSaveDoesNotRestoreActiveSession(bool finish)
    {
        var repository = new DelayedActiveSessionRepository();
        var history = new TestRoundRepository();
        var viewModel = new RoundInputViewModel(history, activeRoundSessionRepository: repository);
        await viewModel.StartRoundAsync();
        viewModel.Holes[0].FirstPuttDistanceMeters = 2;
        repository.BlockNextSave = true;
        var oldSave = viewModel.FlushAutosaveAsync();
        try
        {
            await repository.SaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            viewModel.Holes[0].FirstPuttDistanceMeters = 4;
            Task ending;
            if (finish)
            {
                ending = viewModel.SaveAsync();
            }
            else
            {
                ending = viewModel.AbandonActiveRoundAsync();
            }
            var queuedSave = viewModel.FlushAutosaveAsync();
            Assert.IsFalse(ending.IsCompleted);
            repository.ReleaseSave.TrySetResult();
            await Task.WhenAll(oldSave, ending, queuedSave).WaitAsync(TimeSpan.FromSeconds(5));
            await viewModel.FlushAutosaveAsync();
            Assert.IsNull(repository.Session);
            Assert.AreEqual(1, repository.DeleteCallCount);
            Assert.AreEqual(finish ? 1 : 0, history.SaveCallCount);
            if (finish)
            {
                Assert.IsNotNull(history.SavedRound);
                Assert.AreEqual(4, history.SavedRound.Holes[0].FirstPuttDistanceMeters);
            }
        }
        finally
        {
            repository.ReleaseSave.TrySetResult();
            await oldSave;
        }
    }

    [TestMethod]
    public async Task ApproachCarryForwardKeepsRoundTotalsAndAutosaveConsistent()
    {
        var repository = new TestActiveRoundSessionRepository();
        var viewModel = new RoundInputViewModel(new TestRoundRepository(), activeRoundSessionRepository: repository);
        viewModel.TrackApproach = true;
        await viewModel.StartRoundAsync();
        var hole = viewModel.Holes[0];
        hole.ApproachStartDistanceYards = 100;
        hole.SelectApproachEndLie("Green");
        var summaryUpdates = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RoundInputViewModel.TotalSgText))
            {
                summaryUpdates++;
            }
        };
        hole.ApproachEndDistance = 3;
        Assert.AreEqual(3, hole.FirstPuttDistanceMeters, 0.001);
        Assert.IsTrue(summaryUpdates > 0 && summaryUpdates <= 2,
            $"Carry-forward should produce at most two summary updates, got {summaryUpdates}.");
        var expected = hole.ToHole();
        await viewModel.FlushAutosaveAsync();
        Assert.IsNotNull(repository.Session);
        Assert.AreEqual(3, repository.Session.Draft.Holes[0].FirstPuttDistanceMeters, 0.001);
        Assert.AreEqual(expected.StrokesGainedApproach,
            repository.Session.Draft.Holes[0].StrokesGainedApproach, 0.001);
        Assert.AreEqual(UiFormat.Sg(expected.StrokesGainedPutting + expected.StrokesGainedApproach), viewModel.TotalSgText);
    }

    [TestMethod]
    public async Task OneHoleEditRefreshesRoundSummaryOnceAndPersistsInput()
    {
        var activeRepository = new TestActiveRoundSessionRepository();
        var viewModel = new RoundInputViewModel(
            new TestRoundRepository(), activeRoundSessionRepository: activeRepository);
        await viewModel.StartRoundAsync();
        var summaryUpdates = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RoundInputViewModel.TotalSgText))
            {
                summaryUpdates++;
            }
        };

        viewModel.Holes[0].FirstPuttDistanceMeters = 3;

        Assert.AreEqual(1, summaryUpdates);
        Assert.AreEqual(1, viewModel.CompletedHoleCount);
        await viewModel.FlushAutosaveAsync();
        Assert.IsNotNull(activeRepository.Session);
        Assert.AreEqual(3, activeRepository.Session.Draft.Holes[0].FirstPuttDistanceMeters);
    }

    [TestMethod]
    public async Task HoleNavigationUpdatesImmediatelyAndFlushPersistsLatestHole()
    {
        var activeRepository = new TestActiveRoundSessionRepository();
        var viewModel = new RoundInputViewModel(
            new TestRoundRepository(),
            activeRoundSessionRepository: activeRepository);
        Assert.IsTrue(await viewModel.StartRoundAsync());
        var savesBeforeNavigation = activeRepository.SaveCallCount;

        viewModel.Holes[0].FirstPuttDistanceMeters = 4;
        viewModel.SetCurrentHole(2);
        viewModel.SetCurrentHole(3);

        Assert.AreEqual(3, viewModel.ResumeHoleNumber);
        Assert.AreEqual(savesBeforeNavigation, activeRepository.SaveCallCount);

        await viewModel.FlushAutosaveAsync();
        Assert.IsNotNull(activeRepository.Session);
        Assert.AreEqual(3, activeRepository.Session.CurrentHoleNumber);
        Assert.AreEqual(4, activeRepository.Session.Draft.Holes[0].FirstPuttDistanceMeters);
    }

    [TestMethod]
    public async Task StartAndHoleChangesPersistLatestSession()
    {
        var activeRepository = new TestActiveRoundSessionRepository();
        var viewModel = new RoundInputViewModel(
            new TestRoundRepository(),
            activeRoundSessionRepository: activeRepository);

        Assert.IsTrue(await viewModel.StartRoundAsync());
        viewModel.Holes[0].FirstPuttDistanceMeters = 2.5;
        viewModel.Holes[0].Putts = 1;
        await viewModel.SetCurrentHoleAsync(3);

        Assert.IsNotNull(activeRepository.Session);
        Assert.AreEqual(3, activeRepository.Session.CurrentHoleNumber);
        Assert.AreEqual(2.5, activeRepository.Session.Draft.Holes[0].FirstPuttDistanceMeters, 0.001);
        Assert.AreEqual(1, activeRepository.Session.Draft.Holes[0].Putts);
    }

    [TestMethod]
    public async Task LoadActiveRestoresRoundConfigurationDataAndResumeHole()
    {
        var holes = Enumerable.Range(1, 9)
            .Select(number => number == 2
                ? StrokesGainedCalculator.BuildHole(number, 3.2, 2)
                : StrokesGainedCalculator.BuildHole(number, 0, 0))
            .ToList();
        var session = new ActiveRoundSession(
            new RoundDraft(
                "resume-me",
                new DateTime(2026, 7, 25),
                holes,
                new RoundTrackingOptions(true, true, false),
                9),
            2,
            DateTimeOffset.UtcNow);
        var viewModel = new RoundInputViewModel(
            new TestRoundRepository(),
            activeRoundSessionRepository: new TestActiveRoundSessionRepository(session));

        var loaded = await viewModel.LoadActiveAsync();

        Assert.IsTrue(loaded);
        Assert.IsTrue(viewModel.IsRoundVisible);
        Assert.AreEqual("Igangværende runde", viewModel.ScreenTitle);
        Assert.AreEqual(9, viewModel.HoleCount);
        Assert.AreEqual(2, viewModel.ResumeHoleNumber);
        Assert.IsTrue(viewModel.TrackPutting);
        Assert.IsTrue(viewModel.TrackApproach);
        Assert.AreEqual(3.2, viewModel.Holes[1].FirstPuttDistanceMeters, 0.001);
    }

    [TestMethod]
    public async Task FinalSaveMovesRoundToHistoryAndClearsActiveSession()
    {
        var roundRepository = new TestRoundRepository();
        var activeRepository = new TestActiveRoundSessionRepository();
        var viewModel = new RoundInputViewModel(
            roundRepository,
            activeRoundSessionRepository: activeRepository);
        await viewModel.StartRoundAsync();
        viewModel.Holes[0].FirstPuttDistanceMeters = 2;
        await viewModel.FlushAutosaveAsync();

        var roundId = await viewModel.SaveAsync();

        Assert.IsFalse(string.IsNullOrWhiteSpace(roundId));
        Assert.IsNotNull(roundRepository.SavedRound);
        Assert.IsNull(activeRepository.Session);
        Assert.AreEqual(1, activeRepository.DeleteCallCount);
    }

    [TestMethod]
    public async Task FailedFinalSaveKeepsActiveSessionForRecovery()
    {
        var roundRepository = new TestRoundRepository
        {
            SaveException = new IOException("History unavailable.")
        };
        var activeRepository = new TestActiveRoundSessionRepository();
        var viewModel = new RoundInputViewModel(
            roundRepository,
            activeRoundSessionRepository: activeRepository);
        await viewModel.StartRoundAsync();

        var roundId = await viewModel.SaveAsync();

        Assert.AreEqual(string.Empty, roundId);
        Assert.IsNotNull(activeRepository.Session);
        Assert.AreEqual(0, activeRepository.DeleteCallCount);
    }

    [TestMethod]
    public async Task AbandonDeletesSessionWithoutCreatingHistory()
    {
        var roundRepository = new TestRoundRepository();
        var activeRepository = new TestActiveRoundSessionRepository();
        var viewModel = new RoundInputViewModel(
            roundRepository,
            activeRoundSessionRepository: activeRepository);
        await viewModel.StartRoundAsync();

        var abandoned = await viewModel.AbandonActiveRoundAsync();

        Assert.IsTrue(abandoned);
        Assert.IsNull(activeRepository.Session);
        Assert.AreEqual(0, roundRepository.SaveCallCount);
    }
    private sealed class DelayedActiveSessionRepository : IActiveRoundSessionRepository
    {
        public ActiveRoundSession? Session { get; private set; }
        public bool BlockNextSave { get; set; }
        public int DeleteCallCount { get; private set; }
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string ActiveStoragePath => string.Empty;
        public Task<ActiveRoundSession?> GetAsync() => Task.FromResult(Session);

        public async Task SaveAsync(ActiveRoundSession session)
        {
            if (BlockNextSave)
            {
                BlockNextSave = false;
                SaveStarted.TrySetResult();
                await ReleaseSave.Task;
            }
            Session = session;
        }

        public Task DeleteAsync()
        {
            DeleteCallCount++;
            Session = null;
            return Task.CompletedTask;
        }
    }
}
