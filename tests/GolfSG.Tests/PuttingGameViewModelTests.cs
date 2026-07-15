using GolfSG.Core;
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
    public void ConfiguredGameDefaultsToTwentyPutts()
    {
        var viewModel = new PuttingGameViewModel(new InMemoryRoundRepository());

        Assert.AreEqual("20", viewModel.HoleCountText);
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

    [TestMethod]
    public async Task CustomGameUsesFeetInputButSavesMeters()
    {
        var repository = new InMemoryRoundRepository();
        var settings = new FixedDistanceUnitSettings(PuttingDistanceUnitPreference.Feet);
        var viewModel = new PuttingGameViewModel(repository, settings)
        {
            HoleCountText = "1",
            MinimumDistanceMetersText = "6",
            MaximumDistanceMetersText = "6"
        };

        Assert.IsTrue(viewModel.StartConfiguredGame());
        Assert.AreEqual("6,0 ft", viewModel.CurrentDistanceText);

        await viewModel.SubmitAsync();

        Assert.HasCount(1, repository.Rounds);
        Assert.AreEqual(DistanceConversions.FeetToMeters(6), repository.Rounds[0].Holes[0].FirstPuttDistanceMeters, 0.001);
    }

    [TestMethod]
    public async Task LadderBenchmarkSavesPresetMetadata()
    {
        var repository = new InMemoryRoundRepository();
        var viewModel = new PuttingGameViewModel(repository);
        viewModel.StartNormalLadderBenchmark();

        Assert.IsTrue(viewModel.IsActive);
        Assert.AreEqual("Normal ladder benchmark", viewModel.GameTitle);
        Assert.AreEqual("Putt 1 af 30", viewModel.ProgressText);
        Assert.AreEqual("1,0 m", viewModel.CurrentDistanceText);

        string? completedRoundId = null;
        for (var index = 0; index < 30; index++)
        {
            completedRoundId = await viewModel.SubmitAsync();
        }

        Assert.IsFalse(string.IsNullOrWhiteSpace(completedRoundId));
        Assert.HasCount(1, repository.Rounds);
        var gameInfo = repository.Rounds[0].GameInfo;
        Assert.IsNotNull(gameInfo);
        Assert.AreEqual("PuttingBenchmark", gameInfo.Type);
        Assert.AreEqual(PuttingBenchmarkType.Ladder, gameInfo.BenchmarkType);
        Assert.AreEqual(PuttingGame.NormalLadderBenchmarkPresetId, gameInfo.PresetId);
        Assert.AreEqual(30, gameInfo.AttemptCount);
        Assert.AreEqual(1, gameInfo.StartDistanceMeters);
        Assert.AreEqual(6, gameInfo.EndDistanceMeters);
        Assert.AreEqual(1, gameInfo.DistanceStepMeters);
        Assert.AreEqual(5, gameInfo.PuttsPerDistance);
        Assert.IsTrue(repository.Rounds[0].Holes.Select(hole => hole.FirstPuttDistanceMeters)
            .SequenceEqual(repository.Rounds[0].Holes.Select(hole => hole.FirstPuttDistanceMeters).Order()));
    }

    [TestMethod]
    public void LadderBenchmarkHistorySummaryShowsPresetDetails()
    {
        var definition = PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalLadderBenchmark);
        var round = new Round(
            "ladder",
            new DateTime(2026, 6, 1),
            [PuttingGame.BuildPutt(1, definition.DistancesMeters[0], 2)],
            new RoundTrackingOptions(true, false, false, true, PuttingGame.NormalLadderBenchmark),
            1,
            false,
            PuttingGame.CreateRoundGameInfo(definition));

        var item = new RoundListItemViewModel(round);

        StringAssert.Contains(item.DetailText, "Normal ladder benchmark");
        StringAssert.Contains(item.DetailText, "1,0 m-6,0 m");
        StringAssert.Contains(item.DetailText, "1,0 m trin");
        StringAssert.Contains(item.DetailText, "5 pr. afstand");
    }

    [TestMethod]
    public void LadderBenchmarkHistorySummaryShowsFeetWhenPreferred()
    {
        var definition = PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalLadderBenchmark);
        var round = new Round(
            "ladder-feet",
            new DateTime(2026, 6, 1),
            [PuttingGame.BuildPutt(1, definition.DistancesMeters[0], 2)],
            new RoundTrackingOptions(true, false, false, true, PuttingGame.NormalLadderBenchmark),
            1,
            false,
            PuttingGame.CreateRoundGameInfo(definition));
        var settings = new FixedDistanceUnitSettings(PuttingDistanceUnitPreference.Feet);

        var item = new RoundListItemViewModel(round, settings);

        StringAssert.Contains(item.DetailText, "3,3 ft-19,7 ft");
        StringAssert.Contains(item.DetailText, "3,3 ft trin");
    }

    [TestMethod]
    public async Task FinalSubmitSaveFailureKeepsGameActiveAndShowsError()
    {
        var repository = new InMemoryRoundRepository { ThrowOnSave = true };
        var viewModel = CreateConfiguredGame(1, repository);

        var completedRoundId = await viewModel.SubmitAsync();

        Assert.IsNull(completedRoundId);
        Assert.IsTrue(viewModel.IsActive);
        Assert.IsFalse(viewModel.IsComplete);
        Assert.IsTrue(viewModel.HasError);
        StringAssert.Contains(viewModel.ErrorMessage, "gemmes");
        Assert.AreEqual("Putt 1 af 1", viewModel.ProgressText);
        Assert.AreEqual("Gem og afslut", viewModel.PrimaryActionText);
        Assert.HasCount(0, repository.Rounds);
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

        public bool ThrowOnSave { get; init; }

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
            if (ThrowOnSave)
            {
                throw new IOException("Save failed.");
            }

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
