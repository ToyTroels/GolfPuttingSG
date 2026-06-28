using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.ViewModels;

public sealed class HoleInputViewModel : ViewModelBase
{
    private const double MaxFirstPuttDistanceMeters = 30;
    private const double MaxApproachDistanceMeters = 250;
    private const double MaxAroundGreenDistanceMeters = 50;
    private const double MaxFinishDistanceMeters = 250;
    private const double FirstPuttDistanceStepMeters = 0.1;
    private const double ApproachDistanceStepMeters = 1;
    private const double AroundGreenDistanceStepMeters = 1;
    private const double GreenFinishDistanceStepMeters = 0.1;
    private const double MetersPerYard = 0.9144;
    private const double MetersPerFoot = 0.3048;

    private string distanceText = string.Empty;
    private string approachDistanceText = string.Empty;
    private string approachStartDistanceText = string.Empty;
    private string approachEndDistanceText = string.Empty;
    private string approachEndDistanceToGreenEdgeText = string.Empty;
    private string approachStartLieText = "Fairway";
    private string approachEndLieText = "Green";
    private string aroundGreenStartDistanceText = string.Empty;
    private string aroundGreenEndDistanceText = string.Empty;
    private string aroundGreenStartLieText = "Rough";
    private string aroundGreenEndLieText = "Green";
    private int putts = 2;
    private int approachShots;
    private int approachPar = 4;
    private int approachPenaltyStrokes;
    private int aroundGreenPenaltyStrokes;
    private double expectedPutts;
    private double expectedApproachShots;
    private double expectedApproachFinishStrokes;
    private double expectedAroundGreenStartStrokes;
    private double expectedAroundGreenFinishStrokes;
    private double strokesGainedPutting;
    private double strokesGainedApproach;
    private double strokesGainedAroundGreen;
    private int totalHoles = 18;
    private bool trackPutting = true;
    private bool trackApproach;
    private bool approachIsTeeShot;
    private bool approachHoled;
    private bool trackAroundGreen;
    private bool aroundGreenHoled;
    private readonly List<GolfShot> completedAroundGreenShots = [];
    private double? carriedPuttingDistanceFromApproachMeters;
    private double? carriedPuttingDistanceFromAroundGreenMeters;
    private double? carriedAroundGreenStartDistanceFromApproachMeters;
    private string? carriedAroundGreenStartLieFromApproach;
    private bool isApplyingApproachCarryForward;
    private bool hasManualPuttingDistanceOverride;

    public HoleInputViewModel(int holeNumber)
    {
        HoleNumber = holeNumber;
    }

    public int HoleNumber { get; }

    public IReadOnlyList<string> AroundGreenStartLieOptions { get; } =
        ["Kortklippet", "Rough", "Sand", "Problemlie"];

    public IReadOnlyList<string> AroundGreenEndLieOptions { get; } =
        ["Green", "Kortklippet", "Rough", "Sand", "Problemlie", "I hul"];

    public IReadOnlyList<string> ApproachStartLieOptions { get; } =
        ["Fairway", "Rough", "Sand", "Problemlie"];

    public IReadOnlyList<string> ApproachEndLieOptions { get; } =
        ["Green", "Fairway", "Rough", "Sand", "Problemlie", "Kortklippet", "I hul"];

