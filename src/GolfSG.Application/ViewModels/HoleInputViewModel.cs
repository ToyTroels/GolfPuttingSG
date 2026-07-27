using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public sealed partial class HoleInputViewModel : ViewModelBase
{
    private const double MaxFirstPuttDistanceMeters = 30;
    private const double MaxApproachDistanceMeters = 250;
    private const double MaxAroundGreenDistanceMeters = 50;
    private const double MaxFinishDistanceMeters = 250;
    private const double FirstPuttDistanceStepMeters = 0.1;
    private const double ApproachDistanceStepMeters = 0.1;
    private const double AroundGreenDistanceStepMeters = 1;
    private const double GreenFinishDistanceStepMeters = 0.1;

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
    private readonly IDistanceUnitSettings distanceUnitSettings;
    private readonly List<GolfShot> completedAroundGreenShots = [];
    private double? carriedPuttingDistanceFromApproachMeters;
    private double? carriedPuttingDistanceFromAroundGreenMeters;
    private double? carriedAroundGreenStartDistanceFromApproachMeters;
    private string? carriedAroundGreenStartLieFromApproach;
    private bool isApplyingApproachCarryForward;
    private bool hasManualPuttingDistanceOverride;

    public HoleInputViewModel(int holeNumber, IDistanceUnitSettings? distanceUnitSettings = null)
    {
        HoleNumber = holeNumber;
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
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
            if (SetProperty(ref distanceText, value ?? string.Empty))
            {
                if (!isApplyingApproachCarryForward)
                {
                    hasManualPuttingDistanceOverride = !string.IsNullOrWhiteSpace(distanceText);
                    ClearCarriedPuttingDistanceIfManuallyChanged();
                    ClearCarriedAroundGreenPuttingDistanceIfManuallyChanged();
                }

                NotifyPuttingDistanceCarryForwardChanged();
                OnPropertyChanged(nameof(FirstPuttDistanceMeters));
                OnPropertyChanged(nameof(FirstPuttDistanceDisplayText));
                OnPropertyChanged(nameof(CanAdvancePuttingStep));
                Recalculate();
            }
        }
    }

    public double FirstPuttDistanceMeters
    {
        get => ParsePuttingDistance(DistanceText);
        set => DistanceText = FormatPuttingInputDistance(Math.Clamp(value, 0, MaxFirstPuttDistanceMeters), 1);
    }

    public string FirstPuttDistanceDisplayText => FormatPuttDistance();

    public string PuttingDistanceUnitText => UiFormat.PuttingDistanceUnitText(PuttingDistanceUnit);

    public PuttingDistanceUnitPreference PuttingDistanceUnitPreference => PuttingDistanceUnit;

    public int Putts
    {
        get => putts;
        set
        {
            if (SetProperty(ref putts, Math.Clamp(value, 0, 5)))
            {
                OnPropertyChanged(nameof(CanAdvancePuttingStep));
                Recalculate();
            }
        }
    }

    public string ApproachDistanceText
    {
        get => approachDistanceText;
        set
        {
            if (SetProperty(ref approachDistanceText, value ?? string.Empty))
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
        set => ApproachDistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxApproachDistanceMeters), 1);
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
            if (SetProperty(ref approachStartDistanceText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(ApproachStartDistanceYards));
                OnPropertyChanged(nameof(ApproachStartDistanceDisplayText));
                OnPropertyChanged(nameof(CanAdvanceApproachStep));
                Recalculate();
            }
        }
    }

    public double ApproachStartDistanceYards
    {
        get => ParseDistance(ApproachStartDistanceText);
        set => ApproachStartDistanceText = FormatSliderDistance(Math.Clamp(value, 0, MaxApproachDistanceMeters), 1);
    }

    public string ApproachStartDistanceDisplayText => FormatApproachStartDistance();

    public string ApproachEndDistanceText
    {
        get => approachEndDistanceText;
        set
        {
            if (SetProperty(ref approachEndDistanceText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(ApproachEndDistance));
                OnPropertyChanged(nameof(ApproachEndDistanceDisplayText));
                OnPropertyChanged(nameof(CanAdvanceApproachStep));
                ApplyApproachCarryForward();
                Recalculate();
            }
        }
    }

    public double ApproachEndDistance
    {
        get => ParseFinishDistance(ApproachEndDistanceText, ParseLie(ApproachEndLieText));
        set => ApproachEndDistanceText = FormatFinishInputDistance(
            Math.Clamp(value, 0, MaxFinishDistanceMeters),
            ParseLie(ApproachEndLieText),
            GetApproachFinishDistanceDecimals());
    }

    public string ApproachEndDistanceDisplayText => FormatApproachEndDistance();

    public string ApproachEndDistanceToGreenEdgeText
    {
        get => approachEndDistanceToGreenEdgeText;
        set
        {
            if (SetProperty(ref approachEndDistanceToGreenEdgeText, value ?? string.Empty))
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
        set => ApproachEndDistanceToGreenEdgeText = FormatSliderDistance(Math.Clamp(value, 0, MaxApproachDistanceMeters), 1);
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
                OnPropertyChanged(nameof(CanAdvanceApproachStep));
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
                OnPropertyChanged(nameof(CanAdvanceApproachStep));
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

    public bool IsApproachEndOnGreen => IsApproachFinishDistanceVisible &&
        ParseLie(ApproachEndLieText) == ShotLie.Green;

    public bool IsApproachEndOffGreen => IsApproachFinishDistanceVisible &&
        ParseLie(ApproachEndLieText) is not ShotLie.Green and not ShotLie.Holed;

    public string ApproachEndDistanceUnitText => ParseLie(ApproachEndLieText) == ShotLie.Green
        ? PuttingDistanceUnitText
        : "m";

    public bool IsApproachInputVisible => TrackApproach;

    public bool IsAroundGreenInputVisible => TrackAroundGreen && !ApproachFinishedOnGreenOrHoled;

    public bool IsPuttingInputVisible => TrackPutting && !ApproachFinishedHoled && !AroundGreenFinishedHoled;

    public bool HasCarriedPuttingDistance => carriedPuttingDistanceFromApproachMeters is not null ||
        carriedPuttingDistanceFromAroundGreenMeters is not null;

    public bool IsPuttingDistanceInputVisible => IsPuttingInputVisible && !HasCarriedPuttingDistance;

    public string CarriedPuttingDistanceText => HasCarriedPuttingDistance
        ? $"F\u00f8rste putt: {FormatPuttDistance()} (overf\u00f8rt fra {CarriedPuttingDistanceSourceText})"
        : string.Empty;

    private string CarriedPuttingDistanceSourceText => carriedPuttingDistanceFromAroundGreenMeters is not null
        ? "omkring green"
        : "indspil";

    public bool CanAdvanceApproachStep => IsApproachInputVisible &&
        ParseDistance(ApproachStartDistanceText) > 0 &&
        (ApproachHoled ||
            ParseLie(ApproachEndLieText) == ShotLie.Holed ||
            ParseFinishDistance(ApproachEndDistanceText, ParseLie(ApproachEndLieText)) > 0);

    public bool CanAdvanceAroundGreenStep => IsAroundGreenInputVisible &&
        ParseDistance(AroundGreenStartDistanceText) > 0 &&
        (AroundGreenHoled ||
            ParseLie(AroundGreenEndLieText) == ShotLie.Holed ||
            (ParseLie(AroundGreenEndLieText) == ShotLie.Green &&
                ParseFinishDistance(AroundGreenEndDistanceText, ParseLie(AroundGreenEndLieText)) > 0));

    public bool CanAdvancePuttingStep => IsPuttingInputVisible &&
        ParsePuttingDistance(DistanceText) > 0 &&
        Putts > 0;

    public string AroundGreenStartDistanceText
    {
        get => aroundGreenStartDistanceText;
        set
        {
            var normalized = DistanceInputParser.NormalizeDecimalSeparator(value);
            if (SetProperty(ref aroundGreenStartDistanceText, normalized))
            {
                if (!isApplyingApproachCarryForward)
                {
                    ClearCarriedAroundGreenDistanceIfManuallyChanged();
                }

                OnPropertyChanged(nameof(AroundGreenStartDistanceYards));
                OnPropertyChanged(nameof(AroundGreenStartDistanceDisplayText));
                OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
                OnPropertyChanged(nameof(CanAdvanceAroundGreenStep));
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
            var normalized = DistanceInputParser.NormalizeDecimalSeparator(value);
            if (SetProperty(ref aroundGreenEndDistanceText, normalized))
            {
                OnPropertyChanged(nameof(AroundGreenEndDistance));
                OnPropertyChanged(nameof(AroundGreenEndDistanceDisplayText));
                OnPropertyChanged(nameof(CanAddAnotherAroundGreenShot));
                OnPropertyChanged(nameof(CanAdvanceAroundGreenStep));
                ApplyAroundGreenCarryForward();
                Recalculate();
            }
        }
    }

    public double AroundGreenEndDistance
    {
        get => ParseFinishDistance(AroundGreenEndDistanceText, ParseLie(AroundGreenEndLieText));
        set => AroundGreenEndDistanceText = FormatFinishInputDistance(Math.Clamp(value, 0, MaxFinishDistanceMeters), ParseLie(AroundGreenEndLieText), 1);
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
                OnPropertyChanged(nameof(CanAdvanceAroundGreenStep));
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
                OnPropertyChanged(nameof(CanAdvanceAroundGreenStep));
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

    public bool IsAroundGreenEndOnGreen => IsAroundGreenFinishDistanceVisible &&
        ParseLie(AroundGreenEndLieText) == ShotLie.Green;

    public bool IsAroundGreenEndOffGreen => IsAroundGreenFinishDistanceVisible &&
        ParseLie(AroundGreenEndLieText) is not ShotLie.Green and not ShotLie.Holed;

    public string AroundGreenEndDistanceUnitText => ParseLie(AroundGreenEndLieText) == ShotLie.Green
        ? PuttingDistanceUnitText
        : "m";

    public string AroundGreenShotTitle => completedAroundGreenShots.Count == 0
        ? "Omkring green"
        : $"Omkring green slag {completedAroundGreenShots.Count + 1}";

    public IReadOnlyList<AroundGreenShotSummaryViewModel> CompletedAroundGreenShotSummaries =>
        completedAroundGreenShots
            .Select((shot, index) => AroundGreenShotSummaryViewModel.FromShot(shot, index + 1, PuttingDistanceUnit))
            .ToList();

    public bool HasCompletedAroundGreenShots => completedAroundGreenShots.Count > 0;

    public bool CanUndoLastAroundGreenShot => completedAroundGreenShots.Count > 0;

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

    public void EditCarriedPuttingDistance()
    {
        if (!HasCarriedPuttingDistance)
        {
            return;
        }

        carriedPuttingDistanceFromApproachMeters = null;
        carriedPuttingDistanceFromAroundGreenMeters = null;
        hasManualPuttingDistanceOverride = true;
        NotifyPuttingDistanceCarryForwardChanged();
    }

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
        LoadNextAroundGreenShotFromPreviousFinish(shot);
        carriedPuttingDistanceFromAroundGreenMeters = null;
        NotifyPuttingDistanceCarryForwardChanged();
        NotifyAroundGreenShotStateChanged();
        Recalculate();
    }

    public void UndoLastAroundGreenShot()
    {
        if (!CanUndoLastAroundGreenShot)
        {
            return;
        }

        var shot = completedAroundGreenShots[^1];
        completedAroundGreenShots.RemoveAt(completedAroundGreenShots.Count - 1);
        LoadAroundGreenShotForEditing(shot);
        carriedPuttingDistanceFromAroundGreenMeters = null;
        NotifyPuttingDistanceCarryForwardChanged();
        NotifyAroundGreenShotStateChanged();
        ApplyAroundGreenCarryForward();
        Recalculate();
    }

    private void LoadNextAroundGreenShotFromPreviousFinish(GolfShot shot)
    {
        aroundGreenStartDistanceText = FormatStoredDistance(ToMeters(shot.EndDistanceToPin, shot.EndDistanceUnit), 0);
        aroundGreenStartLieText = FormatAroundGreenStartLie(shot.EndLie);
        aroundGreenEndDistanceText = string.Empty;
        aroundGreenEndLieText = "Green";
        aroundGreenPenaltyStrokes = 0;
        aroundGreenHoled = false;
    }

    private void LoadAroundGreenShotForEditing(GolfShot shot)
    {
        aroundGreenStartDistanceText = FormatStoredDistance(ToMeters(shot.StartDistanceToPin, shot.StartDistanceUnit), 0);
        aroundGreenStartLieText = FormatAroundGreenStartLie(shot.StartLie);
        aroundGreenEndDistanceText = shot.Holed || shot.EndLie == ShotLie.Holed
            ? string.Empty
            : FormatStoredDistance(ToMeters(shot.EndDistanceToPin, shot.EndDistanceUnit), 1);
        aroundGreenEndLieText = FormatLie(shot.EndLie);
        aroundGreenPenaltyStrokes = shot.PenaltyStrokes;
        aroundGreenHoled = shot.Holed;
    }

    private void NotifyAroundGreenShotStateChanged()
    {
        OnPropertyChanged(nameof(AroundGreenShotTitle));
        OnPropertyChanged(nameof(CompletedAroundGreenShotSummaries));
        OnPropertyChanged(nameof(HasCompletedAroundGreenShots));
        OnPropertyChanged(nameof(CanUndoLastAroundGreenShot));
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
        OnPropertyChanged(nameof(AroundGreenEndDistanceUnitText));
        OnFlowVisibilityChanged();
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
            IsPuttingInputVisible ? ParsePuttingDistance(DistanceText) : 0,
            IsPuttingInputVisible ? Putts : 0,
            0,
            0,
            IsApproachInputVisible ? BuildApproachShot() : null,
            null,
            BuildAroundGreenShots());
    }

    public void Load(HolePuttingData hole)
    {
        distanceText = hole.FirstPuttDistanceMeters > 0 ? FormatPuttingInputDistance(hole.FirstPuttDistanceMeters, 1) : string.Empty;
        approachDistanceText = hole.ApproachDistanceMeters > 0 ? FormatStoredDistance(hole.ApproachDistanceMeters, 1) : string.Empty;
        approachStartDistanceText = hole.ApproachStartDistanceYards > 0 ? FormatStoredDistance(YardsToMeters(hole.ApproachStartDistanceYards), 1) : string.Empty;
        approachEndDistanceText = hole.ApproachEndDistance > 0
            ? FormatFinishInputDistance(
                ToMeters(hole.ApproachEndDistance, hole.ApproachEndDistanceUnit),
                hole.ApproachEndLie,
                1)
            : string.Empty;
        approachEndDistanceToGreenEdgeText = hole.ApproachEndDistanceToGreenEdgeYards > 0 ? FormatStoredDistance(YardsToMeters(hole.ApproachEndDistanceToGreenEdgeYards), 1) : string.Empty;
        approachStartLieText = FormatLie(hole.ApproachStartLie);
        approachEndLieText = FormatLie(hole.ApproachEndLie);
        approachPenaltyStrokes = hole.ApproachPenaltyStrokes;
        approachHoled = hole.ApproachHoled;
        approachPar = hole.ApproachPar;
        approachIsTeeShot = hole.ApproachIsTeeShot;
        aroundGreenStartDistanceText = hole.AroundGreenStartDistanceYards > 0 ? FormatStoredDistance(YardsToMeters(hole.AroundGreenStartDistanceYards), 0) : string.Empty;
        aroundGreenStartLieText = FormatLie(hole.AroundGreenStartLie);
        aroundGreenEndDistanceText = hole.AroundGreenEndDistance > 0 ? FormatFinishInputDistance(ToMeters(hole.AroundGreenEndDistance, hole.AroundGreenEndDistanceUnit), hole.AroundGreenEndLie, 1) : string.Empty;
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
            aroundGreenEndDistanceText = currentShot.EndDistanceToPin > 0 ? FormatFinishInputDistance(ToMeters(currentShot.EndDistanceToPin, currentShot.EndDistanceUnit), currentShot.EndLie, 1) : string.Empty;
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
        hasManualPuttingDistanceOverride = false;
        NotifyPuttingDistanceCarryForwardChanged();
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
}
