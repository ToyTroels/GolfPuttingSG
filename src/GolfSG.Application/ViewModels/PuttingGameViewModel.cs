using GolfSG.Application.Putting;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;
using System.Collections.ObjectModel;

namespace GolfSG.Application.ViewModels;

public sealed class PuttingGameViewModel : ViewModelBase
{
    private readonly IPuttingGameSessionService sessionService;
    private readonly IDistanceUnitSettings distanceUnitSettings;
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

    private readonly IActivePuttingGameRepository activeRepository;
    private Task pendingAutosave = Task.CompletedTask;
    private bool autosaveFailed;

    public PuttingGameViewModel(IRoundRepository repository, IDistanceUnitSettings? distanceUnitSettings = null, IActivePuttingGameRepository? activeRepository = null)
        : this(new PuttingGameSessionService(repository, activeRepository), distanceUnitSettings, activeRepository)
    {
    }

    public PuttingGameViewModel(
        IPuttingGameSessionService sessionService,
        IDistanceUnitSettings? distanceUnitSettings = null,
        IActivePuttingGameRepository? activeRepository = null)
    {
        this.sessionService = sessionService;
        this.activeRepository = activeRepository ?? NullActivePuttingGameRepository.Instance;
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
        minimumDistanceMetersText = FormatPuttingInputDistance(PuttingGame.DefaultMinimumDistanceMeters, 0);
        maximumDistanceMetersText = FormatPuttingInputDistance(PuttingGame.DefaultMaximumDistanceMeters, 0);
    }

    public string GameTitle => sessionService.Definition.DisplayName;

