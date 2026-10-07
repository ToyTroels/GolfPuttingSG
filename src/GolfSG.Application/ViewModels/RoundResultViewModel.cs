using System.Collections.ObjectModel;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Queries;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public sealed class RoundResultViewModel : ViewModelBase
{
    private readonly IRoundResultQueryService queryService;
    private readonly IDistanceUnitSettings distanceUnitSettings;
    private Round? round;
    private RoundSummary? summary;
    private ExpectedBirdiesSummary expectedBirdies = new(0, 0, 0);
    private RoundTrackingOptions trackingOptions = RoundTrackingOptions.PuttingOnly;

    public RoundResultViewModel(IRoundRepository repository, IDistanceUnitSettings? distanceUnitSettings = null)
    {
        queryService = new RoundResultQueryService(repository);
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
    }

    public ObservableCollection<HoleResultItemViewModel> HoleResults { get; } = [];
    public ObservableCollection<string> Analysis { get; } = [];
    public bool HasAnalysis => Analysis.Count > 0;
    public ObservableCollection<PuttingDistanceBucketItemViewModel> PuttingDistanceBuckets { get; } = [];

    public string RoundId => round?.Id ?? string.Empty;
    public bool TrackPutting => trackingOptions.TrackPutting;
    public bool TrackApproach => trackingOptions.TrackApproach;
    public bool TrackAroundGreen => trackingOptions.TrackAroundGreen;
    public bool IsPuttingGame => trackingOptions.IsPuttingGame;
    public string ResultTitle => IsPuttingGame
        ? PuttingGame.GetTitle(round?.GameInfo, trackingOptions.PuttingGameMode)
        : "Resultat";

    public string TotalSgText => summary is null
        ? UiFormat.Sg(0)
        : UiFormat.Sg(
            (TrackPutting ? summary.TotalStrokesGainedPutting : 0) +
            (TrackApproach ? summary.TotalStrokesGainedApproach : 0) +
            (TrackAroundGreen ? summary.TotalStrokesGainedAroundGreen : 0));

    public string TotalPuttingSgText => summary is null ? UiFormat.Sg(0) : UiFormat.Sg(summary.TotalStrokesGainedPutting);
    public string TotalApproachSgText => summary is null ? UiFormat.Sg(0) : UiFormat.Sg(summary.TotalStrokesGainedApproach);
    public string TotalAroundGreenSgText => summary is null ? UiFormat.Sg(0) : UiFormat.Sg(summary.TotalStrokesGainedAroundGreen);
    public string AverageDistanceText => summary is null ? UiFormat.PuttingDistance(0, PuttingDistanceUnit) : UiFormat.PuttingDistance(summary.AverageFirstPuttDistance, PuttingDistanceUnit);
    public string AverageApproachDistanceText => summary is null ? UiFormat.WholeMeters(0) : UiFormat.WholeMeters(summary.AverageApproachDistance);
    public string AverageAroundGreenDistanceText => summary is null ? UiFormat.WholeMeters(0) : UiFormat.WholeMeters(YardsToMeters(summary.AverageAroundGreenDistance));
    public string TotalPuttsText => summary?.TotalPutts.ToString() ?? "0";
    public bool ShowExpectedBirdies => TrackPutting && !IsPuttingGame;
    public string ExpectedBirdiesCoverageText => expectedBirdies.GirHoleCount > 0
        ? $"{expectedBirdies.GirHoleCount} GIR-huller med registrerede putts"
        : "Ingen GIR-huller med registrerede putts. Markér GIR ved hulinput for at se sammenligningen.";
    public string GirExpectedPuttsText => expectedBirdies.GirHoleCount == 0 ? "—"
        : expectedBirdies.ExpectedBirdies.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));
    public string GirActualPuttsText => expectedBirdies.GirHoleCount == 0 ? "—" : expectedBirdies.ActualBirdies.ToString();
    public string ExpectedBirdiesDifferenceText => expectedBirdies.GirHoleCount == 0 ? "—" : UiFormat.Sg(expectedBirdies.Difference);
    public string ExpectedPuttsText => ((summary?.TotalPutts ?? 0) + (summary?.TotalStrokesGainedPutting ?? 0))
        .ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));
    public bool ShowTotalSg => new[] { TrackPutting, TrackApproach, TrackAroundGreen }.Count(tracked => tracked) > 1;
    public string TargetPuttsText => IsPuttingGame && round is not null
        ? round.Holes.Sum(hole => hole.ExpectedPutts).ToString("0.0")
        : string.Empty;
    public string TotalApproachShotsText => summary?.TotalApproachShots.ToString() ?? "0";
    public string TotalAroundGreenShotsText => summary?.TotalAroundGreenShots.ToString() ?? "0";
    public string RoundProgressText => round is null
        ? "0 / 0 huller"
        : $"{round.CompletedHoleCount} / {round.ConfiguredHoleCount} huller registreret";
    public string RoundCompletionText => round is null
        ? string.Empty
        : round.EndedEarly ? "Runden blev afsluttet tidligt." : "Runden blev fuldført.";
    public string ThreePuttRateText => summary is null ? "0%" : $"{CalculateThreePuttRate(summary):0}%";
    public string ThreePuttStartingDistanceText
    {
        get
        {
            var holes = round?.Holes.Where(hole => hole.IsCompleted && hole.Putts == 3).ToList();
            return holes is not { Count: > 0 } ? "Ingen 3-putts registreret"
                : $"{UiFormat.PuttingDistance(holes.Average(hole => hole.FirstPuttDistanceMeters), PuttingDistanceUnit)} · {holes.Count} 3-putts";
        }
    }
    public string PuttingOpportunityText
    {
        get
        {
            var weakest = PuttingDistanceBuckets.Where(bucket => bucket.IsLoss)
                .MinBy(bucket => bucket.TotalStrokesGained);
            return weakest is null ? "Ingen afstandsgruppe har tabte slag mod PGA-reference."
                : $"{weakest.Name}: {UiFormat.Sg(weakest.TotalStrokesGained)} SG på {weakest.Attempts} registreringer.";
        }
    }
    public string BestHoleText => FormatPuttingResult(summary?.BestHole);
    public string WorstHoleText => FormatPuttingResult(summary?.WorstHole);
    public string BestApproachHoleText => summary?.BestApproachHole is null ? "-" : $"Hul {summary.BestApproachHole.HoleNumber} ({UiFormat.Sg(summary.BestApproachHole.StrokesGainedApproach)})";
    public string WorstApproachHoleText => summary?.WorstApproachHole is null ? "-" : $"Hul {summary.WorstApproachHole.HoleNumber} ({UiFormat.Sg(summary.WorstApproachHole.StrokesGainedApproach)})";
    public string BestAroundGreenHoleText => summary?.BestAroundGreenHole is null ? "-" : $"Hul {summary.BestAroundGreenHole.HoleNumber} ({UiFormat.Sg(summary.BestAroundGreenHole.StrokesGainedAroundGreen)})";
    public string WorstAroundGreenHoleText => summary?.WorstAroundGreenHole is null ? "-" : $"Hul {summary.WorstAroundGreenHole.HoleNumber} ({UiFormat.Sg(summary.WorstAroundGreenHole.StrokesGainedAroundGreen)})";

    public async Task LoadAsync(string roundId)
    {
        var result = await queryService.LoadAsync(roundId);
        if (result is null)
        {
            return;
        }

        round = result.Round;
        trackingOptions = result.TrackingOptions;
        summary = result.Summary;
        expectedBirdies = ExpectedBirdiesSummary.Calculate(round.Holes);
        HoleResults.Clear();
        foreach (var hole in round.Holes.Where(round.IsTrackedHoleCompleted))
        {
            HoleResults.Add(new HoleResultItemViewModel(HoleResultMapper.ToResult(hole), trackingOptions, PuttingDistanceUnit));
        }

        PuttingDistanceBuckets.Clear();
        foreach (var bucket in BuildRecapDistanceBuckets(summary.PuttingDistanceBuckets))
        {
            PuttingDistanceBuckets.Add(new PuttingDistanceBucketItemViewModel(bucket, PuttingDistanceUnit));
        }

        Analysis.Clear();
        foreach (var note in BuildAnalysis(summary, trackingOptions))
        {
            Analysis.Add(note);
        }

        OnAllPropertiesChanged();
    }

    public static IReadOnlyList<PuttingDistanceBucketSummary> BuildRecapDistanceBuckets(IReadOnlyList<PuttingDistanceBucketSummary> buckets)
    {
        return
        [
            CombineBuckets("Korte putts", buckets.Take(2)),
            buckets[2] with { Name = "Mellemlange putts" },
            buckets[3] with { Name = "Mellemlange putts" },
            buckets[4] with { Name = "Mellemlange putts" },
            buckets[5] with { Name = "Mellemlange putts" },
            buckets[6] with { Name = "Lange putts" }
        ];
    }

    private static PuttingDistanceBucketSummary CombineBuckets(string name, IEnumerable<PuttingDistanceBucketSummary> buckets)
    {
        var items = buckets.ToList();
        return new PuttingDistanceBucketSummary(
            name, items[0].MinimumDistanceMeters, items[^1].MaximumDistanceMeters,
            items.Sum(item => item.Attempts), items.Sum(item => item.TotalPutts),
            items.Sum(item => item.TotalStrokesGained));
    }

    private static double CalculateThreePuttRate(RoundSummary summary)
    {
        var completedHoles = summary.OnePutts + summary.TwoPutts + summary.ThreePuttsOrWorse;
        return completedHoles == 0 ? 0 : summary.ThreePuttsOrWorse / (double)completedHoles * 100;
    }

    private static IEnumerable<string> BuildAnalysis(RoundSummary summary, RoundTrackingOptions trackingOptions)
    {
        if (trackingOptions.TrackApproach)
        {
            if (summary.TotalStrokesGainedApproach > 0)
            {
                yield return "Dine approachslag var bedre end referencebaseline.";
            }
            else if (summary.TotalStrokesGainedApproach >= -2)
            {
                yield return "Dine approachslag var tæt på referencebaseline.";
            }
            else
            {
                yield return "Du tabte især slag på approach.";
            }
        }

        if (trackingOptions.TrackAroundGreen)
        {
            if (summary.TotalStrokesGainedAroundGreen > 0)
            {
                yield return "Dine slag omkring green var bedre end referencebaseline.";
            }
            else if (summary.TotalStrokesGainedAroundGreen >= -2)
            {
                yield return "Dine slag omkring green var tæt på referencebaseline.";
            }
            else
            {
                yield return "Du tabte især slag omkring green.";
            }
        }
    }

    private void OnAllPropertiesChanged()
    {
        OnPropertyChanged(nameof(RoundId));
        OnPropertyChanged(nameof(HasAnalysis));
        OnPropertyChanged(nameof(TrackPutting));
        OnPropertyChanged(nameof(TrackApproach));
        OnPropertyChanged(nameof(TrackAroundGreen));
        OnPropertyChanged(nameof(IsPuttingGame));
        OnPropertyChanged(nameof(ResultTitle));
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(TotalPuttingSgText));
        OnPropertyChanged(nameof(TotalApproachSgText));
        OnPropertyChanged(nameof(TotalAroundGreenSgText));
        OnPropertyChanged(nameof(AverageDistanceText));
        OnPropertyChanged(nameof(AverageApproachDistanceText));
        OnPropertyChanged(nameof(AverageAroundGreenDistanceText));
        OnPropertyChanged(nameof(TotalPuttsText));
        OnPropertyChanged(nameof(ShowExpectedBirdies));
        OnPropertyChanged(nameof(ExpectedBirdiesCoverageText));
        OnPropertyChanged(nameof(GirExpectedPuttsText));
        OnPropertyChanged(nameof(GirActualPuttsText));
        OnPropertyChanged(nameof(ExpectedBirdiesDifferenceText));
        OnPropertyChanged(nameof(ExpectedPuttsText));
        OnPropertyChanged(nameof(ShowTotalSg));
        OnPropertyChanged(nameof(TargetPuttsText));
        OnPropertyChanged(nameof(TotalApproachShotsText));
        OnPropertyChanged(nameof(TotalAroundGreenShotsText));
        OnPropertyChanged(nameof(RoundProgressText));
        OnPropertyChanged(nameof(RoundCompletionText));
        OnPropertyChanged(nameof(ThreePuttRateText));
        OnPropertyChanged(nameof(ThreePuttStartingDistanceText));
        OnPropertyChanged(nameof(PuttingOpportunityText));
        OnPropertyChanged(nameof(BestHoleText));
        OnPropertyChanged(nameof(WorstHoleText));
        OnPropertyChanged(nameof(BestApproachHoleText));
        OnPropertyChanged(nameof(WorstApproachHoleText));
        OnPropertyChanged(nameof(BestAroundGreenHoleText));
        OnPropertyChanged(nameof(WorstAroundGreenHoleText));
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

        return $"Putt {hole.HoleNumber}, {UiFormat.PuttingDistance(hole.FirstPuttDistanceMeters, PuttingDistanceUnit)} ({UiFormat.Sg(hole.StrokesGainedPutting)})";
    }

    private PuttingDistanceUnitPreference PuttingDistanceUnit => distanceUnitSettings.PuttingDistanceUnit;

    private static double YardsToMeters(double distanceYards) => DistanceConversions.YardsToMeters(distanceYards);
}

