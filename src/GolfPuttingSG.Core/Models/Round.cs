namespace GolfPuttingSG.Core.Models;

public sealed record Round(
    string Id,
    DateTime Date,
    IReadOnlyList<HolePuttingData> Holes)
{
    public static Round Empty() => new(
        Guid.NewGuid().ToString("N"),
        DateTime.Today,
        Enumerable.Range(1, 18)
            .Select(hole => GolfPuttingSG.Core.StrokesGainedCalculator.BuildHole(hole, 0, 0))
            .ToList());
}
