using GolfPuttingSG.Core;
using GolfPuttingSG.Core.Models;

namespace GolfPuttingSG.ViewModels;

public sealed class HoleInputViewModel : ViewModelBase
{
    private string distanceText = string.Empty;
    private int putts;
    private double expectedPutts;
    private double strokesGainedPutting;
    private int totalHoles = 18;

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

    public string DistanceText
    {
        get => distanceText;
        set
        {
            var normalized = value.Replace(',', '.');
            if (SetProperty(ref distanceText, normalized))
            {
                Recalculate();
            }
        }
    }

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

    public double ExpectedPutts
    {
        get => expectedPutts;
        private set => SetProperty(ref expectedPutts, value);
    }

    public double StrokesGainedPutting
    {
        get => strokesGainedPutting;
        private set
        {
            if (SetProperty(ref strokesGainedPutting, value))
            {
                OnPropertyChanged(nameof(StrokesGainedText));
            }
        }
    }

    public string StrokesGainedText => UiFormat.Sg(StrokesGainedPutting);

    public void IncreasePutts() => Putts++;

    public void DecreasePutts() => Putts--;

    public void SetTotalHoles(int count) => TotalHoles = count;

    public HolePuttingData ToHole()
    {
        return StrokesGainedCalculator.BuildHole(HoleNumber, ParseDistance(), Putts);
    }

    public void Load(HolePuttingData hole)
    {
        distanceText = hole.FirstPuttDistanceMeters > 0 ? hole.FirstPuttDistanceMeters.ToString("0.###") : string.Empty;
        putts = hole.Putts;
        expectedPutts = hole.ExpectedPutts;
        strokesGainedPutting = hole.StrokesGainedPutting;
        OnPropertyChanged(nameof(DistanceText));
        OnPropertyChanged(nameof(Putts));
        OnPropertyChanged(nameof(ExpectedPutts));
        OnPropertyChanged(nameof(StrokesGainedPutting));
        OnPropertyChanged(nameof(StrokesGainedText));
    }

    private void Recalculate()
    {
        var hole = ToHole();
        ExpectedPutts = hole.ExpectedPutts;
        StrokesGainedPutting = hole.StrokesGainedPutting;
    }

    private double ParseDistance()
    {
        return double.TryParse(
            DistanceText,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
    }
}
