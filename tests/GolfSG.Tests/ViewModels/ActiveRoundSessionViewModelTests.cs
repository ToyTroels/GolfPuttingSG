using GolfSG.Application.Rounds;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class ActiveRoundSessionViewModelTests
{
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
}
