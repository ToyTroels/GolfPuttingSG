using GolfSG.Core.Models;

namespace GolfSG.Application.ViewModels;

public static class HoleCarryForwardService
{
    private const double DistanceEqualityToleranceMeters = 0.05;

    public static HoleCarryForwardResult FromApproach(ApproachCarryForwardRequest request)
    {
        if (!request.TrackApproach ||
            request.ApproachFinishedHoled ||
            request.EndDistanceMeters <= 0)
        {
            return HoleCarryForwardResult.Empty;
        }

        if (request.EndLie == ShotLie.Green)
        {
            return !request.TrackPutting || request.HasManualPuttingDistanceOverride
                ? HoleCarryForwardResult.Empty
                : HoleCarryForwardResult.CarryPuttingDistance(
                    Math.Min(request.EndDistanceMeters, request.MaxPuttingDistanceMeters),
                    HoleCarryForwardPuttingSource.Approach);
        }

        if (!request.TrackAroundGreen)
        {
            return HoleCarryForwardResult.Empty;
        }

        var aroundGreenDistanceMeters = CanReplaceCarriedDistance(
                request.CurrentAroundGreenStartDistanceMeters,
                request.PreviousCarriedAroundGreenStartDistanceMeters)
            ? Math.Min(request.EndDistanceMeters, request.MaxAroundGreenDistanceMeters)
            : null as double?;

        var aroundGreenLieText = ShouldReplaceCarriedLie(
                request.CurrentAroundGreenStartLieText,
                request.PreviousCarriedAroundGreenStartLieText)
            ? ShotLieLabels.FormatAroundGreenStart(request.EndLie)
            : null;

        return new HoleCarryForwardResult(
            PuttingDistanceMeters: null,
            PuttingSource: HoleCarryForwardPuttingSource.None,
            AroundGreenStartDistanceMeters: aroundGreenDistanceMeters,
            AroundGreenStartLieText: aroundGreenLieText);
    }

    public static HoleCarryForwardResult FromAroundGreen(AroundGreenCarryForwardRequest request)
    {
        if (!request.TrackAroundGreen ||
            !request.TrackPutting ||
            request.AroundGreenFinishedHoled ||
            request.EndLie != ShotLie.Green ||
            request.EndDistanceMeters <= 0 ||
            request.HasManualPuttingDistanceOverride)
        {
            return HoleCarryForwardResult.Empty;
        }

        return HoleCarryForwardResult.CarryPuttingDistance(
            Math.Min(request.EndDistanceMeters, request.MaxPuttingDistanceMeters),
            HoleCarryForwardPuttingSource.AroundGreen);
    }

    public static bool ShouldClearCarriedDistance(double currentDistanceMeters, double? carriedDistanceMeters)
    {
        return carriedDistanceMeters is not null &&
            !AreDistancesEqual(currentDistanceMeters, carriedDistanceMeters.Value);
    }

    public static bool ShouldClearCarriedLie(string currentLieText, string? carriedLieText)
    {
        return !string.IsNullOrWhiteSpace(carriedLieText) &&
            !string.Equals(currentLieText, carriedLieText, StringComparison.Ordinal);
    }

    private static bool CanReplaceCarriedDistance(double currentDistanceMeters, double? previousCarriedDistanceMeters)
    {
        return currentDistanceMeters <= 0 ||
            (previousCarriedDistanceMeters is not null &&
                AreDistancesEqual(currentDistanceMeters, previousCarriedDistanceMeters.Value));
    }

    private static bool ShouldReplaceCarriedLie(string currentLieText, string? previousCarriedLieText)
    {
        return string.IsNullOrWhiteSpace(previousCarriedLieText) ||
            string.Equals(currentLieText, previousCarriedLieText, StringComparison.Ordinal);
    }

    private static bool AreDistancesEqual(double first, double second) =>
        Math.Abs(first - second) < DistanceEqualityToleranceMeters;
}

public sealed record ApproachCarryForwardRequest(
    bool TrackApproach,
    bool TrackPutting,
    bool TrackAroundGreen,
    bool ApproachFinishedHoled,
    double EndDistanceMeters,
    ShotLie EndLie,
    bool HasManualPuttingDistanceOverride,
    double CurrentAroundGreenStartDistanceMeters,
    double? PreviousCarriedAroundGreenStartDistanceMeters,
    string CurrentAroundGreenStartLieText,
    string? PreviousCarriedAroundGreenStartLieText,
    double MaxPuttingDistanceMeters,
    double MaxAroundGreenDistanceMeters);

public sealed record AroundGreenCarryForwardRequest(
    bool TrackAroundGreen,
    bool TrackPutting,
    bool AroundGreenFinishedHoled,
    double EndDistanceMeters,
    ShotLie EndLie,
    bool HasManualPuttingDistanceOverride,
    double MaxPuttingDistanceMeters);

public sealed record HoleCarryForwardResult(
    double? PuttingDistanceMeters,
    HoleCarryForwardPuttingSource PuttingSource,
    double? AroundGreenStartDistanceMeters,
    string? AroundGreenStartLieText)
{
    public static HoleCarryForwardResult Empty { get; } = new(
        PuttingDistanceMeters: null,
        PuttingSource: HoleCarryForwardPuttingSource.None,
        AroundGreenStartDistanceMeters: null,
        AroundGreenStartLieText: null);

    public static HoleCarryForwardResult CarryPuttingDistance(
        double distanceMeters,
        HoleCarryForwardPuttingSource source) =>
        new(
            PuttingDistanceMeters: distanceMeters,
            PuttingSource: source,
            AroundGreenStartDistanceMeters: null,
            AroundGreenStartLieText: null);
}

public enum HoleCarryForwardPuttingSource
{
    None,
    Approach,
    AroundGreen
}
