namespace GolfPuttingSG.Core.Models;

public sealed record RoundTrackingOptions(
    bool TrackPutting,
    bool TrackApproach,
    bool IsPuttingGame = false,
    string? PuttingGameMode = null)
{
    public static RoundTrackingOptions PuttingOnly { get; } = new(true, false);
    public static RoundTrackingOptions PuttingGame { get; } = new(true, false, true, GolfPuttingSG.Core.PuttingGame.LadderMode);
    public static RoundTrackingOptions TourRoundGame { get; } = new(true, false, true, GolfPuttingSG.Core.PuttingGame.TourRoundMode);
}