public sealed class HoleResultItemViewModel
{
    public HoleResultItemViewModel(
        HoleResult hole,
        RoundTrackingOptions trackingOptions,
        PuttingDistanceUnitPreference puttingDistanceUnit = PuttingDistanceUnitPreference.Meters)
    {
        Title = trackingOptions.IsPuttingGame ? $"Putt {hole.HoleNumber}" : $"Hul {hole.HoleNumber}";

        var details = new List<string>();
        var sg = 0d;
        if (trackingOptions.TrackPutting && hole.Putting is not null)
        {
            var distance = UiFormat.PuttingDistance(hole.Putting.FirstPuttDistanceMeters, puttingDistanceUnit);

            details.Add($"{distance} - {hole.Putting.Putts} putts");
            sg += hole.Putting.StrokesGained;
        }

        if (trackingOptions.TrackApproach && hole.Approach is not null)
        {
            details.Add(hole.Approach.Shot is not null
                ? $"Approach {UiFormat.WholeMeters(YardsToMeters(hole.Approach.Shot.StartDistanceToPin))} {ShotLieLabels.Format(hole.Approach.Shot.StartLie).ToLowerInvariant()}"
                : $"{UiFormat.WholeMeters(hole.Approach.DistanceMeters)} - {hole.Approach.Shots} approachslag");
            sg += hole.Approach.StrokesGained;
        }

        if (trackingOptions.TrackAroundGreen && hole.AroundGreen is not null)
        {
            details.Add($"Omkring green {UiFormat.WholeMeters(YardsToMeters(hole.AroundGreen.StartDistanceYards))} {ShotLieLabels.Format(hole.AroundGreen.StartLie).ToLowerInvariant()}");
            sg += hole.AroundGreen.StrokesGained;
        }

        Detail = string.Join(" | ", details);
        StrokesGainedText = UiFormat.Sg(sg);
    }

