using System.Collections.ObjectModel;
using GolfPuttingSG.Core;
using GolfPuttingSG.Core.Models;
using GolfPuttingSG.Services;

namespace GolfPuttingSG.ViewModels;

public sealed class RoundInputViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;
    private string roundId = Guid.NewGuid().ToString("N");
    private DateTime date = DateTime.Today;
    private RoundSummary summary = StrokesGainedCalculator.CalculateRoundSummary(Round.Empty());
    private int holeCount = 18;

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

    public string TotalSgText => UiFormat.Sg(summary.TotalStrokesGainedPutting);
    public string TotalPuttsText => summary.TotalPutts.ToString();
    public string CountBreakdownText => $"1-putts: {summary.OnePutts} · 2-putts: {summary.TwoPutts} · 3-putts+: {summary.ThreePuttsOrWorse}";

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
        ScreenTitle = "Rediger runde";
        OnPropertyChanged(nameof(ScreenTitle));
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
        return new Round(roundId, date, Holes.Select(hole => hole.ToHole()).ToList());
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
        }

        RefreshSummary();
    }

    private void AddHole(int holeNumber)
    {
        var hole = new HoleInputViewModel(holeNumber);
        hole.PropertyChanged += (_, _) => RefreshSummary();
        Holes.Add(hole);
    }

    private void RefreshSummary()
    {
        summary = StrokesGainedCalculator.CalculateRoundSummary(BuildRound());
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(CountBreakdownText));
    }
}
