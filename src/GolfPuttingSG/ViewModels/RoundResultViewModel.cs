using System.Collections.ObjectModel;
using GolfPuttingSG.Core;
using GolfPuttingSG.Core.Models;
using GolfPuttingSG.Services;

namespace GolfPuttingSG.ViewModels;

public sealed class RoundResultViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;
    private Round? round;
    private RoundSummary? summary;

    public RoundResultViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public ObservableCollection<HoleResultItemViewModel> HoleResults { get; } = [];
    public ObservableCollection<string> Analysis { get; } = [];

    public string RoundId => round?.Id ?? string.Empty;
    public string TotalSgText => summary is null ? UiFormat.Sg(0) : UiFormat.Sg(summary.TotalStrokesGainedPutting);
    public string AverageDistanceText => summary is null ? UiFormat.Meters(0) : UiFormat.Meters(summary.AverageFirstPuttDistance);
    public string TotalPuttsText => summary?.TotalPutts.ToString() ?? "0";
    public string ThreePuttRateText => summary is null ? "0%" : $"{CalculateThreePuttRate(summary):0}%";
    public string BestHoleText => summary?.BestHole is null ? "-" : $"Hul {summary.BestHole.HoleNumber} ({UiFormat.Sg(summary.BestHole.StrokesGainedPutting)})";
    public string WorstHoleText => summary?.WorstHole is null ? "-" : $"Hul {summary.WorstHole.HoleNumber} ({UiFormat.Sg(summary.WorstHole.StrokesGainedPutting)})";

    public async Task LoadAsync(string roundId)
    {
        round = await repository.GetRoundAsync(roundId);
        if (round is null)
        {
            return;
        }

        summary = StrokesGainedCalculator.CalculateRoundSummary(round);
        HoleResults.Clear();
        foreach (var hole in round.Holes.Where(hole => hole.IsCompleted))
        {
            HoleResults.Add(new HoleResultItemViewModel(hole));
        }

        Analysis.Clear();
        foreach (var note in BuildAnalysis(round, summary))
        {
            Analysis.Add(note);
        }

        OnPropertyChanged(nameof(RoundId));
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(AverageDistanceText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(ThreePuttRateText));
        OnPropertyChanged(nameof(BestHoleText));
        OnPropertyChanged(nameof(WorstHoleText));
    }

    private static double CalculateThreePuttRate(RoundSummary summary)
    {
        var completedHoles = summary.OnePutts + summary.TwoPutts + summary.ThreePuttsOrWorse;
        return completedHoles == 0 ? 0 : summary.ThreePuttsOrWorse / (double)completedHoles * 100;
    }

    private static IEnumerable<string> BuildAnalysis(Round round, RoundSummary summary)
    {
        if (summary.TotalStrokesGainedPutting > 0)
        {
            yield return "Du puttede bedre end PGA Tour-baseline på denne runde.";
        }
        else if (summary.TotalStrokesGainedPutting >= -2)
        {
            yield return "Du var tæt på PGA Tour-baseline.";
        }
        else
        {
            yield return "Du tabte især slag på greens.";
        }

        if (summary.ThreePuttsOrWorse >= 3)
        {
            yield return "Fokusområde: længdekontrol på lange putts.";
        }

        var missedShortPutts = round.Holes.Count(hole =>
            hole.IsCompleted &&
            hole.FirstPuttDistanceMeters <= 1.5 &&
            hole.Putts >= 2);

        if (missedShortPutts >= 2)
        {
            yield return "Fokusområde: korte putts.";
        }
    }
}

public sealed class HoleResultItemViewModel
{
    public HoleResultItemViewModel(HolePuttingData hole)
    {
        Title = $"Hul {hole.HoleNumber}";
        Detail = $"{UiFormat.Meters(hole.FirstPuttDistanceMeters)} · {hole.Putts} putts";
        StrokesGainedText = UiFormat.Sg(hole.StrokesGainedPutting);
    }

    public string Title { get; }
    public string Detail { get; }
    public string StrokesGainedText { get; }
}
