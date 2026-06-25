using System.Collections.ObjectModel;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class RoundInputViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;
    private string roundId = Guid.NewGuid().ToString("N");
    private DateTime date = DateTime.Now;
    private RoundSummary summary = StrokesGainedCalculator.CalculateRoundSummary(Round.Empty());
    private int holeCount = 18;
    private bool trackPutting = true;
    private bool trackApproach;

    public RoundInputViewModel(IRoundRepository repository)
    {
        this.repository = repository;
        Holes = [];
        SetHoleCount(18);
    }

    public ObservableCollection<HoleInputViewModel> Holes { get; }

    public string ScreenTitle { get; private set; } = "Ny runde";

    public int HoleCount
    {
        get => holeCount;
        private set
        {
            if (SetProperty(ref holeCount, value))
            {
                OnPropertyChanged(nameof(HoleCountText));
            }
        }
    }

    public string HoleCountText => $"{HoleCount} huller";

    public bool TrackPutting
    {
        get => trackPutting;
        set
        {
            if (!value && !TrackApproach)
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
            if (!value && !TrackPutting)
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

    public string TotalSgText => UiFormat.Sg(
        (TrackPutting ? summary.TotalStrokesGainedPutting : 0) +
        (TrackApproach ? summary.TotalStrokesGainedApproach : 0));

    public string TotalPuttingSgText => UiFormat.Sg(summary.TotalStrokesGainedPutting);

    public string TotalApproachSgText => UiFormat.Sg(summary.TotalStrokesGainedApproach);

    public string TotalPuttsText => summary.TotalPutts.ToString();

    public string TotalApproachShotsText => summary.TotalApproachShots.ToString();

    public string CountBreakdownText => $"1-putts: {summary.OnePutts} | 2-putts: {summary.TwoPutts} | 3-putts+: {summary.ThreePuttsOrWorse}";

    public void IncreaseHoleCount() => SetHoleCount(HoleCount + 1);

    public void DecreaseHoleCount() => SetHoleCount(HoleCount - 1);

    public async Task LoadAsync(string? existingRoundId)
    {
        if (string.IsNullOrWhiteSpace(existingRoundId))
        {
            return;
        }

        var round = await repository.GetRoundAsync(existingRoundId);
        if (round is null)
        {
            return;
        }

        roundId = round.Id;
        date = round.Date;
        var options = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        trackPutting = options.TrackPutting;
        trackApproach = options.TrackApproach;
        ScreenTitle = "Rediger runde";
        OnPropertyChanged(nameof(ScreenTitle));
        OnTrackingPropertiesChanged();
        SetHoleCount(Math.Clamp(round.Holes.Count, 1, 18));

        foreach (var hole in round.Holes)
        {
            Holes.First(input => input.HoleNumber == hole.HoleNumber).Load(hole);
        }

        RefreshSummary();
    }

    public async Task<string> SaveAsync()
    {
        var round = BuildRound();
        await repository.SaveRoundAsync(round);
        return round.Id;
    }

    private Round BuildRound()
    {
        return new Round(
            roundId,
            date,
            Holes.Select(hole => hole.ToHole()).ToList(),
            new RoundTrackingOptions(TrackPutting, TrackApproach));
    }

    private void SetHoleCount(int count)
    {
        var clampedCount = Math.Clamp(count, 1, 18);
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
            hole.SetTracking(new RoundTrackingOptions(TrackPutting, TrackApproach));
        }

        RefreshSummary();
    }

    private void AddHole(int holeNumber)
    {
        var hole = new HoleInputViewModel(holeNumber);
        hole.SetTracking(new RoundTrackingOptions(TrackPutting, TrackApproach));
        hole.PropertyChanged += (_, _) => RefreshSummary();
        Holes.Add(hole);
    }

    private void RefreshSummary()
    {
        summary = StrokesGainedCalculator.CalculateRoundSummary(BuildRound());
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(TotalPuttingSgText));
        OnPropertyChanged(nameof(TotalApproachSgText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(TotalApproachShotsText));
        OnPropertyChanged(nameof(CountBreakdownText));
    }

    private void ApplyTrackingToHoles()
    {
        var options = new RoundTrackingOptions(TrackPutting, TrackApproach);
        foreach (var hole in Holes)
        {
            hole.SetTracking(options);
        }
    }

    private void OnTrackingPropertiesChanged()
    {
        OnPropertyChanged(nameof(TrackPutting));
        OnPropertyChanged(nameof(TrackApproach));
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(TotalPuttingSgText));
        OnPropertyChanged(nameof(TotalApproachSgText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(TotalApproachShotsText));
        OnPropertyChanged(nameof(CountBreakdownText));
    }
}
