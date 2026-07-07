using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class PuttingGameViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;
    private readonly List<HolePuttingData> completedPutts = [];
    private readonly string roundId = Guid.NewGuid().ToString("N");
    private IReadOnlyList<double> customDistancesMeters = [];
    private string mode = PuttingGame.LadderMode;
    private PuttingGameDefinition currentGameDefinition = PuttingGame.GetDefinition(PuttingGame.LadderMode);
    private int currentIndex;
    private int puttsUsed = 2;
    private bool isSetup = true;
    private bool isComplete;
    private string holeCountText = PuttingGame.DefaultHoleCount.ToString();
    private string minimumDistanceMetersText = PuttingGame.DefaultMinimumDistanceMeters.ToString("0");
    private string maximumDistanceMetersText = PuttingGame.DefaultMaximumDistanceMeters.ToString("0");
    private string setupErrorText = string.Empty;
    private string gameTargetText = PuttingGame.TargetPutts.ToString();
    private PuttingDistanceOrder benchmarkDistanceOrder = PuttingDistanceOrder.Random;
    private PuttingTrainingDistanceDistribution trainingDistanceDistribution = PuttingTrainingDistanceDistribution.BellCurve;
    private bool showRemainingDistanceDetails;
    private string selectedBenchmarkDistanceOrder = "Tilfældig";
    private string selectedTrainingDistanceDistribution = "Bell-curve";
    private bool isBusy;
    private string errorMessage = string.Empty;

    public PuttingGameViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public string GameTitle => currentGameDefinition.DisplayName;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanSubmit));
                OnPropertyChanged(nameof(PrimaryActionText));
            }
        }
    }

    public bool CanSubmit => IsActive && !IsBusy;

    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsSetup
    {
        get => isSetup;
        private set
        {
            if (SetProperty(ref isSetup, value))
            {
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
    }

    public bool IsComplete
    {
        get => isComplete;
        private set
        {
            if (SetProperty(ref isComplete, value))
            {
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
    }

    public bool IsActive => !IsSetup && !IsComplete;

    public string HoleCountText
    {
        get => holeCountText;
        set => SetProperty(ref holeCountText, value);
    }

    public string MinimumDistanceMetersText
    {
        get => minimumDistanceMetersText;
        set => SetProperty(ref minimumDistanceMetersText, value);
    }

    public string MaximumDistanceMetersText
    {
        get => maximumDistanceMetersText;
        set => SetProperty(ref maximumDistanceMetersText, value);
    }

    public string SetupErrorText
    {
        get => setupErrorText;
        private set
        {
            if (SetProperty(ref setupErrorText, value))
            {
                OnPropertyChanged(nameof(HasSetupError));
            }
        }
    }

    public bool HasSetupError => !string.IsNullOrWhiteSpace(SetupErrorText);

    public IReadOnlyList<string> TrainingDistanceDistributionOptions { get; } = ["Bell-curve", "Kort", "Lang", "Tilfældig"];

    public string SelectedTrainingDistanceDistribution
    {
        get => selectedTrainingDistanceDistribution;
        set
        {
            if (SetProperty(ref selectedTrainingDistanceDistribution, value))
            {
                trainingDistanceDistribution = value switch
                {
                    "Kort" => PuttingTrainingDistanceDistribution.Short,
                    "Lang" => PuttingTrainingDistanceDistribution.Long,
                    "Tilfældig" => PuttingTrainingDistanceDistribution.Random,
                    _ => PuttingTrainingDistanceDistribution.BellCurve
                };
            }
        }
    }
    public IReadOnlyList<string> BenchmarkDistanceOrderOptions { get; } = ["Tilfældig", "Stigende", "Faldende"];

    public string SelectedBenchmarkDistanceOrder
    {
        get => selectedBenchmarkDistanceOrder;
        set
        {
            if (SetProperty(ref selectedBenchmarkDistanceOrder, value))
            {
                benchmarkDistanceOrder = value switch
                {
                    "Stigende" => PuttingDistanceOrder.Ascending,
                    "Faldende" => PuttingDistanceOrder.Descending,
                    _ => PuttingDistanceOrder.Random
                };
            }
        }
    }

    public string ShortBenchmarkText => $"{FormatBenchmark(PuttingGame.ShortBenchmark)} - {PuttingGame.ShortBenchmarkDistancesMeters.Count} putts";

    public string NormalBenchmarkText => $"{FormatBenchmark(PuttingGame.NormalBenchmark)} - {PuttingGame.NormalBenchmarkDistancesMeters.Count} putts";

    public string ThoroughBenchmarkText => $"{FormatBenchmark(PuttingGame.ThoroughBenchmark)} - {PuttingGame.ThoroughBenchmarkDistancesMeters.Count} putts";

    public string ShortLadderBenchmarkText => FormatLadderBenchmark(PuttingGame.GetBenchmarkDefinition(PuttingGame.ShortLadderBenchmark));

    public string NormalLadderBenchmarkText => FormatLadderBenchmark(PuttingGame.GetBenchmarkDefinition(PuttingGame.NormalLadderBenchmark));

    public string ThoroughLadderBenchmarkText => FormatLadderBenchmark(PuttingGame.GetBenchmarkDefinition(PuttingGame.ThoroughLadderBenchmark));

    public int PuttsUsed
    {
        get => puttsUsed;
        set
        {
            if (SetProperty(ref puttsUsed, Math.Clamp(value, 1, 5)))
            {
                OnPropertyChanged(nameof(PuttsUsedText));
                OnPropertyChanged(nameof(CurrentResultText));
                OnPropertyChanged(nameof(CurrentResultValueText));
            }
        }
    }

    public string ProgressText => IsComplete
        ? $"{GameTitle} færdig"
        : $"Putt {currentIndex + 1} af {Distances.Count}";

    public string CurrentDistanceText => IsComplete
        ? "-"
        : UiFormat.Meters(CurrentDistanceMeters);

    public string PuttsUsedText => PuttsUsed.ToString();

    public string CurrentResultText => IsComplete
        ? string.Empty
        : $"Dette resultat: {UiFormat.Sg(CurrentExpectedPutts - PuttsUsed)}";

    public string CurrentResultValueText => IsComplete
        ? "-"
        : UiFormat.Sg(CurrentExpectedPutts - PuttsUsed);

    public string TotalPuttsText => completedPutts.Sum(putt => putt.Putts).ToString();

    public string RunningSgText => UiFormat.Sg(completedPutts.Sum(putt => putt.StrokesGainedPutting));

    public double ProgressFraction => Distances.Count == 0
        ? 0
        : (double)Math.Min(currentIndex, Distances.Count) / Distances.Count;

    public string ProgressCountText => Distances.Count == 0
        ? "0/0"
        : $"{Math.Min(currentIndex + 1, Distances.Count)}/{Distances.Count}";

    public string RemainingCountText
    {
        get
        {
            var remainingCount = Math.Max(Distances.Count - currentIndex, 0);
            return remainingCount == 1
                ? "1 tilbage"
                : $"{remainingCount} tilbage";
        }
    }

    public string RemainingDistancesPreviewText
    {
        get
        {
            var remaining = Distances.Skip(currentIndex + 1).Take(4).Select(FormatDistance).ToList();
            return remaining.Count == 0
                ? "Sidste putt"
                : $"N\u00e6ste: {string.Join(", ", remaining)}";
        }
    }

    public string RemainingDistancesText
    {
        get
        {
            var remaining = Distances.Skip(currentIndex).ToList();
            return remaining.Count == 0
                ? "Ingen afstande tilbage"
                : $"{remaining.Count} tilbage: {string.Join(", ", remaining.Select(FormatDistance))}";
        }
    }

    public bool HasRemainingDistanceDetails => Distances.Count - currentIndex > 1;

    public bool ShowRemainingDistanceDetails
    {
        get => showRemainingDistanceDetails;
        set
        {
            if (SetProperty(ref showRemainingDistanceDetails, value))
            {
                OnPropertyChanged(nameof(RemainingDistanceDetailsButtonText));
            }
        }
    }

    public string RemainingDistanceDetailsButtonText => ShowRemainingDistanceDetails
        ? "Skjul fuld liste"
        : "Vis fuld liste";

    public string PrimaryActionText => IsBusy
        ? "Gemmer..."
        : Distances.Count - currentIndex <= 1
            ? "Gem og afslut"
            : "Registrer putt";

    public string TargetPuttsText => gameTargetText;

    public string FinalSgText => RunningSgText;

    public string BestResultText => FormatResult(completedPutts.MaxBy(putt => putt.StrokesGainedPutting));

    public string WorstResultText => FormatResult(completedPutts.MinBy(putt => putt.StrokesGainedPutting));

    public void Start(string gameMode)
    {
        mode = PuttingGame.NormalizeMode(gameMode);
        currentGameDefinition = PuttingGame.GetDefinition(mode);
        completedPutts.Clear();
        currentIndex = 0;
        PuttsUsed = 2;
        ShowRemainingDistanceDetails = false;
        IsComplete = false;
        IsSetup = mode != PuttingGame.TourRoundMode;
        gameTargetText = PuttingGame.TargetPutts.ToString();
        if (!IsSetup)
        {
            customDistancesMeters = [];
        }

        RefreshAll();
    }

    public bool StartConfiguredGame()
    {
        if (!int.TryParse(HoleCountText, out var holeCount) || holeCount < 1 || holeCount > 99)
        {
            SetupErrorText = "Indtast et antal putts fra 1 til 99.";
            return false;
        }

        if (!TryParseDistance(MinimumDistanceMetersText, out var minimumDistanceMeters) || minimumDistanceMeters <= 0)
        {
            SetupErrorText = "Indtast en minimumsafstand større end 0.";
            return false;
        }

        if (!TryParseDistance(MaximumDistanceMetersText, out var maximumDistanceMeters) ||
            maximumDistanceMeters < minimumDistanceMeters)
        {
            SetupErrorText = "Indtast en maksimumsafstand der mindst er minimumsafstanden.";
            return false;
        }

        customDistancesMeters = PuttingGame.BuildTrainingDistancesMeters(
            holeCount,
            minimumDistanceMeters,
            maximumDistanceMeters,
            trainingDistanceDistribution);
        currentGameDefinition = PuttingGame.CreateCustomDefinition(customDistancesMeters);
        gameTargetText = customDistancesMeters.Sum(PuttingGame.GetExpectedPutts).ToString("0.0");
        BeginCustomDistanceGame();
        return true;
    }

    public void StartShortBenchmark() => StartBenchmark(PuttingGame.ShortBenchmark);

    public void StartNormalBenchmark() => StartBenchmark(PuttingGame.NormalBenchmark);

    public void StartThoroughBenchmark() => StartBenchmark(PuttingGame.ThoroughBenchmark);

    public void StartShortLadderBenchmark() => StartLadderBenchmark(PuttingGame.ShortLadderBenchmark);

    public void StartNormalLadderBenchmark() => StartLadderBenchmark(PuttingGame.NormalLadderBenchmark);

    public void StartThoroughLadderBenchmark() => StartLadderBenchmark(PuttingGame.ThoroughLadderBenchmark);

    public void IncreasePutts() => PuttsUsed++;

    public void DecreasePutts() => PuttsUsed--;

    public async Task<string?> SubmitAsync()
    {
        if (IsBusy)
        {
            return null;
        }

        if (IsComplete)
        {
            return roundId;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            completedPutts.Add(UseCustomDistances
                ? PuttingGame.BuildPutt(currentIndex + 1, CurrentDistanceMeters, PuttsUsed)
                : PuttingGame.BuildPutt(currentIndex + 1, CurrentDistanceFeet, PuttsUsed, mode));
            currentIndex++;

            if (currentIndex >= Distances.Count)
            {
                var saved = await SaveAsync();
                if (!saved)
                {
                    currentIndex--;
                    completedPutts.RemoveAt(completedPutts.Count - 1);
                    RefreshAll();
                    return null;
                }

                IsComplete = true;
                RefreshAll();
                return roundId;
            }

            PuttsUsed = 2;
            RefreshAll();
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IReadOnlyList<double> Distances => UseCustomDistances
        ? customDistancesMeters
        : PuttingGame.GetPresetDistances(mode).Select(distance => (double)distance).ToList();

    private bool UseCustomDistances => customDistancesMeters.Count > 0;

    private int CurrentDistanceFeet => (int)Distances[currentIndex];

    private double CurrentDistanceMeters => UseCustomDistances
        ? Distances[currentIndex]
        : FeetToMeters(CurrentDistanceFeet);

    private double CurrentExpectedPutts => UseCustomDistances
        ? PuttingGame.GetExpectedPutts(CurrentDistanceMeters)
        : PuttingGame.GetExpectedPutts(CurrentDistanceFeet, mode);

    private async Task<bool> SaveAsync()
    {
        var round = new Round(
            roundId,
            DateTime.Now,
            completedPutts.ToList(),
            new RoundTrackingOptions(true, false, false, true, mode),
            completedPutts.Count,
            false,
            PuttingGame.CreateRoundGameInfo(currentGameDefinition));

        try
        {
            await repository.SaveRoundAsync(round);
            return true;
        }
        catch (Exception)
        {
            ErrorMessage = "Resultatet kunne ikke gemmes. Pr\u00f8v igen, eller tjek lagring under Indstillinger.";
            return false;
        }
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(IsSetup));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(GameTitle));
        OnPropertyChanged(nameof(HoleCountText));
        OnPropertyChanged(nameof(MinimumDistanceMetersText));
        OnPropertyChanged(nameof(MaximumDistanceMetersText));
        OnPropertyChanged(nameof(SetupErrorText));
        OnPropertyChanged(nameof(HasSetupError));
        OnPropertyChanged(nameof(TrainingDistanceDistributionOptions));
        OnPropertyChanged(nameof(SelectedTrainingDistanceDistribution));
        OnPropertyChanged(nameof(BenchmarkDistanceOrderOptions));
        OnPropertyChanged(nameof(SelectedBenchmarkDistanceOrder));
        OnPropertyChanged(nameof(ShortBenchmarkText));
        OnPropertyChanged(nameof(NormalBenchmarkText));
        OnPropertyChanged(nameof(ThoroughBenchmarkText));
        OnPropertyChanged(nameof(ShortLadderBenchmarkText));
        OnPropertyChanged(nameof(NormalLadderBenchmarkText));
        OnPropertyChanged(nameof(ThoroughLadderBenchmarkText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(CurrentDistanceText));
        OnPropertyChanged(nameof(PuttsUsedText));
        OnPropertyChanged(nameof(CurrentResultText));
        OnPropertyChanged(nameof(CurrentResultValueText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(RunningSgText));
        OnPropertyChanged(nameof(ProgressFraction));
        OnPropertyChanged(nameof(ProgressCountText));
        OnPropertyChanged(nameof(RemainingCountText));
        OnPropertyChanged(nameof(RemainingDistancesPreviewText));
        OnPropertyChanged(nameof(RemainingDistancesText));
        OnPropertyChanged(nameof(HasRemainingDistanceDetails));
        OnPropertyChanged(nameof(ShowRemainingDistanceDetails));
        OnPropertyChanged(nameof(RemainingDistanceDetailsButtonText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(TargetPuttsText));
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

        return $"Putt {putt.HoleNumber}, {FormatDistance(Distances[putt.HoleNumber - 1])} ({UiFormat.Sg(putt.StrokesGainedPutting)})";
    }

    private string FormatDistance(double distance) => UseCustomDistances
        ? UiFormat.Meters(distance)
        : UiFormat.Meters(FeetToMeters(distance));

    private void StartBenchmark(string benchmark)
    {
        var benchmarkDefinition = PuttingGame.GetBenchmarkDefinition(benchmark);
        customDistancesMeters = PuttingGame.OrderDistances(
            benchmarkDefinition.DistancesMeters,
            benchmarkDistanceOrder);
        currentGameDefinition = benchmarkDefinition with { DistancesMeters = customDistancesMeters };
        gameTargetText = customDistancesMeters.Sum(PuttingGame.GetExpectedPutts).ToString("0.0");
        BeginCustomDistanceGame();
    }

    private void StartLadderBenchmark(string benchmark)
    {
        var benchmarkDefinition = PuttingGame.GetBenchmarkDefinition(benchmark);
        customDistancesMeters = benchmarkDefinition.DistancesMeters;
        currentGameDefinition = benchmarkDefinition;
        gameTargetText = customDistancesMeters.Sum(PuttingGame.GetExpectedPutts).ToString("0.0");
        BeginCustomDistanceGame();
    }

    private static string FormatBenchmark(string benchmark) => benchmark switch
    {
        PuttingGame.ShortBenchmark => "Kort",
        PuttingGame.NormalBenchmark => "Normal",
        PuttingGame.ThoroughBenchmark => "Grundig",
        _ => benchmark
    };

    private static string FormatLadderBenchmark(PuttingGameDefinition definition)
    {
        var start = UiFormat.Meters(definition.StartDistanceMeters ?? definition.MinimumDistanceMeters ?? 0);
        var end = UiFormat.Meters(definition.EndDistanceMeters ?? definition.MaximumDistanceMeters ?? 0);
        return $"{FormatBenchmarkLength(definition.BenchmarkLength)} ladder - {start}-{end} - {definition.AttemptCount} putts";
    }

    private static string FormatBenchmarkLength(BenchmarkLength? benchmarkLength) => benchmarkLength switch
    {
        BenchmarkLength.Short => "Kort",
        BenchmarkLength.Normal => "Normal",
        BenchmarkLength.Thorough => "Grundig",
        _ => "Ladder"
    };

    private static double FeetToMeters(double distanceFeet) => DistanceConversions.FeetToMeters(distanceFeet);

    private static bool TryParseDistance(string text, out double distanceMeters) =>
        DistanceInputParser.TryParse(text, out distanceMeters);

    private void BeginCustomDistanceGame()
    {
        completedPutts.Clear();
        currentIndex = 0;
        PuttsUsed = 2;
        ShowRemainingDistanceDetails = false;
        SetupErrorText = string.Empty;
        IsComplete = false;
        IsSetup = false;
        RefreshAll();
    }
}