    public string Title { get; }
    public string Detail { get; }
    public string StrokesGainedText { get; }

    private static double YardsToMeters(double distanceYards) => DistanceConversions.YardsToMeters(distanceYards);
}

public sealed class PuttingDistanceBucketItemViewModel
{
    public PuttingDistanceBucketItemViewModel(
        PuttingDistanceBucketSummary bucket,
        PuttingDistanceUnitPreference puttingDistanceUnit = PuttingDistanceUnitPreference.Meters)
    {
        Name = $"{bucket.Name} ({FormatRange(bucket, puttingDistanceUnit)})";
        RangeText = "F\u00F8rste putt-afstand";
        DetailText = $"{bucket.Attempts} f\u00F8rste putts | {bucket.TotalPutts} putts";
        StrokesGainedText = bucket.Attempts == 0 ? "—" : UiFormat.Sg(bucket.TotalStrokesGained);
        IsLoss = bucket.Attempts > 0 && bucket.TotalStrokesGained < -0.005;
        TotalStrokesGained = bucket.TotalStrokesGained;
        Attempts = bucket.Attempts;
        IsGain = bucket.Attempts > 0 && bucket.TotalStrokesGained >= 0.005;
        OutcomeText = bucket.Attempts == 0 ? "Ingen data"
            : IsLoss ? "SG · tabte slag"
            : bucket.TotalStrokesGained >= 0.005 ? "SG · vundne slag" : "SG · på niveau";
    }

