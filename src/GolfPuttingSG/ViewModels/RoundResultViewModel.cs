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
    private RoundTrackingOptions trackingOptions = RoundTrackingOptions.PuttingOnly;

    public RoundResultViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public ObservableCollection<HoleResultItemViewModel> HoleResults { get; } = [];
    public ObservableCollection<string> Analysis { get; } = [];

    public string RoundId => round?.Id ?? string.Empty;
    public bool TrackPutting => trackingOptions.TrackPutting;
    public bool TrackApproach => trackingOptions.TrackApproach;
    public bool IsPuttingGame => trackingOptions.IsPuttingGame;
    public string ResultTitle => IsPuttingGame
        ? PuttingGame.GetTitle(trackingOptions.PuttingGameMode ?? PuttingGame.LadderMode)
        : "Resultat";

    public string TotalSgText => summary is null
        ? UiFormat.Sg(0)
        : UiFormat.Sg((TrackPutting ? summary.TotalStrokesGainedPutting : 0) + (TrackApproach ? summary.TotalStrokesGainedApproach : 0));

    public string TotalPuttingSgText => summary is null ? UiFormat.Sg(0) : UiFormat.Sg(summary.TotalStrokesGainedPutting);
    public string TotalApproachSgText => summary is null ? UiFormat.Sg(0) : UiFormat.Sg(summary.TotalStrokesGainedApproach);
    public string AverageDistanceText => summary is null ? UiFormat.Meters(0) : UiFormat.Meters(summary.AverageFirstPuttDistance);
    public string AverageApproachDistanceText => summary is null ? UiFormat.WholeMeters(0) : UiFormat.WholeMeters(summary.AverageApproachDistance);
    public string TotalPuttsText => summary?.TotalPutts.ToString() ?? "0";
    public string TargetPuttsText => IsPuttingGame ? "30" : string.Empty;
    public string TotalApproachShotsText => summary?.TotalApproachShots.ToString() ?? "0";
    public string ThreePuttRateText => summary is null ? "0%" : $"{CalculateThreePuttRate(summary):0}%";
    public string BestHoleText => FormatPuttingResult(summary?.BestHole);
    public string WorstHoleText => FormatPuttingResult(summary?.WorstHole);
    public string BestApproachHoleText => summary?.BestApproachHole is null ? "-" : $"Hul {summary.BestApproachHole.HoleNumber} ({UiFormat.Sg(summary.BestApproachHole.StrokesGainedApproach)})";
    public string WorstApproachHoleText => summary?.WorstApproachHole is null ? "-" : $"Hul {summary.WorstApproachHole.HoleNumber} ({UiFormat.Sg(summary.WorstApproachHole.StrokesGainedApproach)})";

    public async Task LoadAsync(string roundId)
    {
        round = await repository.GetRoundAsync(roundId);
        if (round is null)
        {
            return;
        }

        trackingOptions = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        summary = StrokesGainedCalculator.CalculateRoundSummary(round);
        HoleResults.Clear();
        foreach (var hole in round.Holes.Where(IsTrackedHoleCompleted))
        {
            HoleResults.Add(new HoleResultItemViewModel(hole, trackingOptions));
        }

        Analysis.Clear();
        foreach (var note in BuildAnalysis(round, summary, trackingOptions))
        {
            Analysis.Add(note);
        }

        OnAllPropertiesChanged();
    }

    private bool IsTrackedHoleCompleted(HolePuttingData hole)
    {
        return (TrackPutting && hole.IsCompleted) || (TrackApproach && hole.IsApproachCompleted);
    }

    private static double CalculateThreePuttRate(RoundSummary summary)
    {
        var completedHoles = summary.OnePutts + summary.TwoPutts + summary.ThreePuttsOrWorse;
        return completedHoles == 0 ? 0 : summary.ThreePuttsOrWorse / (double)completedHoles * 100;
    }

    private static IEnumerable<string> BuildAnalysis(Round round, RoundSummary summary, RoundTrackingOptions trackingOptions)
    {
        if (trackingOptions.TrackPutting)
        {
            if (summary.TotalStrokesGainedPutting > 0)
            {
                yield return "Du puttede bedre end PGA Tour-baseline på denne runde.";
            }
            else if (summary.TotalStrokesGainedPutting >= -2)
            {
                yield return "Du var tæt på PGA Tour-baseline på greens.";
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

        if (trackingOptions.TrackApproach)
        {
            if (summary.TotalStrokesGainedApproach > 0)
            {
                yield return "Din approach var bedre end PGA Tour-baseline.";
            }
            else if (summary.TotalStrokesGainedApproach >= -2)
            {
                yield return "Din approach var tæt på PGA Tour-baseline.";
            }
            else
            {
                yield return "Du tabte især slag på approach-slag.";
            }
        }
    }

    private void OnAllPropertiesChanged()
    {
        OnPropertyChanged(nameof(RoundId));
        OnPropertyChanged(nameof(TrackPutting));
        OnPropertyChanged(nameof(TrackApproach));
        OnPropertyChanged(nameof(IsPuttingGame));
        OnPropertyChanged(nameof(ResultTitle));
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(TotalPuttingSgText));
        OnPropertyChanged(nameof(TotalApproachSgText));
        OnPropertyChanged(nameof(AverageDistanceText));
        OnPropertyChanged(nameof(AverageApproachDistanceText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(TargetPuttsText));
        OnPropertyChanged(nameof(TotalApproachShotsText));
        OnPropertyChanged(nameof(ThreePuttRateText));
        OnPropertyChanged(nameof(BestHoleText));
        OnPropertyChanged(nameof(WorstHoleText));
        OnPropertyChanged(nameof(BestApproachHoleText));
        OnPropertyChanged(nameof(WorstApproachHoleText));
    }

    private string FormatPuttingResult(HolePuttingData? hole)
    {
        if (hole is null)
        {
            return "-";
        }

        if (!IsPuttingGame)
        {
            return $"Hul {hole.HoleNumber} ({UiFormat.Sg(hole.StrokesGainedPutting)})";
        }

        var distances = PuttingGame.GetPresetDistances(trackingOptions.PuttingGameMode ?? PuttingGame.LadderMode);
        var feet = distances[hole.HoleNumber - 1];
        return $"Putt {hole.HoleNumber}, {feet} ft ({UiFormat.Sg(hole.StrokesGainedPutting)})";
    }
}

public sealed class HoleResultItemViewModel
{
    public HoleResultItemViewModel(HolePuttingData hole, RoundTrackingOptions trackingOptions)
    {
        Title = trackingOptions.IsPuttingGame ? $"Putt {hole.HoleNumber}" : $"Hul {hole.HoleNumber}";

        var details = new List<string>();
        var sg = 0d;
        if (trackingOptions.TrackPutting)
        {
            var distances = PuttingGame.GetPresetDistances(trackingOptions.PuttingGameMode ?? PuttingGame.LadderMode);
            var distance = trackingOptions.IsPuttingGame
                ? $"{distances[hole.HoleNumber - 1]} ft"
                : UiFormat.Meters(hole.FirstPuttDistanceMeters);

            details.Add($"{distance} - {hole.Putts} putts");
            sg += hole.StrokesGainedPutting;
        }

        if (trackingOptions.TrackApproach)
        {
            details.Add($"{UiFormat.WholeMeters(hole.ApproachDistanceMeters)} - {hole.ApproachShots} approach-slag");
            sg += hole.StrokesGainedApproach;
        }

        Detail = string.Join(" | ", details);
        StrokesGainedText = UiFormat.Sg(sg);
    }

    public string Title { get; }
    public string Detail { get; }
    public string StrokesGainedText { get; }
}
