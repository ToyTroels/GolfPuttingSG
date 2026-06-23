using GolfPuttingSG.Core;
using GolfPuttingSG.Core.Models;

namespace GolfPuttingSG.ViewModels;

public sealed class RoundListItemViewModel
{
    public RoundListItemViewModel(Round round)
    {
        Round = round;
        var summary = StrokesGainedCalculator.CalculateRoundSummary(round);
        TotalSg = summary.TotalStrokesGainedPutting;
        TotalPutts = summary.TotalPutts;
        ThreePuttsOrWorse = summary.ThreePuttsOrWorse;
    }

    public Round Round { get; }
    public string Id => Round.Id;
    public string Date => UiFormat.Date(Round.Date);
    public double TotalSg { get; }
    public string TotalSgText => UiFormat.Sg(TotalSg);
    public int TotalPutts { get; }
    public int ThreePuttsOrWorse { get; }
    public string DetailText => $"{TotalPutts} putts · {ThreePuttsOrWorse} 3-putts+";
}
