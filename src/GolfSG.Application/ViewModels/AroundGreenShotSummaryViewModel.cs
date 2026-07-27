using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public sealed record AroundGreenShotSummaryViewModel(
    string Title,
    string StartText,
    string EndText,
    string PenaltyText)
{
    public static AroundGreenShotSummaryViewModel FromShot(
        GolfShot shot,
        int shotNumber,
        PuttingDistanceUnitPreference puttingDistanceUnit)
    {
        var start = $"{UiFormat.WholeMeters(ToMeters(shot.StartDistanceToPin, shot.StartDistanceUnit))} {ShotLieLabels.Format(shot.StartLie).ToLowerInvariant()}";
        var endDistanceMeters = ToMeters(shot.EndDistanceToPin, shot.EndDistanceUnit);
        var endDistance = shot.EndLie == ShotLie.Green
            ? UiFormat.PuttingDistance(endDistanceMeters, puttingDistanceUnit)
            : UiFormat.Meters(endDistanceMeters);
        var end = shot.Holed || shot.EndLie == ShotLie.Holed
            ? "I hul"
            : $"{endDistance} {ShotLieLabels.Format(shot.EndLie).ToLowerInvariant()}";
        var penalties = shot.PenaltyStrokes == 1
            ? "1 strafslag"
            : $"{shot.PenaltyStrokes} strafslag";

        return new AroundGreenShotSummaryViewModel(
            $"Slag omkring green {shotNumber}",
            $"Start: {start}",
            $"Slut: {end}",
            penalties);
    }

    private static double ToMeters(double distance, DistanceUnit unit) => DistanceConversions.ToMeters(distance, unit);
}
