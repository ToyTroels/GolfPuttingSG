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
                return $"{PuttingGameTitle} | {Round.Holes.Count} putts | {TotalPutts} slag";
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

    private double CalculateTrackedTotal()
    {
        return (trackingOptions.TrackPutting ? summary.TotalStrokesGainedPutting : 0) +
            (trackingOptions.TrackApproach ? summary.TotalStrokesGainedApproach : 0) +
            (trackingOptions.TrackAroundGreen ? summary.TotalStrokesGainedAroundGreen : 0);
    }
}
