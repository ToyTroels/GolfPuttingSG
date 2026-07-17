using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class RoundInputRegressionTests
{
    [TestMethod]
    public void NewRoundHasStablePuttingOnlyDefaults()
    {
        var viewModel = new RoundInputViewModel(new TestRoundRepository());

        Assert.AreEqual(18, viewModel.HoleCount);
        Assert.HasCount(18, viewModel.Holes);
        Assert.IsTrue(viewModel.TrackPutting);
        Assert.IsFalse(viewModel.TrackApproach);
        Assert.IsFalse(viewModel.TrackAroundGreen);
        Assert.IsTrue(viewModel.IsSetupVisible);
        Assert.IsFalse(viewModel.IsRoundVisible);
        Assert.AreEqual("0 / 18 huller registreret", viewModel.RoundProgressText);
    }

    [TestMethod]
    public void TrackingConfigurationNeverAllowsAllCategoriesToBeDisabled()
    {
        var viewModel = new RoundInputViewModel(new TestRoundRepository())
        {
            TrackApproach = true,
            TrackPutting = false
        };

        viewModel.TrackApproach = false;

        Assert.IsTrue(viewModel.TrackApproach);
        Assert.IsFalse(viewModel.TrackPutting);
        Assert.IsTrue(viewModel.Holes.All(hole => hole.TrackApproach));
    }

    [TestMethod]
    public async Task LoadExistingRoundRestoresEditStateTrackingAndHoleData()
    {
        var hole = StrokesGainedCalculator.BuildHole(1, 2.5, 2);
        var round = new Round(
            "edit-me",
            new DateTime(2026, 6, 1),
            [hole],
            new RoundTrackingOptions(true, true, false),
            9,
            EndedEarly: true);
        var viewModel = new RoundInputViewModel(new TestRoundRepository([round]));

        await viewModel.LoadAsync(round.Id);

        Assert.AreEqual("Rediger runde", viewModel.ScreenTitle);
        Assert.IsFalse(viewModel.IsSetupVisible);
        Assert.IsTrue(viewModel.IsRoundVisible);
        Assert.AreEqual(9, viewModel.HoleCount);
        Assert.HasCount(9, viewModel.Holes);
        Assert.IsTrue(viewModel.TrackPutting);
        Assert.IsTrue(viewModel.TrackApproach);
        Assert.IsFalse(viewModel.TrackAroundGreen);
        Assert.AreEqual(2.5, viewModel.Holes[0].FirstPuttDistanceMeters, 0.001);
        Assert.AreEqual(2, viewModel.Holes[0].Putts);
    }

    [TestMethod]
    public async Task SavingEditedRoundPreservesIdentityDateAndTrackingOptions()
    {
        var originalDate = new DateTime(2026, 5, 20);
        var round = new Round(
            "stable-id",
            originalDate,
            [StrokesGainedCalculator.BuildHole(1, 3, 2)],
            new RoundTrackingOptions(true, false, true),
            9,
            EndedEarly: true);
        var repository = new TestRoundRepository([round]);
        var viewModel = new RoundInputViewModel(repository);
        await viewModel.LoadAsync(round.Id);

        viewModel.Holes[0].Putts = 1;
        var savedId = await viewModel.SaveAsync();

        Assert.AreEqual(round.Id, savedId);
        Assert.IsNotNull(repository.SavedRound);
        Assert.AreEqual(round.Id, repository.SavedRound.Id);
        Assert.AreEqual(originalDate, repository.SavedRound.Date);
        Assert.AreEqual(round.TrackingOptions, repository.SavedRound.TrackingOptions);
        Assert.AreEqual(1, repository.SavedRound.Holes[0].Putts);
    }

    [TestMethod]
    public void HoleCountCannotShrinkBelowHighestHoleWithTrackedInput()
    {
        var viewModel = new RoundInputViewModel(new TestRoundRepository());
        viewModel.Holes[17].FirstPuttDistanceMeters = 2;
        viewModel.Holes[17].Putts = 2;

        for (var index = 0; index < 30; index++)
        {
            viewModel.DecreaseHoleCount();
        }

        Assert.AreEqual(18, viewModel.HoleCount);
        Assert.HasCount(18, viewModel.Holes);
    }

    [TestMethod]
    public async Task SavingIncompleteNineHoleRoundMarksItEndedEarly()
    {
        var repository = new TestRoundRepository();
        var viewModel = new RoundInputViewModel(repository);
        for (var index = 0; index < 9; index++)
        {
            viewModel.DecreaseHoleCount();
        }

        viewModel.Holes[0].FirstPuttDistanceMeters = 2;
        viewModel.Holes[0].Putts = 2;

        await viewModel.SaveAsync();

        Assert.IsNotNull(repository.SavedRound);
        Assert.AreEqual(9, repository.SavedRound.ConfiguredHoleCount);
        Assert.IsTrue(repository.SavedRound.EndedEarly);
        Assert.AreEqual(1, repository.SavedRound.CompletedHoleCount);
    }

    [TestMethod]
    public async Task RepeatedSaveWhileBusyDoesNotPersistTwice()
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
        var viewModel = new RoundInputViewModel(repository);

        var firstSave = viewModel.SaveAsync();
        await saveStarted.Task;
        var repeatedSave = await viewModel.SaveAsync();
        allowSave.SetResult();
        var firstId = await firstSave;

        Assert.AreEqual(string.Empty, repeatedSave);
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstId));
        Assert.AreEqual(1, repository.SaveCallCount);
    }
}
