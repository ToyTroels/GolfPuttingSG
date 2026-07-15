using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public enum HistorySgCategory
{
    Total,
    Putting,
    Approach,
    AroundGreen
}

public sealed class RoundListItemViewModel : ViewModelBase
{
    private readonly RoundSummary summary;
    private readonly IDistanceUnitSettings distanceUnitSettings;
    private readonly RoundTrackingOptions trackingOptions;
    private HistorySgCategory historyCategory = HistorySgCategory.Total;
    private double historyComparisonAverage;
    private int historyComparisonCount;

    public RoundListItemViewModel(Round round, IDistanceUnitSettings? distanceUnitSettings = null)
    {
        Round = round;
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
        summary = StrokesGainedCalculator.CalculateRoundSummary(round);
        trackingOptions = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        PuttingSg = trackingOptions.TrackPutting ? summary.TotalStrokesGainedPutting : 0;
        ApproachSg = trackingOptions.TrackApproach ? summary.TotalStrokesGainedApproach : 0;
        AroundGreenSg = trackingOptions.TrackAroundGreen ? summary.TotalStrokesGainedAroundGreen : 0;
        TotalSg = PuttingSg + ApproachSg + AroundGreenSg;
        TotalPutts = summary.TotalPutts;
        ThreePuttsOrWorse = summary.ThreePuttsOrWorse;
    }

    public Round Round { get; }
    public string Id => Round.Id;
    public bool TracksPutting => trackingOptions.TrackPutting;
    public bool TracksApproach => trackingOptions.TrackApproach;
    public bool TracksAroundGreen => trackingOptions.TrackAroundGreen;
    public bool IsPuttingGame => trackingOptions.IsPuttingGame;
    public bool IsBenchmark => IsPuttingGame &&
        (string.Equals(Round.GameInfo?.Type, "PuttingBenchmark", StringComparison.Ordinal) ||
            IsLegacyBenchmarkMode(trackingOptions.PuttingGameMode));
    public string HistoryTypeText => IsBenchmark
        ? "Benchmark"
        : IsPuttingGame ? "Putting-spil" : "Runde";
    public string Date => IsPuttingGame
        ? $"{UiFormat.Date(Round.Date)} - {PuttingGameTitle}"
        : UiFormat.Date(Round.Date);
    public double PuttingSg { get; }
    public double ApproachSg { get; }
    public double AroundGreenSg { get; }
    public double TotalSg { get; }
    public string TotalSgText => UiFormat.Sg(TotalSg);
    public string DisplaySgText => UiFormat.Sg(GetSg(historyCategory));
    public string DisplaySgCaption => GetHistoryCategoryLabel(historyCategory);
    public int TotalPutts { get; }
    public int ThreePuttsOrWorse { get; }
    public bool HasComparisonText => historyComparisonCount > 1;
    public string ComparisonText => HasComparisonText
        ? $"Afvigelse fra filtersnit: {UiFormat.Sg(GetSg(historyCategory) - historyComparisonAverage)}"
        : string.Empty;

    public string DetailText
    {
        get
        {
            if (IsPuttingGame)
            {
                var puttingGameParts = new List<string> { PuttingGameTitle };
                var benchmarkDetail = FormatBenchmarkDetail(Round.GameInfo);
                if (!string.IsNullOrWhiteSpace(benchmarkDetail))
                {
                    puttingGameParts.Add(benchmarkDetail);
                }

                puttingGameParts.Add($"{Round.Holes.Count} putts");
                puttingGameParts.Add($"{TotalPutts} slag");
                return string.Join(" | ", puttingGameParts);
            }

            var parts = new List<string>
            {
                $"{Round.CompletedHoleCount}/{Round.ConfiguredHoleCount} huller",
                Round.EndedEarly ? "afsluttet tidligt" : "fuldf\u00f8rt"
            };
            if (trackingOptions.TrackPutting)
            {
                parts.Add($"{TotalPutts} putts");
            }

            if (trackingOptions.TrackApproach)
            {
                parts.Add($"{summary.TotalApproachShots} indspil");
            }

            if (trackingOptions.TrackAroundGreen)
            {
                parts.Add($"{summary.TotalAroundGreenShots} slag omkring green");
            }

            return string.Join(" | ", parts);
        }
    }

    private string PuttingGameTitle => PuttingGame.GetTitle(Round.GameInfo, trackingOptions.PuttingGameMode);

    public double GetSg(HistorySgCategory category)
    {
        return category switch
        {
            HistorySgCategory.Putting => PuttingSg,
            HistorySgCategory.Approach => ApproachSg,
            HistorySgCategory.AroundGreen => AroundGreenSg,
            _ => TotalSg
        };
    }

    public bool TracksCategory(HistorySgCategory category)
    {
        return category switch
        {
            HistorySgCategory.Putting => TracksPutting,
            HistorySgCategory.Approach => TracksApproach,
            HistorySgCategory.AroundGreen => TracksAroundGreen,
            _ => true
        };
    }

    public void SetHistoryDisplay(HistorySgCategory category, double comparisonAverage, int comparisonCount)
    {
        if (historyCategory == category &&
            Math.Abs(historyComparisonAverage - comparisonAverage) < 0.0001 &&
            historyComparisonCount == comparisonCount)
        {
            return;
        }

        historyCategory = category;
        historyComparisonAverage = comparisonAverage;
        historyComparisonCount = comparisonCount;
        OnPropertyChanged(nameof(DisplaySgText));
        OnPropertyChanged(nameof(DisplaySgCaption));
        OnPropertyChanged(nameof(ComparisonText));
        OnPropertyChanged(nameof(HasComparisonText));
    }

    private static bool IsLegacyBenchmarkMode(string? mode) => mode is
        PuttingGame.ShortBenchmark or
        PuttingGame.NormalBenchmark or
        PuttingGame.ThoroughBenchmark or
        PuttingGame.ShortLadderBenchmark or
        PuttingGame.NormalLadderBenchmark or
        PuttingGame.ThoroughLadderBenchmark;

    private string FormatBenchmarkDetail(RoundGameInfo? gameInfo)
    {
        if (gameInfo?.BenchmarkType != PuttingBenchmarkType.Ladder ||
            gameInfo.StartDistanceMeters is null ||
            gameInfo.EndDistanceMeters is null ||
            gameInfo.PuttsPerDistance is null)
        {
            return string.Empty;
        }

        var stepText = gameInfo.DistanceStepMeters is null
            ? gameInfo.DistanceStepDescription
            : $"{UiFormat.PuttingDistance(gameInfo.DistanceStepMeters.Value, PuttingDistanceUnit)} trin";

        var parts = new List<string>
        {
            $"{UiFormat.PuttingDistance(gameInfo.StartDistanceMeters.Value, PuttingDistanceUnit)}-{UiFormat.PuttingDistance(gameInfo.EndDistanceMeters.Value, PuttingDistanceUnit)}"
        };
        if (!string.IsNullOrWhiteSpace(stepText))
        {
            parts.Add(stepText);
        }

        parts.Add($"{gameInfo.PuttsPerDistance} pr. afstand");
        return string.Join(", ", parts);
    }

    private PuttingDistanceUnitPreference PuttingDistanceUnit => distanceUnitSettings.PuttingDistanceUnit;

    private static string GetHistoryCategoryLabel(HistorySgCategory category)
    {
        return category switch
        {
            HistorySgCategory.Putting => "SG putting",
            HistorySgCategory.Approach => "SG approach",
            HistorySgCategory.AroundGreen => "SG omkring green",
            _ => "SG total"
        };
    }
}
