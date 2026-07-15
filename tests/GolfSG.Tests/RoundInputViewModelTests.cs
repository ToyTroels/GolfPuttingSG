using GolfSG.Core.Models;
using GolfSG.Services;
using GolfSG.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class RoundInputViewModelTests
{
    [TestMethod]
    public void TrackingTogglesBatchRoundSummaryNotifications()
    {
        var viewModel = new RoundInputViewModel(new SuccessfulRoundRepository());
        var totalSgNotifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RoundInputViewModel.TotalSgText))
            {
                totalSgNotifications++;
            }
        };

        viewModel.TrackApproach = true;

        Assert.IsTrue(viewModel.Holes.All(hole => hole.TrackApproach));
        Assert.AreEqual(2, totalSgNotifications);

        totalSgNotifications = 0;
        viewModel.TrackAroundGreen = true;

        Assert.IsTrue(viewModel.Holes.All(hole => hole.TrackAroundGreen));
        Assert.AreEqual(2, totalSgNotifications);
    }

    [TestMethod]
    public async Task SaveFailureSetsErrorAndReturnsEmptyRoundId()
    {
        var viewModel = new RoundInputViewModel(new FailingRoundRepository());

        var roundId = await viewModel.SaveAsync();

        Assert.AreEqual(string.Empty, roundId);
        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, "gemmes");
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(viewModel.CanSave);
    }

    [TestMethod]
    public async Task SaveExposesBusyStateWhileRepositoryIsSaving()
    {
        var repository = new BlockingRoundRepository();
        var viewModel = new RoundInputViewModel(repository);

        var saveTask = viewModel.SaveAsync();
        await repository.SaveStarted.Task;

        Assert.IsTrue(viewModel.IsBusy);
        Assert.IsFalse(viewModel.CanSave);
        Assert.AreEqual("Gemmer...", viewModel.SaveButtonText);

        repository.AllowSave.SetResult();
        var roundId = await saveTask;

        Assert.IsFalse(string.IsNullOrWhiteSpace(roundId));
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(viewModel.CanSave);
    }

    private sealed class FailingRoundRepository : EmptyRoundRepository
    {
        public override Task SaveRoundAsync(Round round) =>
            throw new IOException("Save failed.");
    }

    private sealed class BlockingRoundRepository : EmptyRoundRepository
    {
        public TaskCompletionSource SaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task SaveRoundAsync(Round round)
        {
            SaveStarted.SetResult();
            await AllowSave.Task;
        }
    }

    private sealed class SuccessfulRoundRepository : EmptyRoundRepository
    {
    }

    private abstract class EmptyRoundRepository : IRoundRepository
    {
        public string ActiveStoragePath => string.Empty;

        public bool WasLastReadRecoveredFromBackup => false;

        public bool WasLastReadMigratedFromLegacyStorage => false;

        public string? LastMigrationSourcePath => null;

        public bool WasLastUnreadableActiveFilePreserved => false;

        public string? LastPreservedUnreadableFilePath => null;

        public Task<IReadOnlyList<Round>> GetRoundsAsync() => Task.FromResult<IReadOnlyList<Round>>([]);

        public Task<Round?> GetRoundAsync(string roundId) => Task.FromResult<Round?>(null);

        public virtual Task SaveRoundAsync(Round round) => Task.CompletedTask;

        public Task DeleteRoundAsync(string roundId) => Task.CompletedTask;

        public Task ExportRoundsAsync(string destinationPath) => Task.CompletedTask;

        public Task ImportRoundsAsync(string sourcePath) => Task.CompletedTask;
    }
}
