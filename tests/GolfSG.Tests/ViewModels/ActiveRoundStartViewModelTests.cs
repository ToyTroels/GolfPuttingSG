using GolfSG.Application.Rounds;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class ActiveRoundStartViewModelTests
{
    [TestMethod]
    public async Task LoadExposesActiveRoundWithoutAddingItToHistory()
    {
        var activeRepository = new TestActiveRoundSessionRepository(CreateSession("active"));
        var viewModel = new StartViewModel(
            new TestRoundRepository(),
            activeRoundSessionRepository: activeRepository);

        await viewModel.LoadAsync();

        Assert.IsTrue(viewModel.HasActiveRound);
        Assert.IsNotNull(viewModel.ActiveRoundSession);
        Assert.AreEqual("active", viewModel.ActiveRoundSession.Draft.Id);
        Assert.IsEmpty(viewModel.Rounds);
        StringAssert.Contains(viewModel.ActiveRoundDetailText, "1/9 huller");
    }

    [TestMethod]
    public async Task LoadRemovesStaleSessionWhenRoundAlreadyExistsInHistory()
    {
        var completedRound = new Round(
            "same-id",
            new DateTime(2026, 7, 25),
            [StrokesGainedCalculator.BuildHole(1, 2, 2)],
            RoundTrackingOptions.PuttingOnly,
            1);
        var activeRepository = new TestActiveRoundSessionRepository(CreateSession("same-id"));
        var viewModel = new StartViewModel(
            new TestRoundRepository([completedRound]),
            activeRoundSessionRepository: activeRepository);

        await viewModel.LoadAsync();

        Assert.IsFalse(viewModel.HasActiveRound);
        Assert.AreEqual(1, activeRepository.DeleteCallCount);
        Assert.HasCount(1, viewModel.Rounds);
    }

    [TestMethod]
    public async Task AbandonRemovesActiveSession()
    {
        var activeRepository = new TestActiveRoundSessionRepository(CreateSession("active"));
        var viewModel = new StartViewModel(
            new TestRoundRepository(),
            activeRoundSessionRepository: activeRepository);
        await viewModel.LoadAsync();

        var abandoned = await viewModel.AbandonActiveRoundAsync();

        Assert.IsTrue(abandoned);
        Assert.IsFalse(viewModel.HasActiveRound);
        Assert.IsNull(activeRepository.Session);
    }

    private static ActiveRoundSession CreateSession(string id)
    {
        var holes = Enumerable.Range(1, 9)
            .Select(number => number == 1
                ? StrokesGainedCalculator.BuildHole(number, 2, 2)
                : StrokesGainedCalculator.BuildHole(number, 0, 0))
            .ToList();
        return new ActiveRoundSession(
            new RoundDraft(
                id,
                new DateTime(2026, 7, 25),
                holes,
                RoundTrackingOptions.PuttingOnly,
                9),
            2,
            DateTimeOffset.UtcNow);
    }
}
