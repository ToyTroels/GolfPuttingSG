using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.ViewModels;

public sealed class HoleInputViewModel : ViewModelBase
{
    private const double MaxFirstPuttDistanceMeters = 30;
    private const double MaxApproachDistanceMeters = 250;
    private const double FirstPuttDistanceStepMeters = 0.1;
    private const double ApproachDistanceStepMeters = 1;

    private string distanceText = string.Empty;
    private string approachDistanceText = string.Empty;
    private int putts;
    private int approachShots;
    private double expectedPutts;
    private double expectedApproachShots;
    private double strokesGainedPutting;
    private double strokesGainedApproach;
    private int totalHoles = 18;
    private bool trackPutting = true;
    private bool trackApproach;

    public HoleInputViewModel(int holeNumber)
    {
        HoleNumber = holeNumber;
    }

    public int HoleNumber { get; }

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

    public string DistanceText
    {
        get => distanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref distanceText, normalized))
            {
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

    public string StrokesGainedText => UiFormat.Sg(
        (TrackPutting ? StrokesGainedPutting : 0) +
        (TrackApproach ? StrokesGainedApproach : 0));

    public string StrokesGainedPuttingText => UiFormat.Sg(StrokesGainedPutting);

    public string StrokesGainedApproachText => UiFormat.Sg(StrokesGainedApproach);

    public string DetailText
    {
        get
        {
            var parts = new List<string>();
            if (TrackPutting)
            {
                parts.Add($"{FormatPuttDistance()} - {Putts} putts");
            }

            if (TrackApproach)
            {
                parts.Add($"{FormatApproachDistance()} - {ApproachShots} approach-slag");
            }

            return string.Join(" | ", parts);
        }
    }

    public void IncreasePutts() => Putts++;

    public void DecreasePutts() => Putts--;

    public void IncreaseFirstPuttDistance() => FirstPuttDistanceMeters += FirstPuttDistanceStepMeters;

    public void DecreaseFirstPuttDistance() => FirstPuttDistanceMeters -= FirstPuttDistanceStepMeters;

    public void IncreaseApproachShots() => ApproachShots++;

    public void DecreaseApproachShots() => ApproachShots--;

    public void IncreaseApproachDistance() => ApproachDistanceMeters += ApproachDistanceStepMeters;

    public void DecreaseApproachDistance() => ApproachDistanceMeters -= ApproachDistanceStepMeters;

    public void SetTotalHoles(int count) => TotalHoles = count;

    public void SetTracking(RoundTrackingOptions options)
    {
        TrackPutting = options.TrackPutting;
        TrackApproach = options.TrackApproach;
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
    }

    public HolePuttingData ToHole()
    {
        return StrokesGainedCalculator.BuildHole(
            HoleNumber,
            TrackPutting ? ParseDistance(DistanceText) : 0,
            TrackPutting ? Putts : 0,
            TrackApproach ? ParseDistance(ApproachDistanceText) : 0,
            TrackApproach ? ApproachShots : 0);
    }

    public void Load(HolePuttingData hole)
    {
        distanceText = hole.FirstPuttDistanceMeters > 0 ? hole.FirstPuttDistanceMeters.ToString("0.###") : string.Empty;
        approachDistanceText = hole.ApproachDistanceMeters > 0 ? hole.ApproachDistanceMeters.ToString("0.###") : string.Empty;
        putts = hole.Putts;
        approachShots = hole.ApproachShots;
        expectedPutts = hole.ExpectedPutts;
        expectedApproachShots = hole.ExpectedApproachShots;
        strokesGainedPutting = hole.StrokesGainedPutting;
        strokesGainedApproach = hole.StrokesGainedApproach;
        OnPropertyChanged(nameof(DistanceText));
        OnPropertyChanged(nameof(ApproachDistanceText));
        OnPropertyChanged(nameof(FirstPuttDistanceMeters));
        OnPropertyChanged(nameof(ApproachDistanceMeters));
        OnPropertyChanged(nameof(FirstPuttDistanceDisplayText));
        OnPropertyChanged(nameof(ApproachDistanceDisplayText));
        OnPropertyChanged(nameof(Putts));
        OnPropertyChanged(nameof(ApproachShots));
        OnPropertyChanged(nameof(ExpectedPutts));
        OnPropertyChanged(nameof(ExpectedApproachShots));
        OnPropertyChanged(nameof(StrokesGainedPutting));
        OnPropertyChanged(nameof(StrokesGainedApproach));
        OnPropertyChanged(nameof(StrokesGainedPuttingText));
        OnPropertyChanged(nameof(StrokesGainedApproachText));
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
    }

    private void Recalculate()
    {
        var hole = ToHole();
        ExpectedPutts = hole.ExpectedPutts;
        ExpectedApproachShots = hole.ExpectedApproachShots;
        StrokesGainedPutting = hole.StrokesGainedPutting;
        StrokesGainedApproach = hole.StrokesGainedApproach;
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
    }

    private void OnTrackingChanged()
    {
        Recalculate();
        OnPropertyChanged(nameof(StrokesGainedText));
        OnPropertyChanged(nameof(DetailText));
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

    private static string FormatSliderDistance(double value, int decimals)
    {
        var rounded = Math.Round(Math.Max(0, value), decimals);
        return rounded <= 0
            ? string.Empty
            : rounded.ToString(decimals == 0 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}