    public ObservableCollection<PracticePuttResult> RecordedPutts { get; } = [];
    public bool HasRecordedPutts => !IsSetup && RecordedPutts.Count > 0;
    public bool CanReview => HasRecordedPutts && !IsBusy;
    public bool CanUndo => IsActive && RecordedPutts.Count > 0 && !IsBusy;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanSubmit));
                OnPropertyChanged(nameof(PrimaryActionText));
                OnPropertyChanged(nameof(CanReview));
                OnPropertyChanged(nameof(CanUndo));
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

    public string DistanceUnitText => UiFormat.PuttingDistanceUnitText(PuttingDistanceUnit);

    public string MinimumDistanceLabel => $"Minimumsafstand ({DistanceUnitText})";

    public string MaximumDistanceLabel => $"Maksimumsafstand ({DistanceUnitText})";

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
            if (IsBusy) return;
            if (SetProperty(ref puttsUsed, Math.Clamp(value, 1, 5)))
            {
                OnPropertyChanged(nameof(PuttsUsedText));
                OnPropertyChanged(nameof(CurrentResultText));
                OnPropertyChanged(nameof(CurrentResultValueText));
                if (IsActive && !IsBusy) QueueAutosave();
            }
        }
    }

    public string ProgressText => IsComplete
        ? $"{GameTitle} færdig"
        : $"Putt {sessionService.CurrentIndex + 1} af {Distances.Count}";

    public string CurrentDistanceText => IsComplete
        ? "-"
        : UiFormat.PuttingDistance(CurrentDistanceMeters, PuttingDistanceUnit);

    public string PuttsUsedText => PuttsUsed.ToString();

    public string CurrentResultText => IsComplete
        ? string.Empty
        : $"Dette resultat: {UiFormat.Sg(CurrentExpectedPutts - PuttsUsed)}";

    public string CurrentResultValueText => IsComplete
        ? "-"
        : UiFormat.Sg(CurrentExpectedPutts - PuttsUsed);

    public string TotalPuttsText => sessionService.CompletedPutts.Sum(putt => putt.Putts).ToString();

    public string RunningSgText => UiFormat.Sg(sessionService.CompletedPutts.Sum(putt => putt.StrokesGainedPutting));

    public double ProgressFraction => Distances.Count == 0
        ? 0
        : (double)Math.Min(sessionService.CurrentIndex, Distances.Count) / Distances.Count;

    public string ProgressCountText => Distances.Count == 0
        ? "0/0"
        : $"{Math.Min(sessionService.CurrentIndex + 1, Distances.Count)}/{Distances.Count}";

    public string RemainingCountText
    {
        get
        {
            var remainingCount = Math.Max(Distances.Count - sessionService.CurrentIndex, 0);
            return remainingCount == 1
                ? "1 tilbage"
                : $"{remainingCount} tilbage";
        }
    }

    public string RemainingDistancesPreviewText
    {
        get
        {
            var remaining = Distances.Skip(sessionService.CurrentIndex + 1).Take(4).Select(FormatDistance).ToList();
            return remaining.Count == 0
                ? "Sidste putt"
                : $"N\u00e6ste: {string.Join(", ", remaining)}";
        }
    }

    public string RemainingDistancesText
    {
        get
        {
            var remaining = Distances.Skip(sessionService.CurrentIndex).ToList();
            return remaining.Count == 0
                ? "Ingen afstande tilbage"
                : $"{remaining.Count} tilbage: {string.Join(", ", remaining.Select(FormatDistance))}";
        }
    }

    public bool HasRemainingDistanceDetails => Distances.Count - sessionService.CurrentIndex > 1;

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
        : Distances.Count - sessionService.CurrentIndex <= 1
            ? "Gem og afslut"
            : "Registrer putt";

    public string TargetPuttsText => gameTargetText;

    public string FinalSgText => RunningSgText;

    public string BestResultText => FormatResult(sessionService.CompletedPutts.MaxBy(putt => putt.StrokesGainedPutting));

    public string WorstResultText => FormatResult(sessionService.CompletedPutts.MinBy(putt => putt.StrokesGainedPutting));

    public void Start(string gameMode)
    {
        sessionService.Start(gameMode);
        PuttsUsed = 2;
        ShowRemainingDistanceDetails = false;
        IsComplete = false;
        IsSetup = sessionService.Mode != PuttingGame.TourRoundMode;
        gameTargetText = sessionService.TargetPutts.ToString("0.#");
        RefreshAll();
        if (IsActive) QueueAutosave();
    }

    public bool StartConfiguredGame()
    {
        if (!int.TryParse(HoleCountText, out var holeCount) || holeCount < 1 || holeCount > 99)
        {
            SetupErrorText = "Indtast et antal putts fra 1 til 99.";
            return false;
        }

        if (!TryParsePuttingDistance(MinimumDistanceMetersText, out var minimumDistanceMeters) || minimumDistanceMeters <= 0)
        {
            SetupErrorText = "Indtast en minimumsafstand større end 0.";
            return false;
        }

        if (!TryParsePuttingDistance(MaximumDistanceMetersText, out var maximumDistanceMeters) ||
            maximumDistanceMeters < minimumDistanceMeters)
        {
            SetupErrorText = "Indtast en maksimumsafstand der mindst er minimumsafstanden.";
            return false;
        }

        sessionService.StartTraining(
            holeCount,
            minimumDistanceMeters,
            maximumDistanceMeters,
            trainingDistanceDistribution);
        gameTargetText = sessionService.TargetPutts.ToString("0.0");
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
            return sessionService.RoundId;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await PersistPendingAutosaveAsync();
            var result = await sessionService.SubmitAsync(PuttsUsed);
            if (result.IsComplete)
            {
                IsComplete = true;
                RefreshAll();
                return result.RoundId;
            }

            puttsUsed = 2;
            RefreshAll();
            return null;
        }
        catch (Exception)
        {
            ErrorMessage = "Resultatet kunne ikke gemmes. Pr�v igen, eller tjek lagring under Indstillinger.";
            RefreshAll();
            return null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IReadOnlyList<double> Distances => sessionService.Distances;

    private bool UseCustomDistances => sessionService.DistancesAreMeters;

    private double CurrentDistanceMeters => sessionService.CurrentDistanceMeters;

    private double CurrentExpectedPutts => sessionService.CurrentExpectedPutts;


    private void RefreshAll()
    {
        RecordedPutts.Clear();
        foreach (var putt in sessionService.CompletedPutts)
            RecordedPutts.Add(new PracticePuttResult(
                putt.HoleNumber - 1,
                $"Putt {putt.HoleNumber} · {FormatDistance(Distances[putt.HoleNumber - 1])}",
                putt.Putts,
                $"{putt.Putts} putts · {UiFormat.Sg(putt.StrokesGainedPutting)} SG"));
        OnPropertyChanged(nameof(HasRecordedPutts));
        OnPropertyChanged(nameof(CanReview));
        OnPropertyChanged(nameof(CanUndo));
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
        OnPropertyChanged(nameof(DistanceUnitText));
        OnPropertyChanged(nameof(MinimumDistanceLabel));
        OnPropertyChanged(nameof(MaximumDistanceLabel));
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

    private string FormatDistance(double distance) => UiFormat.PuttingDistance(
        UseCustomDistances ? distance : FeetToMeters(distance),
        PuttingDistanceUnit);

    private void StartBenchmark(string benchmark)
    {
        sessionService.StartBenchmark(benchmark, benchmarkDistanceOrder);
        gameTargetText = sessionService.TargetPutts.ToString("0.0");
        BeginCustomDistanceGame();
    }

    private void StartLadderBenchmark(string benchmark)
    {
        sessionService.StartLadderBenchmark(benchmark);
        gameTargetText = sessionService.TargetPutts.ToString("0.0");
        BeginCustomDistanceGame();
    }

    private static string FormatBenchmark(string benchmark) => benchmark switch
    {
        PuttingGame.ShortBenchmark => "Kort",
        PuttingGame.NormalBenchmark => "Normal",
        PuttingGame.ThoroughBenchmark => "Grundig",
        _ => benchmark
    };

    private string FormatLadderBenchmark(PuttingGameDefinition definition)
    {
        var start = UiFormat.PuttingDistance(definition.StartDistanceMeters ?? definition.MinimumDistanceMeters ?? 0, PuttingDistanceUnit);
        var end = UiFormat.PuttingDistance(definition.EndDistanceMeters ?? definition.MaximumDistanceMeters ?? 0, PuttingDistanceUnit);
        return $"{FormatBenchmarkLength(definition.BenchmarkLength)} ladder - {start}-{end} - {definition.AttemptCount} putts";
    }

    private static string FormatBenchmarkLength(BenchmarkLength? benchmarkLength) => benchmarkLength switch
    {
        BenchmarkLength.Short => "Kort",
        BenchmarkLength.Normal => "Normal",
        BenchmarkLength.Thorough => "Grundig",
        _ => "Ladder"
    };

    private PuttingDistanceUnitPreference PuttingDistanceUnit => distanceUnitSettings.PuttingDistanceUnit;

    private static double FeetToMeters(double distanceFeet) => DistanceConversions.FeetToMeters(distanceFeet);

    private bool TryParsePuttingDistance(string text, out double distanceMeters)
    {
        if (!DistanceInputParser.TryParse(text, out var preferredDistance))
        {
            distanceMeters = 0;
            return false;
        }

        distanceMeters = UiFormat.FromPreferredPuttingDistance(preferredDistance, PuttingDistanceUnit);
        return true;
    }

    private string FormatPuttingInputDistance(double distanceMeters, int decimals) =>
        DistanceInputParser.FormatSlider(UiFormat.ToPreferredPuttingDistance(distanceMeters, PuttingDistanceUnit), decimals);

    private void BeginCustomDistanceGame()
    {
        PuttsUsed = 2;
        ShowRemainingDistanceDetails = false;
        SetupErrorText = string.Empty;
        IsComplete = false;
        IsSetup = false;
        RefreshAll();
        QueueAutosave();
    }

    public void Resume(ActivePuttingGameSession session)
    {
        sessionService.Restore(session);
        IsSetup = true;
        PuttsUsed = session.PuttsUsed;
        IsComplete = false;
        IsSetup = false;
        gameTargetText = sessionService.TargetPutts.ToString("0.#");
        RefreshAll();
    }

    private void QueueAutosave()
    {
        var snapshot = sessionService.Capture(PuttsUsed);
        pendingAutosave = SaveAfterAsync(pendingAutosave, snapshot);
    }

    private async Task SaveAfterAsync(Task previous, ActivePuttingGameSession snapshot)
    {
        await previous;
        try
        {
            await activeRepository.SaveAsync(snapshot);
            autosaveFailed = false;
        }
        catch
        {
            autosaveFailed = true;
            ErrorMessage = "Spillet kunne ikke gemmes automatisk. Prøv igen, før du lukker spillet.";
        }
    }

    public async Task FlushAutosaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await PersistPendingAutosaveAsync(); }
        finally { IsBusy = false; }
    }

    private async Task PersistPendingAutosaveAsync()
    {
        if (IsActive) QueueAutosave();
        await pendingAutosave;
        if (autosaveFailed) throw new IOException(ErrorMessage);
        ErrorMessage = string.Empty;
    }

    public async Task DiscardAsync()
    {
        if (IsBusy) throw new InvalidOperationException("Wait for the current game action to finish.");
        IsBusy = true;
        try
        {
            await pendingAutosave;
            await activeRepository.DeleteAsync();
            IsSetup = true;
        }
        finally { IsBusy = false; RefreshAll(); }
    }

    public async Task<bool> UndoLastAsync()
    {
        if (!CanUndo) return false;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await PersistPendingAutosaveAsync();
            var restoredPutts = await sessionService.UndoLastAsync();
            if (restoredPutts is null) return false;
            puttsUsed = restoredPutts.Value;
            return true;
        }
        catch
        {
            ErrorMessage = "Resultatet kunne ikke fortrydes. Prøv igen; den registrerede score er bevaret.";
            return false;
        }
        finally { IsBusy = false; RefreshAll(); }
    }

    public async Task<bool> EditPuttAsync(int index, int correctedPutts)
    {
        if (!CanReview) return false;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            if (IsActive) await PersistPendingAutosaveAsync();
            return await sessionService.EditAsync(index, correctedPutts, PuttsUsed);
        }
        catch
        {
            ErrorMessage = "Rettelsen kunne ikke gemmes. Prøv igen; det tidligere resultat er bevaret.";
            return false;
        }
        finally { IsBusy = false; RefreshAll(); }
    }
}

public sealed record PracticePuttResult(int Index, string Title, int Putts, string Detail);
