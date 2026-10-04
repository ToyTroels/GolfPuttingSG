using System.Collections.ObjectModel;
using GolfSG.Application.Rounds;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public sealed class RoundInputViewModel : ViewModelBase
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(350);

    private readonly IRoundApplicationService roundApplicationService;
    private readonly IDistanceUnitSettings distanceUnitSettings;
    private readonly IActiveRoundSessionRepository activeRoundSessionRepository;
    private readonly SemaphoreSlim autosaveLock = new(1, 1);
    private string roundId = Guid.NewGuid().ToString("N");
    private DateTime date = DateTime.Now;
    private RoundSummary summary = StrokesGainedCalculator.CalculateRoundSummary(Round.Empty());
    private int holeCount = 18;
    private bool trackPutting = true;
    private bool trackApproach;
    private bool trackAroundGreen;
    private bool hasStarted;
    private bool isBusy;
    private bool isApplyingTracking;
    private bool isActiveSession;
    private bool isRestoringSession;
    private int currentHoleNumber = 1;
    private CancellationTokenSource? autosaveCts;
    private string errorMessage = string.Empty;

    public RoundInputViewModel(
        IRoundRepository repository,
        IDistanceUnitSettings? distanceUnitSettings = null,
        IActiveRoundSessionRepository? activeRoundSessionRepository = null)
        : this(new RoundApplicationService(repository), distanceUnitSettings, activeRoundSessionRepository)
    {
    }

    public RoundInputViewModel(
        IRoundApplicationService roundApplicationService,
        IDistanceUnitSettings? distanceUnitSettings = null,
        IActiveRoundSessionRepository? activeRoundSessionRepository = null)
    {
        this.roundApplicationService = roundApplicationService;
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
        this.activeRoundSessionRepository = activeRoundSessionRepository ?? NullActiveRoundSessionRepository.Instance;
        Holes = [];
        SetHoleCount(18);
    }

    public ObservableCollection<HoleInputViewModel> Holes { get; }

    public string ScreenTitle { get; private set; } = "Ny runde";

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanSave));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
    }

    public bool CanSave => !IsBusy;

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

    public int HoleCount
    {
        get => holeCount;
        private set
        {
            if (SetProperty(ref holeCount, value))
            {
                OnPropertyChanged(nameof(HoleCountText));
                OnPropertyChanged(nameof(RoundProgressText));
                OnPropertyChanged(nameof(IsRoundComplete));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
    }

    public string HoleCountText => $"{HoleCount} huller";

    public string RoundProgressText => $"{CompletedHoleCount} / {HoleCount} huller registreret";

    public string SaveButtonText => IsBusy ? "Gemmer..." : IsRoundComplete ? "Gem runde" : "Afslut tidligt og gem";

    public int CompletedHoleCount => Holes.Count(IsTrackedHoleCompleted);

    public bool IsRoundComplete => roundApplicationService.IsComplete(BuildDraft());

    public bool TrackPutting
    {
        get => trackPutting;
        set
        {
            if (!value && !TrackApproach && !TrackAroundGreen)
            {
                value = true;
            }

            if (SetProperty(ref trackPutting, value))
            {
                ApplyTrackingToHoles();
                RefreshSummary();
                OnTrackingPropertiesChanged();
            }
        }
    }

    public bool TrackApproach
    {
        get => trackApproach;
        set
        {
            if (!value && !TrackPutting && !TrackAroundGreen)
            {
                value = true;
            }

            if (SetProperty(ref trackApproach, value))
            {
                ApplyTrackingToHoles();
                RefreshSummary();
                OnTrackingPropertiesChanged();
            }
        }
    }

    public bool TrackAroundGreen
    {
        get => trackAroundGreen;
        set
        {
            if (!value && !TrackPutting && !TrackApproach)
            {
                value = true;
            }

            if (SetProperty(ref trackAroundGreen, value))
            {
                ApplyTrackingToHoles();
                RefreshSummary();
                OnTrackingPropertiesChanged();
            }
        }
    }

    public string TotalSgText => UiFormat.Sg(
        (TrackPutting ? summary.TotalStrokesGainedPutting : 0) +
        (TrackApproach ? summary.TotalStrokesGainedApproach : 0) +
        (TrackAroundGreen ? summary.TotalStrokesGainedAroundGreen : 0));

    public string TotalPuttingSgText => UiFormat.Sg(summary.TotalStrokesGainedPutting);

    public string TotalApproachSgText => UiFormat.Sg(summary.TotalStrokesGainedApproach);

    public string TotalAroundGreenSgText => UiFormat.Sg(summary.TotalStrokesGainedAroundGreen);

    public string TotalPuttsText => summary.TotalPutts.ToString();

    public string TotalApproachShotsText => summary.TotalApproachShots.ToString();

    public string TotalAroundGreenShotsText => summary.TotalAroundGreenShots.ToString();

    public string CountBreakdownText => $"1-putts: {summary.OnePutts} | 2-putts: {summary.TwoPutts} | 3-putts+: {summary.ThreePuttsOrWorse}";

    public bool IsSetupVisible => !hasStarted;

    public bool IsRoundVisible => hasStarted;

    public int ResumeHoleNumber => Math.Clamp(currentHoleNumber, 1, HoleCount);

    public async Task<bool> StartRoundAsync()
    {
        if (hasStarted)
        {
            return true;
        }

        hasStarted = true;
        isActiveSession = true;
        OnPropertyChanged(nameof(IsSetupVisible));
        OnPropertyChanged(nameof(IsRoundVisible));

        if (await PersistActiveSessionAsync())
        {
            return true;
        }

        isActiveSession = false;
        hasStarted = false;
        OnPropertyChanged(nameof(IsSetupVisible));
        OnPropertyChanged(nameof(IsRoundVisible));
        return false;
    }

    public void IncreaseHoleCount() => SetHoleCount(HoleCount + 1);

    public void DecreaseHoleCount() => SetHoleCount(HoleCount - 1);

    public async Task LoadAsync(string? existingRoundId)
    {
        if (string.IsNullOrWhiteSpace(existingRoundId))
        {
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            isActiveSession = false;
            var round = await roundApplicationService.LoadAsync(existingRoundId);
            if (round is null)
            {
                ErrorMessage = "Runden kunne ikke findes.";
                return;
            }

            roundId = round.Id;
            date = round.Date;
            var options = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
            trackPutting = options.TrackPutting;
            trackApproach = options.TrackApproach;
            trackAroundGreen = options.TrackAroundGreen;
            hasStarted = true;
            ScreenTitle = "Rediger runde";
            OnPropertyChanged(nameof(ScreenTitle));
            OnPropertyChanged(nameof(IsSetupVisible));
            OnPropertyChanged(nameof(IsRoundVisible));
            OnTrackingPropertiesChanged();
            SetHoleCount(roundApplicationService.ClampHoleCount(
                round.ConfiguredHoleCount > 0 ? round.ConfiguredHoleCount : round.Holes.Count));

            foreach (var hole in round.Holes)
            {
                var input = Holes.FirstOrDefault(input => input.HoleNumber == hole.HoleNumber);
                input?.Load(hole);
            }

            RefreshSummary();
        }
        catch (Exception)
        {
            ErrorMessage = "Runden kunne ikke indl\u00e6ses. Pr\u00f8v igen, eller tjek lagring under Indstillinger.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> LoadActiveAsync()
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        isRestoringSession = true;
        try
        {
            var session = await activeRoundSessionRepository.GetAsync();
            if (session is null)
            {
                ErrorMessage = "Den igangværende runde kunne ikke findes.";
                return false;
            }

            var round = roundApplicationService.CreateRound(session.Draft);
            roundId = round.Id;
            date = round.Date;
            currentHoleNumber = Math.Clamp(session.CurrentHoleNumber, 1, session.Draft.ConfiguredHoleCount);
            var options = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
            trackPutting = options.TrackPutting;
            trackApproach = options.TrackApproach;
            trackAroundGreen = options.TrackAroundGreen;
            hasStarted = true;
            isActiveSession = true;
            ScreenTitle = "Igangværende runde";
            OnPropertyChanged(nameof(ScreenTitle));
            OnPropertyChanged(nameof(IsSetupVisible));
            OnPropertyChanged(nameof(IsRoundVisible));
            OnPropertyChanged(nameof(ResumeHoleNumber));
            OnTrackingPropertiesChanged();
            SetHoleCount(roundApplicationService.ClampHoleCount(
                round.ConfiguredHoleCount > 0 ? round.ConfiguredHoleCount : round.Holes.Count));

            foreach (var hole in round.Holes)
            {
                var input = Holes.FirstOrDefault(input => input.HoleNumber == hole.HoleNumber);
                input?.Load(hole);
            }

            RefreshSummary();
            return true;
        }
        catch (Exception)
        {
            isActiveSession = false;
            ErrorMessage = "Den igangværende runde kunne ikke indlæses. Prøv igen, eller tjek lagring under Indstillinger.";
            return false;
        }
        finally
        {
            isRestoringSession = false;
            IsBusy = false;
        }
    }

    public async Task SetCurrentHoleAsync(int holeNumber)
    {
        SetCurrentHole(holeNumber);
        await FlushAutosaveAsync();
    }

    public void SetCurrentHole(int holeNumber)
    {
        currentHoleNumber = Math.Clamp(holeNumber, 1, HoleCount);
        OnPropertyChanged(nameof(ResumeHoleNumber));
        ScheduleAutosave();
    }

    public async Task FlushAutosaveAsync()
    {
        if (!isActiveSession)
        {
            return;
        }

        autosaveCts?.Cancel();
        await PersistActiveSessionAsync();
    }

    public async Task<bool> AbandonActiveRoundAsync()
    {
        if (!isActiveSession)
        {
            return true;
        }

        autosaveCts?.Cancel();
        await autosaveLock.WaitAsync();
        try
        {
            await activeRoundSessionRepository.DeleteAsync();
            isActiveSession = false;
            return true;
        }
        catch (Exception)
        {
            ErrorMessage = "Den igangværende runde kunne ikke slettes. Prøv igen, eller tjek lagring under Indstillinger.";
            return false;
        }
        finally
        {
            autosaveLock.Release();
        }
    }
    public async Task<string> SaveAsync()
    {
        if (IsBusy)
        {
            return string.Empty;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var round = await roundApplicationService.SaveAsync(BuildDraft());
            if (isActiveSession)
            {
                autosaveCts?.Cancel();
                await autosaveLock.WaitAsync();
                try
                {
                    await activeRoundSessionRepository.DeleteAsync();
                    isActiveSession = false;
                }
                catch (Exception)
                {
                    // The completed round is already safe in history. Startup reconciliation
                    // removes a stale active session with the same id.
                }
                finally
                {
                    autosaveLock.Release();
                }
            }

            return round.Id;
        }
        catch (Exception)
        {
            ErrorMessage = "Runden kunne ikke gemmes. Pr\u00f8v igen, eller tjek lagring under Indstillinger.";
            return string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private RoundDraft BuildDraft() =>
        new(
            roundId,
            date,
            Holes.Select(hole => hole.ToHole()).ToList(),
            new RoundTrackingOptions(TrackPutting, TrackApproach, TrackAroundGreen),
            HoleCount);

    private void SetHoleCount(int count)
    {
        var clampedCount = roundApplicationService.ClampHoleCount(count, HighestEnteredHoleNumber());
        while (Holes.Count < clampedCount)
        {
            AddHole(Holes.Count + 1);
        }

        while (Holes.Count > clampedCount)
        {
            Holes.RemoveAt(Holes.Count - 1);
        }

        HoleCount = clampedCount;
        foreach (var hole in Holes)
        {
            hole.SetTotalHoles(clampedCount);
            hole.SetTracking(new RoundTrackingOptions(TrackPutting, TrackApproach, TrackAroundGreen));
        }

        RefreshSummary();
        ScheduleAutosave();
    }

    private void AddHole(int holeNumber)
    {
        var hole = new HoleInputViewModel(holeNumber, distanceUnitSettings);
        hole.SetTracking(new RoundTrackingOptions(TrackPutting, TrackApproach, TrackAroundGreen));
        hole.PropertyChanged += OnHolePropertyChanged;
        Holes.Add(hole);
    }

    private void OnHolePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // DetailText is emitted after Recalculate/Load finishes updating the hole.
        // Other notifications describe the same edit and must not rebuild the
        // entire round summary or restart autosave individually.
        if (!isApplyingTracking &&
            (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(HoleInputViewModel.DetailText)))
        {
            RefreshSummary();
            ScheduleAutosave();
        }
    }

    private void RefreshSummary()
    {
        summary = roundApplicationService.Summarize(BuildDraft());
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(TotalPuttingSgText));
        OnPropertyChanged(nameof(TotalApproachSgText));
        OnPropertyChanged(nameof(TotalAroundGreenSgText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(TotalApproachShotsText));
        OnPropertyChanged(nameof(TotalAroundGreenShotsText));
        OnPropertyChanged(nameof(CountBreakdownText));
        OnPropertyChanged(nameof(CompletedHoleCount));
        OnPropertyChanged(nameof(IsRoundComplete));
        OnPropertyChanged(nameof(RoundProgressText));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private void ApplyTrackingToHoles()
    {
        isApplyingTracking = true;
        try
        {
            var options = new RoundTrackingOptions(TrackPutting, TrackApproach, TrackAroundGreen);
            foreach (var hole in Holes)
            {
                hole.SetTracking(options);
            }
        }
        finally
        {
            isApplyingTracking = false;
        }
    }

    private void OnTrackingPropertiesChanged()
    {
        OnPropertyChanged(nameof(TrackPutting));
        OnPropertyChanged(nameof(TrackApproach));
        OnPropertyChanged(nameof(TrackAroundGreen));
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(TotalPuttingSgText));
        OnPropertyChanged(nameof(TotalApproachSgText));
        OnPropertyChanged(nameof(TotalAroundGreenSgText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(TotalApproachShotsText));
        OnPropertyChanged(nameof(TotalAroundGreenShotsText));
        OnPropertyChanged(nameof(CountBreakdownText));
        OnPropertyChanged(nameof(CompletedHoleCount));
        OnPropertyChanged(nameof(IsRoundComplete));
        OnPropertyChanged(nameof(RoundProgressText));
        OnPropertyChanged(nameof(SaveButtonText));
        ScheduleAutosave();
    }

    private void ScheduleAutosave()
    {
        if (!isActiveSession || isRestoringSession)
        {
            return;
        }

        autosaveCts?.Cancel();
        autosaveCts?.Dispose();
        autosaveCts = new CancellationTokenSource();
        _ = AutosaveAfterDelayAsync(autosaveCts.Token);
    }

    private async Task AutosaveAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutosaveDelay, cancellationToken);
            await PersistActiveSessionAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<bool> PersistActiveSessionAsync()
    {
        if (!isActiveSession || isRestoringSession)
        {
            return true;
        }

        await autosaveLock.WaitAsync();
        try
        {
            if (!isActiveSession || isRestoringSession)
            {
                return true;
            }

            var session = new ActiveRoundSession(
                BuildDraft(),
                Math.Clamp(currentHoleNumber, 1, HoleCount),
                DateTimeOffset.UtcNow);
            // File backup and replacement can perform synchronous disk work.
            // Keep that work off the UI thread, using an immutable draft snapshot.
            await Task.Run(() => activeRoundSessionRepository.SaveAsync(session));
            if (ErrorMessage.StartsWith("Runden kunne ikke gemmes automatisk", StringComparison.Ordinal))
            {
                ErrorMessage = string.Empty;
            }

            return true;
        }
        catch (Exception)
        {
            ErrorMessage = "Runden kunne ikke gemmes automatisk. Prøv igen, eller tjek lagring under Indstillinger.";
            return false;
        }
        finally
        {
            autosaveLock.Release();
        }
    }

    private int HighestEnteredHoleNumber()
    {
        return Holes
            .Where(HasAnyTrackedInput)
            .Select(hole => hole.HoleNumber)
            .DefaultIfEmpty(RoundApplicationService.MinimumHoleCount)
            .Max();
    }

    private bool IsTrackedHoleCompleted(HoleInputViewModel hole)
    {
        var data = hole.ToHole();
        return (TrackPutting && data.IsCompleted) ||
            (TrackApproach && data.IsApproachCompleted) ||
            (TrackAroundGreen && data.IsAroundGreenCompleted);
    }

    private bool HasAnyTrackedInput(HoleInputViewModel hole)
    {
        var data = hole.ToHole();
        return (TrackPutting && data.FirstPuttDistanceMeters > 0) ||
            (TrackApproach && (data.ApproachStartDistanceYards > 0 || data.ApproachPenaltyStrokes > 0 || data.ApproachHoled)) ||
            (TrackAroundGreen && (data.AroundGreenStartDistanceYards > 0 || data.AroundGreenPenaltyStrokes > 0 || data.AroundGreenHoled));
    }
}
