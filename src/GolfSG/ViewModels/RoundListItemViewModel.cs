using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.ViewModels;

public sealed class RoundListItemViewModel
{
    private readonly RoundSummary summary;
    private readonly RoundTrackingOptions trackingOptions;

    public RoundListItemViewModel(Round round)
    {
        Round = round;
        summary = StrokesGainedCalculator.CalculateRoundSummary(round);
        trackingOptions = round.TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        TotalSg = CalculateTrackedTotal();
        TotalPutts = summary.TotalPutts;
        ThreePuttsOrWorse = summary.ThreePuttsOrWorse;
    }

    public Round Round { get; }
    public string Id => Round.Id;
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
    public double TotalSg { get; }
    public string TotalSgText => UiFormat.Sg(TotalSg);
    public int TotalPutts { get; }
    public int ThreePuttsOrWorse { get; }

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
                Round.EndedEarly ? "afsluttet tidligt" : "fuldført"
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

    private static bool IsLegacyBenchmarkMode(string? mode) => mode is
        PuttingGame.ShortBenchmark or
        PuttingGame.NormalBenchmark or
        PuttingGame.ThoroughBenchmark or
        PuttingGame.ShortLadderBenchmark or
        PuttingGame.NormalLadderBenchmark or
        PuttingGame.ThoroughLadderBenchmark;

    private static string FormatBenchmarkDetail(RoundGameInfo? gameInfo)
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
            : $"{UiFormat.Meters(gameInfo.DistanceStepMeters.Value)} trin";

        var parts = new List<string>
        {
            $"{UiFormat.Meters(gameInfo.StartDistanceMeters.Value)}-{UiFormat.Meters(gameInfo.EndDistanceMeters.Value)}"
        };
        if (!string.IsNullOrWhiteSpace(stepText))
        {
            parts.Add(stepText);
        }

        parts.Add($"{gameInfo.PuttsPerDistance} pr. afstand");
        return string.Join(", ", parts);
    }

    private double CalculateTrackedTotal()
    {
        return (trackingOptions.TrackPutting ? summary.TotalStrokesGainedPutting : 0) +
            (trackingOptions.TrackApproach ? summary.TotalStrokesGainedApproach : 0) +
            (trackingOptions.TrackAroundGreen ? summary.TotalStrokesGainedAroundGreen : 0);
    }
}
