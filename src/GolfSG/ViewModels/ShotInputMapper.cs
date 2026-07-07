using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.ViewModels;

public static class ShotInputMapper
{
    public static GolfShot BuildApproachShot(ApproachShotInput input)
    {
        return new GolfShot
        {
            HoleNumber = input.HoleNumber,
            Par = input.Par,
            ShotNumber = input.IsTeeShot ? 1 : 2,
            IsTeeShot = input.IsTeeShot,
            StartDistanceToPin = DistanceConversions.MetersToYards(input.StartDistanceMeters),
            StartDistanceUnit = DistanceUnit.Yards,
            StartLie = input.StartLie,
            StartDistanceToGreenEdgeYards = DistanceConversions.MetersToYards(input.StartDistanceMeters),
            EndDistanceToPin = input.Holed ? 0 : ToShotDistance(input.EndDistanceMeters, input.EndLie),
            EndDistanceUnit = input.EndLie == ShotLie.Green ? DistanceUnit.Feet : DistanceUnit.Yards,
            EndLie = input.EndLie,
            EndDistanceToGreenEdgeYards = 0,
            PenaltyStrokes = input.PenaltyStrokes,
            Holed = input.Holed || input.EndLie == ShotLie.Holed
        };
    }

    public static GolfShot BuildAroundGreenShot(AroundGreenShotInput input)
    {
        return new GolfShot
        {
            HoleNumber = input.HoleNumber,
            ShotNumber = input.ShotNumber,
            StartDistanceToPin = DistanceConversions.MetersToYards(input.StartDistanceMeters),
            StartDistanceUnit = DistanceUnit.Yards,
            StartLie = input.StartLie,
            StartDistanceToGreenEdgeYards = Math.Min(DistanceConversions.MetersToYards(input.StartDistanceMeters), 30),
            EndDistanceToPin = input.Holed ? 0 : ToShotDistance(input.EndDistanceMeters, input.EndLie),
            EndDistanceUnit = input.EndLie == ShotLie.Green ? DistanceUnit.Feet : DistanceUnit.Yards,
            EndLie = input.EndLie,
            PenaltyStrokes = input.PenaltyStrokes,
            Holed = input.Holed || input.EndLie == ShotLie.Holed
        };
    }

    private static double ToShotDistance(double distanceMeters, ShotLie endLie)
    {
        return endLie == ShotLie.Green
            ? DistanceConversions.MetersToFeet(distanceMeters)
            : DistanceConversions.MetersToYards(distanceMeters);
    }
}

public sealed record ApproachShotInput(
    int HoleNumber,
    int Par,
    bool IsTeeShot,
    double StartDistanceMeters,
    ShotLie StartLie,
    double EndDistanceMeters,
    ShotLie EndLie,
    int PenaltyStrokes,
    bool Holed);

public sealed record AroundGreenShotInput(
    int HoleNumber,
    int ShotNumber,
    double StartDistanceMeters,
    ShotLie StartLie,
    double EndDistanceMeters,
    ShotLie EndLie,
    int PenaltyStrokes,
    bool Holed);
