namespace GolfSG.Core.Models;

public sealed record Round(
    string Id,
    DateTime Date,
    IReadOnlyList<HolePuttingData> Holes,
    RoundTrackingOptions? TrackingOptions = null)
{
    public static Round Empty() => new(
        Guid.NewGuid().ToString("N"),
        DateTime.Today,
        Enumerable.Range(1, 18)
            .Select(hole => GolfSG.Core.StrokesGainedCalculator.BuildHole(hole, 0, 0))
            .ToList(),
        RoundTrackingOptions.PuttingOnly);
}