    public string Name { get; }
    public string RangeText { get; }
    public string DetailText { get; }
    public string StrokesGainedText { get; }

    public bool IsLoss { get; }
    public double TotalStrokesGained { get; }
    public int Attempts { get; }
    public bool IsGain { get; }
    public string OutcomeText { get; }

    private static string FormatRange(PuttingDistanceBucketSummary bucket, PuttingDistanceUnitPreference puttingDistanceUnit)
    {
        if (bucket.MinimumDistanceMeters is null && bucket.MaximumDistanceMeters is not null)
        {
            return $"< {UiFormat.PuttingDistance(bucket.MaximumDistanceMeters.Value, puttingDistanceUnit)}";
        }

        if (bucket.MinimumDistanceMeters is not null && bucket.MaximumDistanceMeters is not null)
        {
            return $"{UiFormat.PuttingDistance(bucket.MinimumDistanceMeters.Value, puttingDistanceUnit)} - {UiFormat.PuttingDistance(bucket.MaximumDistanceMeters.Value, puttingDistanceUnit)}";
        }

        if (bucket.MinimumDistanceMeters is not null)
        {
            return $"> {UiFormat.PuttingDistance(bucket.MinimumDistanceMeters.Value, puttingDistanceUnit)}";
        }

        return string.Empty;
    }
}