    public int TotalHoles
    {
        get => totalHoles;
        private set
        {
            if (SetProperty(ref totalHoles, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string Title => $"Hul {HoleNumber}";

    public string Subtitle => $"Hul {HoleNumber} / {TotalHoles}";

    public bool TrackPutting
    {
        get => trackPutting;
        private set
        {
            if (SetProperty(ref trackPutting, value))
            {
                OnTrackingChanged();
            }
        }
    }

    public bool TrackApproach
    {
        get => trackApproach;
        private set
        {
            if (SetProperty(ref trackApproach, value))
            {
                OnTrackingChanged();
            }
        }
    }

    public bool TrackAroundGreen
    {
        get => trackAroundGreen;
        private set
        {
            if (SetProperty(ref trackAroundGreen, value))
            {
                OnTrackingChanged();
            }
        }
    }

    public string DistanceText
    {
        get => distanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref distanceText, normalized))
            {
                if (!isApplyingApproachCarryForward)
                {
                    hasManualPuttingDistanceOverride = !string.IsNullOrWhiteSpace(normalized);
                    ClearCarriedPuttingDistanceIfManuallyChanged();
                    ClearCarriedAroundGreenPuttingDistanceIfManuallyChanged();
                }

                OnPropertyChanged(nameof(FirstPuttDistanceMeters));
                OnPropertyChanged(nameof(FirstPuttDistanceDisplayText));
                Recalculate();
            }
        }
    }

    public double FirstPuttDistanceMeters
    {
        get => ParseDistance(DistanceText);
        set => DistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxFirstPuttDistanceMeters), 1);
    }

    public string FirstPuttDistanceDisplayText => FormatPuttDistance();

    public int Putts
    {
        get => putts;
        set
        {
            if (SetProperty(ref putts, Math.Clamp(value, 0, 5)))
            {
                Recalculate();
            }
        }
    }

    public string ApproachDistanceText
    {
        get => approachDistanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref approachDistanceText, normalized))
            {
                OnPropertyChanged(nameof(ApproachDistanceMeters));
                OnPropertyChanged(nameof(ApproachDistanceDisplayText));
                Recalculate();
            }
        }
    }

    public double ApproachDistanceMeters
    {
        get => ParseDistance(ApproachDistanceText);
        set => ApproachDistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxApproachDistanceMeters), 0);
    }

    public string ApproachDistanceDisplayText => FormatApproachDistance();

    public int ApproachShots
    {
        get => approachShots;
        set
        {
            if (SetProperty(ref approachShots, Math.Clamp(value, 0, 10)))
            {
                Recalculate();
            }
        }
    }

    public int ApproachPar
    {
        get => approachPar;
        set
        {
            if (SetProperty(ref approachPar, Math.Clamp(value, 3, 5)))
            {
                Recalculate();
            }
        }
    }

    public bool ApproachIsTeeShot
    {
        get => approachIsTeeShot;
        set
        {
            if (SetProperty(ref approachIsTeeShot, value))
            {
                if (value && ApproachPar == 3 && ApproachStartLieText != "Tee")
                {
                    ApproachStartLieText = "Tee";
                    OnPropertyChanged(nameof(ApproachStartLieText));
                }

                Recalculate();
            }
        }
    }

    public string ApproachStartDistanceText
    {
        get => approachStartDistanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref approachStartDistanceText, normalized))
            {
                OnPropertyChanged(nameof(ApproachStartDistanceYards));
                OnPropertyChanged(nameof(ApproachStartDistanceDisplayText));
                Recalculate();
            }
        }
    }

    public double ApproachStartDistanceYards
    {
        get => ParseDistance(ApproachStartDistanceText);
        set => ApproachStartDistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxApproachDistanceMeters), 0);
    }

    public string ApproachStartDistanceDisplayText => FormatApproachStartDistance();

    public string ApproachEndDistanceText
    {
        get => approachEndDistanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref approachEndDistanceText, normalized))
            {
                OnPropertyChanged(nameof(ApproachEndDistance));
                OnPropertyChanged(nameof(ApproachEndDistanceDisplayText));
                ApplyApproachCarryForward();
                Recalculate();
            }
        }
    }

    public double ApproachEndDistance
    {
        get => ParseDistance(ApproachEndDistanceText);
        set => ApproachEndDistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxFinishDistanceMeters), 0);
    }

    public string ApproachEndDistanceDisplayText => FormatApproachEndDistance();

    public string ApproachEndDistanceToGreenEdgeText
    {
        get => approachEndDistanceToGreenEdgeText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref approachEndDistanceToGreenEdgeText, normalized))
            {
                OnPropertyChanged(nameof(ApproachEndDistanceToGreenEdgeYards));
                OnPropertyChanged(nameof(ApproachEndDistanceToGreenEdgeDisplayText));
                Recalculate();
            }
        }
    }

    public double ApproachEndDistanceToGreenEdgeYards
    {
        get => ParseDistance(ApproachEndDistanceToGreenEdgeText);
        set => ApproachEndDistanceToGreenEdgeText = FormatSliderDistance(Math.Clamp(value, 0, MaxApproachDistanceMeters), 0);
    }

    public string ApproachEndDistanceToGreenEdgeDisplayText => FormatApproachEndDistanceToGreenEdge();

    public string ApproachStartLieText
    {
        get => approachStartLieText;
        set
        {
            if (SetProperty(ref approachStartLieText, value))
            {
                Recalculate();
            }
        }
    }

    public string ApproachEndLieText
    {
        get => approachEndLieText;
        set
        {
            if (SetProperty(ref approachEndLieText, value))
            {
                ApproachHoled = ParseLie(value) == ShotLie.Holed;
                OnPropertyChanged(nameof(IsApproachFinishDistanceVisible));
                OnPropertyChanged(nameof(ApproachEndDistanceUnitText));
                OnFlowVisibilityChanged();
                ApplyApproachCarryForward();
                Recalculate();
            }
        }
    }

    public bool ApproachHoled
    {
        get => approachHoled;
        set
        {
            if (SetProperty(ref approachHoled, value))
            {
                if (value && ParseLie(ApproachEndLieText) != ShotLie.Holed)
                {
                    approachEndLieText = "I hul";
                    OnPropertyChanged(nameof(ApproachEndLieText));
                }
                else if (!value && ParseLie(ApproachEndLieText) == ShotLie.Holed)
                {
                    approachEndLieText = "Green";
                    OnPropertyChanged(nameof(ApproachEndLieText));
                }

                OnPropertyChanged(nameof(IsApproachFinishDistanceVisible));
                OnFlowVisibilityChanged();
                ApplyApproachCarryForward();
                Recalculate();
            }
        }
    }

    public int ApproachPenaltyStrokes
    {
        get => approachPenaltyStrokes;
        set
        {
            if (SetProperty(ref approachPenaltyStrokes, Math.Clamp(value, 0, 5)))
            {
                Recalculate();
            }
        }
    }

    public bool IsApproachFinishDistanceVisible => !ApproachHoled;

    public string ApproachEndDistanceUnitText => "m";

    public bool IsApproachInputVisible => TrackApproach;

    public bool IsAroundGreenInputVisible => TrackAroundGreen && !ApproachFinishedOnGreenOrHoled;

    public bool IsPuttingInputVisible => TrackPutting && !ApproachFinishedHoled && !AroundGreenFinishedHoled;

    public string AroundGreenStartDistanceText
    {
        get => aroundGreenStartDistanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref aroundGreenStartDistanceText, normalized))
            {
                if (!isApplyingApproachCarryForward)
                {
                    ClearCarriedAroundGreenDistanceIfManuallyChanged();
                }

                OnPropertyChanged(nameof(AroundGreenStartDistanceYards));
                OnPropertyChanged(nameof(AroundGreenStartDistanceDisplayText));
                OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
                Recalculate();
            }
        }
    }

    public double AroundGreenStartDistanceYards
    {
        get => ParseDistance(AroundGreenStartDistanceText);
        set => AroundGreenStartDistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxAroundGreenDistanceMeters), 0);
    }

    public string AroundGreenStartDistanceDisplayText => FormatAroundGreenStartDistance();

    public string AroundGreenEndDistanceText
    {
        get => aroundGreenEndDistanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref aroundGreenEndDistanceText, normalized))
            {
                OnPropertyChanged(nameof(AroundGreenEndDistance));
                OnPropertyChanged(nameof(AroundGreenEndDistanceDisplayText));
                OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
                ApplyAroundGreenCarryForward();
                Recalculate();
            }
        }
    }

    public double AroundGreenEndDistance
    {
        get => ParseDistance(AroundGreenEndDistanceText);
        set => AroundGreenEndDistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxFinishDistanceMeters), 1);
    }

    public string AroundGreenEndDistanceDisplayText => FormatAroundGreenEndDistance();

    public string AroundGreenStartLieText
    {
        get => aroundGreenStartLieText;
        set
        {
            if (SetProperty(ref aroundGreenStartLieText, value))
            {
                if (!isApplyingApproachCarryForward)
                {
                    ClearCarriedAroundGreenLieIfManuallyChanged();
                }

                Recalculate();
            }
        }
    }

    public string AroundGreenEndLieText
    {
        get => aroundGreenEndLieText;
        set
        {
            if (SetProperty(ref aroundGreenEndLieText, value))
            {
                AroundGreenHoled = ParseLie(value) == ShotLie.Holed;
                OnPropertyChanged(nameof(IsAroundGreenFinishDistanceVisible));
                OnPropertyChanged(nameof(AroundGreenEndDistanceUnitText));
                OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
                OnFlowVisibilityChanged();
                ApplyAroundGreenCarryForward();
                Recalculate();
            }
        }
    }

    public bool AroundGreenHoled
    {
        get => aroundGreenHoled;
        set
        {
            if (SetProperty(ref aroundGreenHoled, value))
            {
                if (value && ParseLie(AroundGreenEndLieText) != ShotLie.Holed)
                {
                    aroundGreenEndLieText = "I hul";
                    OnPropertyChanged(nameof(AroundGreenEndLieText));
                }
                else if (!value && ParseLie(AroundGreenEndLieText) == ShotLie.Holed)
                {
                    aroundGreenEndLieText = "Green";
                    OnPropertyChanged(nameof(AroundGreenEndLieText));
                }

                OnPropertyChanged(nameof(IsAroundGreenFinishDistanceVisible));
                OnPropertyChanged(nameof(AroundGreenEndDistanceUnitText));
                OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
                OnFlowVisibilityChanged();
                ApplyAroundGreenCarryForward();
                Recalculate();
            }
        }
    }

    public int AroundGreenPenaltyStrokes
    {
        get => aroundGreenPenaltyStrokes;
        set
        {
            if (SetProperty(ref aroundGreenPenaltyStrokes, Math.Clamp(value, 0, 5)))
            {
                Recalculate();
            }
        }
    }

    public bool IsAroundGreenFinishDistanceVisible => !AroundGreenHoled;

    public string AroundGreenEndDistanceUnitText => "m";

    public string AroundGreenShotTitle => completedAroundGreenShots.Count == 0
        ? "Omkring green"
        : $"Omkring green slag {completedAroundGreenShots.Count + 1}";

    public IReadOnlyList<AroundGreenShotSummaryViewModel> CompletedAroundGreenShotSummaries =>
        completedAroundGreenShots
            .Select((shot, index) => AroundGreenShotSummaryViewModel.FromShot(shot, index + 1))
            .ToList();

    public bool HasCompletedAroundGreenShots => completedAroundGreenShots.Count > 0;

    public bool CanAddAnotherAroundGreenShot => IsAroundGreenInputVisible &&
        ParseDistance(AroundGreenStartDistanceText) > 0 &&
        ParseDistance(AroundGreenEndDistanceText) > 0 &&
        ParseLie(AroundGreenEndLieText) is not ShotLie.Green and not ShotLie.Holed;

    public double ExpectedPutts
    {
        get => expectedPutts;
        private set => SetProperty(ref expectedPutts, value);
    }

    public double ExpectedApproachShots
    {
        get => expectedApproachShots;
        private set => SetProperty(ref expectedApproachShots, value);
    }

    public double ExpectedApproachFinishStrokes
    {
        get => expectedApproachFinishStrokes;
        private set => SetProperty(ref expectedApproachFinishStrokes, value);
    }

    public double ExpectedAroundGreenStartStrokes
    {
        get => expectedAroundGreenStartStrokes;
        private set => SetProperty(ref expectedAroundGreenStartStrokes, value);
    }

    public double ExpectedAroundGreenFinishStrokes
    {
        get => expectedAroundGreenFinishStrokes;
        private set => SetProperty(ref expectedAroundGreenFinishStrokes, value);
    }

    public double StrokesGainedPutting
    {
        get => strokesGainedPutting;
        private set
        {
            if (SetProperty(ref strokesGainedPutting, value))
            {
                OnPropertyChanged(nameof(StrokesGainedPuttingText));
                OnPropertyChanged(nameof(StrokesGainedText));
            }
        }
    }

    public double StrokesGainedApproach
    {
        get => strokesGainedApproach;
        private set
        {
            if (SetProperty(ref strokesGainedApproach, value))
            {
                OnPropertyChanged(nameof(StrokesGainedApproachText));
                OnPropertyChanged(nameof(StrokesGainedText));
            }
        }
    }

    public double StrokesGainedAroundGreen
    {
        get => strokesGainedAroundGreen;
        private set
        {
            if (SetProperty(ref strokesGainedAroundGreen, value))
            {
                OnPropertyChanged(nameof(StrokesGainedAroundGreenText));
                OnPropertyChanged(nameof(StrokesGainedText));
            }
        }
    }

    public string StrokesGainedText => UiFormat.Sg(
        (IsPuttingInputVisible ? StrokesGainedPutting : 0) +
        (IsApproachInputVisible ? StrokesGainedApproach : 0) +
        (IsAroundGreenInputVisible ? StrokesGainedAroundGreen : 0));

    public string StrokesGainedPuttingText => UiFormat.Sg(StrokesGainedPutting);

    public string StrokesGainedApproachText => UiFormat.Sg(StrokesGainedApproach);

    public string StrokesGainedAroundGreenText => UiFormat.Sg(StrokesGainedAroundGreen);

    public string DetailText
    {
        get
        {
            var parts = new List<string>();
            if (IsApproachInputVisible)
            {
                parts.Add($"Indspil {FormatApproachStartDistance()} {ApproachStartLieText.ToLowerInvariant()} ({StrokesGainedApproachText})");
            }

            if (IsAroundGreenInputVisible)
            {
                parts.Add($"Omkring green {FormatAroundGreenStartDistance()} {AroundGreenStartLieText.ToLowerInvariant()} ({StrokesGainedAroundGreenText})");
            }

            if (IsPuttingInputVisible)
            {
                parts.Add($"{FormatPuttDistance()} - {Putts} putts");
            }

            return string.Join(" | ", parts);
        }
    }

    public void IncreasePutts() => Putts++;

    public void DecreasePutts() => Putts--;

    public void SetPutts(int value) => Putts = value;

    public void IncreaseFirstPuttDistance() => FirstPuttDistanceMeters += FirstPuttDistanceStepMeters;

    public void DecreaseFirstPuttDistance() => FirstPuttDistanceMeters -= FirstPuttDistanceStepMeters;

    public void IncreaseApproachShots() => ApproachShots++;

    public void DecreaseApproachShots() => ApproachShots--;

    public void IncreaseApproachDistance() => ApproachDistanceMeters += ApproachDistanceStepMeters;

    public void DecreaseApproachDistance() => ApproachDistanceMeters -= ApproachDistanceStepMeters;

    public void IncreaseApproachStartDistance() => ApproachStartDistanceYards += ApproachDistanceStepMeters;

    public void DecreaseApproachStartDistance() => ApproachStartDistanceYards -= ApproachDistanceStepMeters;

    public void IncreaseApproachEndDistance() => ApproachEndDistance += GetApproachFinishStep();

    public void DecreaseApproachEndDistance() => ApproachEndDistance -= GetApproachFinishStep();

    public void IncreaseApproachEndDistanceToGreenEdge() => ApproachEndDistanceToGreenEdgeYards += ApproachDistanceStepMeters;

    public void DecreaseApproachEndDistanceToGreenEdge() => ApproachEndDistanceToGreenEdgeYards -= ApproachDistanceStepMeters;

    public void IncreaseApproachPar() => ApproachPar++;

    public void DecreaseApproachPar() => ApproachPar--;

    public void IncreaseApproachPenaltyStrokes() => ApproachPenaltyStrokes++;

    public void DecreaseApproachPenaltyStrokes() => ApproachPenaltyStrokes--;

    public void SelectApproachStartLie(string lie) => ApproachStartLieText = lie;

    public void SelectApproachEndLie(string lie) => ApproachEndLieText = lie;

    public void IncreaseAroundGreenStartDistance() => AroundGreenStartDistanceYards += AroundGreenDistanceStepMeters;

    public void DecreaseAroundGreenStartDistance() => AroundGreenStartDistanceYards -= AroundGreenDistanceStepMeters;

    public void IncreaseAroundGreenEndDistance() => AroundGreenEndDistance += GetAroundGreenFinishStep();

    public void DecreaseAroundGreenEndDistance() => AroundGreenEndDistance -= GetAroundGreenFinishStep();

    public void IncreaseAroundGreenPenaltyStrokes() => AroundGreenPenaltyStrokes++;

    public void DecreaseAroundGreenPenaltyStrokes() => AroundGreenPenaltyStrokes--;

    public void SelectAroundGreenStartLie(string lie) => AroundGreenStartLieText = lie;

    public void SelectAroundGreenEndLie(string lie) => AroundGreenEndLieText = lie;

    public void AddAnotherAroundGreenShot()
    {
        if (!CanAddAnotherAroundGreenShot)
        {
            return;
        }

        var shot = BuildAroundGreenShot(completedAroundGreenShots.Count + 1);
        completedAroundGreenShots.Add(shot);

        aroundGreenStartDistanceText = FormatStoredDistance(ToMeters(shot.EndDistanceToPin, shot.EndDistanceUnit), 0);
        aroundGreenStartLieText = FormatAroundGreenStartLie(shot.EndLie);
        aroundGreenEndDistanceText = string.Empty;
        aroundGreenEndLieText = "Green";
        aroundGreenPenaltyStrokes = 0;
        aroundGreenHoled = false;
        carriedPuttingDistanceFromAroundGreenMeters = null;

        OnPropertyChanged(nameof(AroundGreenShotTitle));
        OnPropertyChanged(nameof(CompletedAroundGreenShotSummaries));
        OnPropertyChanged(nameof(HasCompletedAroundGreenShots));
        OnPropertyChanged(nameof(AroundGreenStartDistanceText));
        OnPropertyChanged(nameof(AroundGreenStartDistanceYards));
        OnPropertyChanged(nameof(AroundGreenStartDistanceDisplayText));
        OnPropertyChanged(nameof(AroundGreenStartLieText));
        OnPropertyChanged(nameof(AroundGreenEndDistanceText));
        OnPropertyChanged(nameof(AroundGreenEndDistance));
        OnPropertyChanged(nameof(AroundGreenEndDistanceDisplayText));
        OnPropertyChanged(nameof(AroundGreenEndLieText));
        OnPropertyChanged(nameof(AroundGreenPenaltyStrokes));
        OnPropertyChanged(nameof(AroundGreenHoled));
        OnPropertyChanged(nameof(IsAroundGreenFinishDistanceVisible));
        OnFlowVisibilityChanged();
        Recalculate();
    }

    public void SetTotalHoles(int count) => TotalHoles = count;

    public void SetTracking(RoundTrackingOptions options)
    {
        TrackPutting = options.TrackPutting;
        TrackApproach = options.TrackApproach;
        TrackAroundGreen = options.TrackAroundGreen;
        ApplyApproachCarryForward();
        ApplyAroundGreenCarryForward();
        OnFlowVisibilityChanged();
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
    }

    public HolePuttingData ToHole()
    {
        return StrokesGainedCalculator.BuildHole(
            HoleNumber,
            IsPuttingInputVisible ? ParseDistance(DistanceText) : 0,
            IsPuttingInputVisible ? Putts : 0,
            0,
            0,
            IsApproachInputVisible ? BuildApproachShot() : null,
            null,
            BuildAroundGreenShots());
    }

    public void Load(HolePuttingData hole)
    {
        distanceText = hole.FirstPuttDistanceMeters > 0 ? hole.FirstPuttDistanceMeters.ToString("0.###") : string.Empty;
        approachDistanceText = hole.ApproachDistanceMeters > 0 ? hole.ApproachDistanceMeters.ToString("0.###") : string.Empty;
        approachStartDistanceText = hole.ApproachStartDistanceYards > 0 ? FormatStoredDistance(YardsToMeters(hole.ApproachStartDistanceYards), 0) : string.Empty;
        approachEndDistanceText = hole.ApproachEndDistance > 0 ? FormatStoredDistance(ToMeters(hole.ApproachEndDistance, hole.ApproachEndDistanceUnit), 0) : string.Empty;
        approachEndDistanceToGreenEdgeText = hole.ApproachEndDistanceToGreenEdgeYards > 0 ? FormatStoredDistance(YardsToMeters(hole.ApproachEndDistanceToGreenEdgeYards), 0) : string.Empty;
        approachStartLieText = FormatLie(hole.ApproachStartLie);
        approachEndLieText = FormatLie(hole.ApproachEndLie);
        approachPenaltyStrokes = hole.ApproachPenaltyStrokes;
        approachHoled = hole.ApproachHoled;
        approachPar = hole.ApproachPar;
        approachIsTeeShot = hole.ApproachIsTeeShot;
        aroundGreenStartDistanceText = hole.AroundGreenStartDistanceYards > 0 ? FormatStoredDistance(YardsToMeters(hole.AroundGreenStartDistanceYards), 0) : string.Empty;
        aroundGreenStartLieText = FormatLie(hole.AroundGreenStartLie);
        aroundGreenEndDistanceText = hole.AroundGreenEndDistance > 0 ? FormatStoredDistance(ToMeters(hole.AroundGreenEndDistance, hole.AroundGreenEndDistanceUnit), 1) : string.Empty;
        aroundGreenEndLieText = FormatLie(hole.AroundGreenEndLie);
        aroundGreenPenaltyStrokes = hole.AroundGreenPenaltyStrokes;
        aroundGreenHoled = hole.AroundGreenHoled;
        completedAroundGreenShots.Clear();
        if (hole.AroundGreenShots is { Count: > 1 })
        {
            completedAroundGreenShots.AddRange(hole.AroundGreenShots.Take(hole.AroundGreenShots.Count - 1));
            var currentShot = hole.AroundGreenShots[^1];
            aroundGreenStartDistanceText = FormatStoredDistance(ToMeters(currentShot.StartDistanceToPin, currentShot.StartDistanceUnit), 0);
            aroundGreenStartLieText = FormatLie(currentShot.StartLie);
            aroundGreenEndDistanceText = currentShot.EndDistanceToPin > 0 ? FormatStoredDistance(ToMeters(currentShot.EndDistanceToPin, currentShot.EndDistanceUnit), 1) : string.Empty;
            aroundGreenEndLieText = FormatLie(currentShot.EndLie);
            aroundGreenPenaltyStrokes = currentShot.PenaltyStrokes;
            aroundGreenHoled = currentShot.Holed;
        }
        putts = hole.Putts;
        approachShots = hole.ApproachShots;
        expectedPutts = hole.ExpectedPutts;
        expectedApproachShots = hole.ExpectedApproachShots;
        expectedApproachFinishStrokes = hole.ExpectedApproachFinishStrokes;
        expectedAroundGreenStartStrokes = hole.ExpectedAroundGreenStartStrokes;
        expectedAroundGreenFinishStrokes = hole.ExpectedAroundGreenFinishStrokes;
        strokesGainedPutting = hole.StrokesGainedPutting;
        strokesGainedApproach = hole.StrokesGainedApproach;
        strokesGainedAroundGreen = hole.StrokesGainedAroundGreen;
        carriedPuttingDistanceFromApproachMeters = null;
        carriedPuttingDistanceFromAroundGreenMeters = null;
        carriedAroundGreenStartDistanceFromApproachMeters = null;
        carriedAroundGreenStartLieFromApproach = null;
        OnPropertyChanged(nameof(DistanceText));
        OnPropertyChanged(nameof(ApproachDistanceText));
        OnPropertyChanged(nameof(ApproachStartDistanceText));
        OnPropertyChanged(nameof(ApproachEndDistanceText));
        OnPropertyChanged(nameof(ApproachEndDistanceToGreenEdgeText));
        OnPropertyChanged(nameof(AroundGreenStartDistanceText));
        OnPropertyChanged(nameof(AroundGreenEndDistanceText));
        OnPropertyChanged(nameof(FirstPuttDistanceMeters));
        OnPropertyChanged(nameof(ApproachDistanceMeters));
        OnPropertyChanged(nameof(ApproachStartDistanceYards));
        OnPropertyChanged(nameof(ApproachEndDistance));
        OnPropertyChanged(nameof(ApproachEndDistanceToGreenEdgeYards));
        OnPropertyChanged(nameof(AroundGreenStartDistanceYards));
        OnPropertyChanged(nameof(AroundGreenEndDistance));
        OnPropertyChanged(nameof(FirstPuttDistanceDisplayText));
        OnPropertyChanged(nameof(ApproachDistanceDisplayText));
        OnPropertyChanged(nameof(ApproachStartDistanceDisplayText));
        OnPropertyChanged(nameof(ApproachEndDistanceDisplayText));
        OnPropertyChanged(nameof(ApproachEndDistanceToGreenEdgeDisplayText));
        OnPropertyChanged(nameof(ApproachStartLieText));
        OnPropertyChanged(nameof(ApproachEndLieText));
        OnPropertyChanged(nameof(ApproachPenaltyStrokes));
        OnPropertyChanged(nameof(ApproachHoled));
        OnPropertyChanged(nameof(ApproachPar));
        OnPropertyChanged(nameof(ApproachIsTeeShot));
        OnPropertyChanged(nameof(IsApproachFinishDistanceVisible));
        OnPropertyChanged(nameof(ApproachEndDistanceUnitText));
        OnFlowVisibilityChanged();
        OnPropertyChanged(nameof(AroundGreenStartDistanceDisplayText));
        OnPropertyChanged(nameof(AroundGreenEndDistanceDisplayText));
        OnPropertyChanged(nameof(AroundGreenStartLieText));
        OnPropertyChanged(nameof(AroundGreenEndLieText));
        OnPropertyChanged(nameof(AroundGreenHoled));
        OnPropertyChanged(nameof(AroundGreenPenaltyStrokes));
        OnPropertyChanged(nameof(AroundGreenShotTitle));
        OnPropertyChanged(nameof(CompletedAroundGreenShotSummaries));
        OnPropertyChanged(nameof(HasCompletedAroundGreenShots));
        OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
        OnPropertyChanged(nameof(IsAroundGreenFinishDistanceVisible));
        OnPropertyChanged(nameof(AroundGreenEndDistanceUnitText));
        OnPropertyChanged(nameof(Putts));
        OnPropertyChanged(nameof(ApproachShots));
        OnPropertyChanged(nameof(ExpectedPutts));
        OnPropertyChanged(nameof(ExpectedApproachShots));
        OnPropertyChanged(nameof(ExpectedApproachFinishStrokes));
        OnPropertyChanged(nameof(ExpectedAroundGreenStartStrokes));
        OnPropertyChanged(nameof(ExpectedAroundGreenFinishStrokes));
        OnPropertyChanged(nameof(StrokesGainedPutting));
        OnPropertyChanged(nameof(StrokesGainedApproach));
        OnPropertyChanged(nameof(StrokesGainedAroundGreen));
        OnPropertyChanged(nameof(StrokesGainedPuttingText));
        OnPropertyChanged(nameof(StrokesGainedApproachText));
        OnPropertyChanged(nameof(StrokesGainedAroundGreenText));
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
    }

    private void Recalculate()
    {
        var hole = ToHole();
        ExpectedPutts = hole.ExpectedPutts;
        ExpectedApproachShots = hole.ExpectedApproachShots;
        ExpectedApproachFinishStrokes = hole.ExpectedApproachFinishStrokes;
        ExpectedAroundGreenStartStrokes = hole.ExpectedAroundGreenStartStrokes;
        ExpectedAroundGreenFinishStrokes = hole.ExpectedAroundGreenFinishStrokes;
        StrokesGainedPutting = hole.StrokesGainedPutting;
        StrokesGainedApproach = hole.StrokesGainedApproach;
        StrokesGainedAroundGreen = hole.StrokesGainedAroundGreen;
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
    }

    private void OnTrackingChanged()
    {
        Recalculate();
        OnFlowVisibilityChanged();
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
    }

    private void OnFlowVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsApproachInputVisible));
        OnPropertyChanged(nameof(IsAroundGreenInputVisible));
        OnPropertyChanged(nameof(IsPuttingInputVisible));
        OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
    }

    private string FormatPuttDistance()
    {
        var distance = ParseDistance(DistanceText);
        return distance > 0 ? UiFormat.Meters(distance) : "-";
    }

    private string FormatApproachDistance()
    {
        var distance = ParseDistance(ApproachDistanceText);
        return distance > 0 ? UiFormat.WholeMeters(distance) : "-";
    }

    private string FormatApproachStartDistance()
    {
        var distance = ParseDistance(ApproachStartDistanceText);
        return distance > 0 ? UiFormat.WholeMeters(distance) : "-";
    }

    private string FormatApproachEndDistance()
    {
        if (ApproachHoled)
        {
            return "I hul";
        }

        var distance = ParseDistance(ApproachEndDistanceText);
        return distance > 0 ? UiFormat.WholeMeters(distance) : "-";
    }

    private string FormatApproachEndDistanceToGreenEdge()
    {
        var distance = ParseDistance(ApproachEndDistanceToGreenEdgeText);
        return distance > 0 ? UiFormat.WholeMeters(distance) : "-";
    }

    private GolfShot BuildApproachShot()
    {
        var endLie = ParseLie(ApproachEndLieText);
        return new GolfShot
        {
            HoleNumber = HoleNumber,
            Par = ApproachPar,
            ShotNumber = ApproachIsTeeShot ? 1 : 2,
            IsTeeShot = ApproachIsTeeShot,
            StartDistanceToPin = MetersToYards(ParseDistance(ApproachStartDistanceText)),
            StartDistanceUnit = DistanceUnit.Yards,
            StartLie = ParseLie(ApproachStartLieText),
            StartDistanceToGreenEdgeYards = MetersToYards(ParseDistance(ApproachStartDistanceText)),
            EndDistanceToPin = ApproachHoled ? 0 : ToShotDistance(ParseDistance(ApproachEndDistanceText), endLie),
            EndDistanceUnit = endLie == ShotLie.Green ? DistanceUnit.Feet : DistanceUnit.Yards,
            EndLie = endLie,
            EndDistanceToGreenEdgeYards = 0,
            PenaltyStrokes = ApproachPenaltyStrokes,
            Holed = ApproachHoled || endLie == ShotLie.Holed
        };
    }

    private double GetApproachFinishStep() => ApproachDistanceStepMeters;

    private string FormatAroundGreenStartDistance()
    {
        var distance = ParseDistance(AroundGreenStartDistanceText);
        return distance > 0 ? UiFormat.WholeMeters(distance) : "-";
    }

    private string FormatAroundGreenEndDistance()
    {
        if (AroundGreenHoled)
        {
            return "I hul";
        }

        var distance = ParseDistance(AroundGreenEndDistanceText);
        return distance > 0 ? UiFormat.Meters(distance) : "-";
    }

    private IReadOnlyList<GolfShot> BuildAroundGreenShots()
    {
        if (!IsAroundGreenInputVisible)
        {
            return [];
        }

        var shots = completedAroundGreenShots.ToList();
        var currentShot = BuildAroundGreenShot(shots.Count + 1);
        if (currentShot.StartDistanceToPin > 0)
        {
            shots.Add(currentShot);
        }

        return shots;
    }

    private GolfShot BuildAroundGreenShot(int shotNumber)
    {
        var endLie = ParseLie(AroundGreenEndLieText);
        return new GolfShot
        {
            HoleNumber = HoleNumber,
            ShotNumber = shotNumber,
            StartDistanceToPin = MetersToYards(ParseDistance(AroundGreenStartDistanceText)),
            StartDistanceUnit = DistanceUnit.Yards,
            StartLie = ParseLie(AroundGreenStartLieText),
            StartDistanceToGreenEdgeYards = Math.Min(MetersToYards(ParseDistance(AroundGreenStartDistanceText)), 30),
            EndDistanceToPin = AroundGreenHoled ? 0 : ToShotDistance(ParseDistance(AroundGreenEndDistanceText), endLie),
            EndDistanceUnit = endLie == ShotLie.Green ? DistanceUnit.Feet : DistanceUnit.Yards,
            EndLie = endLie,
            PenaltyStrokes = AroundGreenPenaltyStrokes,
            Holed = AroundGreenHoled || endLie == ShotLie.Holed
        };
    }

    private double GetAroundGreenFinishStep()
    {
        return ParseLie(AroundGreenEndLieText) == ShotLie.Green
            ? GreenFinishDistanceStepMeters
            : AroundGreenDistanceStepMeters;
    }

    private bool ApproachFinishedHoled => TrackApproach &&
        (ApproachHoled || ParseLie(ApproachEndLieText) == ShotLie.Holed);

    private bool ApproachFinishedOnGreenOrHoled => TrackApproach &&
        (ApproachFinishedHoled || ParseLie(ApproachEndLieText) == ShotLie.Green);

    private bool AroundGreenFinishedHoled => IsAroundGreenInputVisible &&
        (AroundGreenHoled || ParseLie(AroundGreenEndLieText) == ShotLie.Holed);

    private void ApplyAroundGreenCarryForward()
    {
        if (!TrackAroundGreen || !TrackPutting || AroundGreenFinishedHoled)
        {
            return;
        }

        if (ParseLie(AroundGreenEndLieText) != ShotLie.Green)
        {
            return;
        }

        var endDistanceMeters = ParseDistance(AroundGreenEndDistanceText);
        if (endDistanceMeters <= 0)
        {
            return;
        }

        var carriedDistanceMeters = Math.Min(endDistanceMeters, MaxFirstPuttDistanceMeters);
        if (!hasManualPuttingDistanceOverride)
        {
            ApplyApproachCarryForwardValue(() =>
            {
                FirstPuttDistanceMeters = carriedDistanceMeters;
                carriedPuttingDistanceFromAroundGreenMeters = ParseDistance(DistanceText);
                carriedPuttingDistanceFromApproachMeters = null;
            });
        }
    }

    private void ApplyApproachCarryForward()
    {
        if (!TrackApproach || ApproachFinishedHoled)
        {
            return;
        }

        var endDistanceMeters = ParseDistance(ApproachEndDistanceText);
        if (endDistanceMeters <= 0)
        {
            return;
        }

        var endLie = ParseLie(ApproachEndLieText);
        if (endLie == ShotLie.Green)
        {
            CarryApproachDistanceToPutting(endDistanceMeters);
            return;
        }

        CarryApproachDistanceToAroundGreen(endDistanceMeters, endLie);
    }

    private void CarryApproachDistanceToPutting(double endDistanceMeters)
    {
        if (!TrackPutting)
        {
            return;
        }

        var carriedDistanceMeters = Math.Min(endDistanceMeters, MaxFirstPuttDistanceMeters);
        if (!hasManualPuttingDistanceOverride)
        {
            ApplyApproachCarryForwardValue(() =>
            {
                FirstPuttDistanceMeters = carriedDistanceMeters;
                carriedPuttingDistanceFromApproachMeters = ParseDistance(DistanceText);
            });
        }
    }

    private void CarryApproachDistanceToAroundGreen(double endDistanceMeters, ShotLie endLie)
    {
        if (!TrackAroundGreen)
        {
            return;
        }

        var carriedDistanceMeters = Math.Min(endDistanceMeters, MaxAroundGreenDistanceMeters);
        if (CanReplaceCarriedValue(ParseDistance(AroundGreenStartDistanceText), carriedAroundGreenStartDistanceFromApproachMeters))
        {
            ApplyApproachCarryForwardValue(() =>
            {
                AroundGreenStartDistanceYards = carriedDistanceMeters;
                carriedAroundGreenStartDistanceFromApproachMeters = ParseDistance(AroundGreenStartDistanceText);
            });
        }

        var carriedLieText = FormatAroundGreenStartLie(endLie);
        if (string.IsNullOrWhiteSpace(carriedAroundGreenStartLieFromApproach) ||
            string.Equals(AroundGreenStartLieText, carriedAroundGreenStartLieFromApproach, StringComparison.Ordinal))
        {
            ApplyApproachCarryForwardValue(() =>
            {
                AroundGreenStartLieText = carriedLieText;
                carriedAroundGreenStartLieFromApproach = carriedLieText;
            });
        }
    }

    private void ApplyApproachCarryForwardValue(Action apply)
    {
        isApplyingApproachCarryForward = true;
        try
        {
            apply();
        }
        finally
        {
            isApplyingApproachCarryForward = false;
        }
    }

    private void ClearCarriedPuttingDistanceIfManuallyChanged()
    {
        if (carriedPuttingDistanceFromApproachMeters is not null &&
            !AreDistancesEqual(ParseDistance(DistanceText), carriedPuttingDistanceFromApproachMeters.Value))
        {
            carriedPuttingDistanceFromApproachMeters = null;
        }
    }

    private void ClearCarriedAroundGreenPuttingDistanceIfManuallyChanged()
    {
        if (carriedPuttingDistanceFromAroundGreenMeters is not null &&
            !AreDistancesEqual(ParseDistance(DistanceText), carriedPuttingDistanceFromAroundGreenMeters.Value))
        {
            carriedPuttingDistanceFromAroundGreenMeters = null;
        }
    }

    private void ClearCarriedAroundGreenDistanceIfManuallyChanged()
    {
        if (carriedAroundGreenStartDistanceFromApproachMeters is not null &&
            !AreDistancesEqual(ParseDistance(AroundGreenStartDistanceText), carriedAroundGreenStartDistanceFromApproachMeters.Value))
        {
            carriedAroundGreenStartDistanceFromApproachMeters = null;
        }
    }

    private void ClearCarriedAroundGreenLieIfManuallyChanged()
    {
        if (!string.IsNullOrWhiteSpace(carriedAroundGreenStartLieFromApproach) &&
            !string.Equals(AroundGreenStartLieText, carriedAroundGreenStartLieFromApproach, StringComparison.Ordinal))
        {
            carriedAroundGreenStartLieFromApproach = null;
        }
    }

    private static bool CanReplaceCarriedValue(double currentDistance, double? previousCarriedDistance)
    {
        return currentDistance <= 0 ||
            (previousCarriedDistance is not null && AreDistancesEqual(currentDistance, previousCarriedDistance.Value));
    }

    private static bool AreDistancesEqual(double first, double second) => Math.Abs(first - second) < 0.05;

    private static string FormatAroundGreenStartLie(ShotLie lie)
    {
        return lie switch
        {
            ShotLie.Fairway or ShotLie.FairwayCut => "Kortklippet",
            ShotLie.Sand => "Sand",
            ShotLie.Recovery => "Problemlie",
            _ => "Rough"
        };
    }

    private static ShotLie ParseLie(string text)
    {
        return text switch
        {
            "Kortklippet" => ShotLie.FairwayCut,
            "Fairway cut" => ShotLie.FairwayCut,
            "Fairway" => ShotLie.Fairway,
            "Tee" => ShotLie.Tee,
            "Sand" => ShotLie.Sand,
            "Problemlie" => ShotLie.Recovery,
            "Recovery" => ShotLie.Recovery,
            "Green" => ShotLie.Green,
            "I hul" => ShotLie.Holed,
            "Holed" => ShotLie.Holed,
            _ => ShotLie.Rough
        };
    }

    private static string FormatLie(ShotLie lie)
    {
        return lie switch
        {
            ShotLie.FairwayCut => "Kortklippet",
            ShotLie.Fairway => "Fairway",
            ShotLie.Tee => "Tee",
            ShotLie.Sand => "Sand",
            ShotLie.Recovery => "Problemlie",
            ShotLie.Green => "Green",
            ShotLie.Holed => "I hul",
            _ => "Rough"
        };
    }

    private static double ToShotDistance(double distanceMeters, ShotLie endLie)
    {
        return endLie == ShotLie.Green
            ? MetersToFeet(distanceMeters)
            : MetersToYards(distanceMeters);
    }

    private static double ToMeters(double distance, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Feet => distance * MetersPerFoot,
            DistanceUnit.Yards => YardsToMeters(distance),
            _ => distance
        };
    }

    private static double MetersToYards(double distanceMeters) => distanceMeters / MetersPerYard;

    private static double MetersToFeet(double distanceMeters) => distanceMeters / MetersPerFoot;

    private static double YardsToMeters(double distanceYards) => distanceYards * MetersPerYard;

    private static double ParseDistance(string text)
    {
        return double.TryParse(
            text,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
    }

    private static string FormatStoredDistance(double value, int decimals)
    {
        return value.ToString(decimals == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatSliderDistance(double value, int decimals)
    {
        var rounded = Math.Round(Math.Max(0, value), decimals);
        return rounded <= 0
            ? string.Empty
            : rounded.ToString(decimals == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed record AroundGreenShotSummaryViewModel(
    string Title,
    string StartText,
    string EndText,
    string PenaltyText)
{
    private const double MetersPerYard = 0.9144;
    private const double MetersPerFoot = 0.3048;

    public static AroundGreenShotSummaryViewModel FromShot(GolfShot shot, int shotNumber)
    {
        var start = $"{UiFormat.WholeMeters(ToMeters(shot.StartDistanceToPin, shot.StartDistanceUnit))} {FormatLie(shot.StartLie).ToLowerInvariant()}";
        var end = shot.Holed || shot.EndLie == ShotLie.Holed
            ? "I hul"
            : $"{UiFormat.Meters(ToMeters(shot.EndDistanceToPin, shot.EndDistanceUnit))} {FormatLie(shot.EndLie).ToLowerInvariant()}";
        var penalties = shot.PenaltyStrokes == 1
            ? "1 strafslag"
            : $"{shot.PenaltyStrokes} strafslag";

        return new AroundGreenShotSummaryViewModel(
            $"Slag omkring green {shotNumber}",
            $"Start: {start}",
            $"Slut: {end}",
            penalties);
    }

    private static double ToMeters(double distance, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Feet => distance * MetersPerFoot,
            DistanceUnit.Yards => distance * MetersPerYard,
            _ => distance
        };
    }

    private static string FormatLie(ShotLie lie)
    {
        return lie switch
        {
            ShotLie.FairwayCut => "Kortklippet",
            ShotLie.Fairway => "Fairway",
            ShotLie.Tee => "Tee",
            ShotLie.Sand => "Sand",
            ShotLie.Recovery => "Problemlie",
            ShotLie.Green => "Green",
            ShotLie.Holed => "I hul",
            _ => "Rough"
        };
    }
}
