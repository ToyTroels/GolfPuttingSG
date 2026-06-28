using GolfSG.Core.Models;
using GolfSG.Services;
using GolfSG.ViewModels;

namespace GolfSG.Tests;

[TestClass]
public sealed class PuttingGameViewModelTests
{
    [TestMethod]
    public void ConfiguredGameShowsCompactProgressAndRemainingPreview()
    {
        var viewModel = CreateConfiguredGame(5);

        Assert.IsTrue(viewModel.IsActive);
        Assert.AreEqual("Putt 1 af 5", viewModel.ProgressText);
        Assert.AreEqual("1/5", viewModel.ProgressCountText);
        Assert.AreEqual(0, viewModel.ProgressFraction);
        Assert.AreEqual("5 tilbage", viewModel.RemainingCountText);
        Assert.IsTrue(viewModel.RemainingDistancesPreviewText.StartsWith("N", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.HasRemainingDistanceDetails);
        Assert.IsFalse(viewModel.ShowRemainingDistanceDetails);
        Assert.AreEqual("Registrer putt", viewModel.PrimaryActionText);
    }

    [TestMethod]
    public async Task ProgressPreviewAndActionTextUpdateAsPuttsAreSubmitted()
    {
        var viewModel = CreateConfiguredGame(3);

        await viewModel.SubmitAsync();
        await viewModel.SubmitAsync();

        Assert.IsTrue(viewModel.IsActive);
        Assert.AreEqual("Putt 3 af 3", viewModel.ProgressText);
        Assert.AreEqual("3/3", viewModel.ProgressCountText);
        Assert.AreEqual(2.0 / 3.0, viewModel.ProgressFraction, 0.001);
        Assert.AreEqual("1 tilbage", viewModel.RemainingCountText);
        Assert.AreEqual("Sidste putt", viewModel.RemainingDistancesPreviewText);
        Assert.IsFalse(viewModel.HasRemainingDistanceDetails);
        Assert.AreEqual("Gem og afslut", viewModel.PrimaryActionText);
    }

    [TestMethod]
    public async Task FinalSubmitCompletesAndSavesPuttingGame()
    {
        var repository = new InMemoryRoundRepository();
        var viewModel = CreateConfiguredGame(2, repository);

        var firstSubmitRoundId = await viewModel.SubmitAsync();
        var completedRoundId = await viewModel.SubmitAsync();

        Assert.IsNull(firstSubmitRoundId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(completedRoundId));
        Assert.IsTrue(viewModel.IsComplete);
        Assert.IsFalse(viewModel.IsActive);
        Assert.AreEqual("0 tilbage", viewModel.RemainingCountText);
        Assert.HasCount(1, repository.Rounds);
        Assert.AreEqual(completedRoundId, repository.Rounds[0].Id);
        Assert.AreEqual(2, repository.Rounds[0].CompletedHoleCount);
    }

    private static PuttingGameViewModel CreateConfiguredGame(
        int puttCount,
        InMemoryRoundRepository? repository = null)
    {
        var viewModel = new PuttingGameViewModel(repository ?? new InMemoryRoundRepository())
        {
            HoleCountText = puttCount.ToString(),
            MinimumDistanceMetersText = "1",
            MaximumDistanceMetersText = "5"
        };

        Assert.IsTrue(viewModel.StartConfiguredGame());
        return viewModel;
    }

    private sealed class InMemoryRoundRepository : IRoundRepository
    {
        public List<Round> Rounds { get; } = [];

        public string ActiveStoragePath => string.Empty;

        public bool WasLastReadRecoveredFromBackup => false;

        public bool WasLastReadMigratedFromLegacyStorage => false;

        public string? LastMigrationSourcePath => null;

        public bool WasLastUnreadableActiveFilePreserved => false;

        public string? LastPreservedUnreadableFilePath => null;

        public Task<IReadOnlyList<Round>> GetRoundsAsync() => Task.FromResult<IReadOnlyList<Round>>(Rounds);

        public Task<Round?> GetRoundAsync(string roundId) =>
            Task.FromResult(Rounds.FirstOrDefault(round => round.Id == roundId));

        public Task SaveRoundAsync(Round round)
        {
            Rounds.RemoveAll(existingRound => existingRound.Id == round.Id);
            Rounds.Add(round);
            return Task.CompletedTask;
        }

        public Task DeleteRoundAsync(string roundId)
        {
            Rounds.RemoveAll(round => round.Id == roundId);
            return Task.CompletedTask;
        }

        public Task ExportRoundsAsync(string destinationPath) => Task.CompletedTask;

        public Task ImportRoundsAsync(string sourcePath) => Task.CompletedTask;
    }
}
