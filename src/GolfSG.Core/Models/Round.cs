namespace GolfSG.Core.Models;

public sealed record Round(
    string Id,
    DateTime Date,
    IReadOnlyList<HolePuttingData> Holes,
    RoundTrackingOptions? TrackingOptions = null,
    int ConfiguredHoleCount = 18,
    bool EndedEarly = false)
{
    public int CompletedHoleCount => Holes.Count(IsTrackedHoleCompleted);

    public bool IsCompletedNormally => !EndedEarly && CompletedHoleCount >= ConfiguredHoleCount;

    public static Round Empty() => new(
        Guid.NewGuid().ToString("N"),
        DateTime.Today,
        Enumerable.Range(1, 18)
            .Select(hole => GolfSG.Core.StrokesGainedCalculator.BuildHole(hole, 0, 0))
            .ToList(),
        RoundTrackingOptions.PuttingOnly,
        18);

    public bool IsTrackedHoleCompleted(HolePuttingData hole)
    {
        var options = TrackingOptions ?? RoundTrackingOptions.PuttingOnly;
        return (options.TrackPutting && hole.IsCompleted) ||
            (options.TrackApproach && hole.IsApproachCompleted) ||
            (options.TrackAroundGreen && hole.IsAroundGreenCompleted);
    }
}
