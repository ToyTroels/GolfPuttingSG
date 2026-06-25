using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class PuttingGameViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;
    private readonly List<HolePuttingData> completedPutts = [];
    private readonly string roundId = Guid.NewGuid().ToString("N");
    private string mode = PuttingGame.LadderMode;
    private int currentIndex;
    private int puttsUsed = 2;
    private bool isComplete;

    public PuttingGameViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public string GameTitle => PuttingGame.GetTitle(mode);

    public bool IsComplete
    {
        get => isComplete;
        private set
        {
            if (SetProperty(ref isComplete, value))
            {
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public bool IsActive => !IsComplete;

    public int PuttsUsed
    {
        get => puttsUsed;
        set
        {
            if (SetProperty(ref puttsUsed, Math.Clamp(value, 1, 5)))
            {
                OnPropertyChanged(nameof(PuttsUsedText));
                OnPropertyChanged(nameof(CurrentResultText));
            }
        }
    }

    public string ProgressText => IsComplete
        ? $"{GameTitle} complete"
        : $"Putt {currentIndex + 1} of {Distances.Count}";

    public string CurrentDistanceText => IsComplete
        ? "-"
        : $"{CurrentDistanceFeet} ft";

    public string PuttsUsedText => PuttsUsed.ToString();

    public string CurrentResultText => IsComplete
        ? string.Empty
        : $"This result: {UiFormat.Sg(PuttingGame.GetExpectedPutts(CurrentDistanceFeet, mode) - PuttsUsed)}";

    public string TotalPuttsText => completedPutts.Sum(putt => putt.Putts).ToString();

    public string RunningSgText => UiFormat.Sg(completedPutts.Sum(putt => putt.StrokesGainedPutting));

    public string RemainingDistancesText
    {
        get
        {
            var remaining = Distances.Skip(currentIndex).ToList();
            return remaining.Count == 0
                ? "No distances remaining"
                : $"{remaining.Count} remaining: {string.Join(", ", remaining)} ft";
        }
    }

    public string TargetPuttsText => PuttingGame.TargetPutts.ToString();

    public string FinalSgText => RunningSgText;

    public string BestResultText => FormatResult(completedPutts.MaxBy(putt => putt.StrokesGainedPutting));

    public string WorstResultText => FormatResult(completedPutts.MinBy(putt => putt.StrokesGainedPutting));

    public void Start(string gameMode)
    {
        mode = PuttingGame.NormalizeMode(gameMode);
        RefreshAll();
    }

    public void IncreasePutts() => PuttsUsed++;

    public void DecreasePutts() => PuttsUsed--;

    public async Task<string?> SubmitAsync()
    {
        if (IsComplete)
        {
            return roundId;
        }

        completedPutts.Add(PuttingGame.BuildPutt(currentIndex + 1, CurrentDistanceFeet, PuttsUsed, mode));
        currentIndex++;

        if (currentIndex >= Distances.Count)
        {
            IsComplete = true;
            await SaveAsync();
            RefreshAll();
            return roundId;
        }

        PuttsUsed = 2;
        RefreshAll();
        return null;
    }

    private IReadOnlyList<int> Distances => PuttingGame.GetPresetDistances(mode);

    private int CurrentDistanceFeet => Distances[currentIndex];

    private async Task SaveAsync()
    {
        var round = new Round(
            roundId,
            DateTime.Today,
            completedPutts.ToList(),
            new RoundTrackingOptions(true, false, true, mode));

        await repository.SaveRoundAsync(round);
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(GameTitle));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(CurrentDistanceText));
        OnPropertyChanged(nameof(PuttsUsedText));
        OnPropertyChanged(nameof(CurrentResultText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(RunningSgText));
        OnPropertyChanged(nameof(RemainingDistancesText));
        OnPropertyChanged(nameof(FinalSgText));
        OnPropertyChanged(nameof(BestResultText));
        OnPropertyChanged(nameof(WorstResultText));
    }

    private string FormatResult(HolePuttingData? putt)
    {
        if (putt is null)
        {
            return "-";
        }

        var feet = Distances[putt.HoleNumber - 1];
        return $"Putt {putt.HoleNumber}, {feet} ft ({UiFormat.Sg(putt.StrokesGainedPutting)})";
    }
}
