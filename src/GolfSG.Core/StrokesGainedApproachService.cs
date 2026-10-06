using GolfSG.Core.Models;

namespace GolfSG.Core;

public sealed class StrokesGainedApproachService : IStrokesGainedApproachService
{
    private static readonly IReadOnlyDictionary<ShotLie, ApproachReferencePoint[]> ApproachBaseline =
        new Dictionary<ShotLie, ApproachReferencePoint[]>
        {
            [ShotLie.Tee] =
            [
                new(100, 2.92),
                new(125, 2.95),
                new(150, 3.00),
                new(175, 3.12),
                new(200, 3.26),
                new(225, 3.45),
                new(250, 3.65)
            ],
            [ShotLie.Fairway] =
            [
                new(30, 2.51),
                new(40, 2.61),
                new(50, 2.66),
                new(60, 2.70),
                new(75, 2.75),
                new(100, 2.80),
                new(125, 2.85),
                new(150, 2.95),
                new(175, 3.08),
                new(200, 3.22),
                new(225, 3.38),
                new(250, 3.55)
            ],
            [ShotLie.Rough] =
            [
                new(30, 2.70),
                new(40, 2.80),
                new(50, 2.87),
                new(60, 2.92),
                new(75, 2.98),
                new(100, 3.00),
                new(125, 3.08),
                new(150, 3.15),
                new(175, 3.28),
                new(200, 3.45),
                new(225, 3.65),
                new(250, 3.85)
            ],
            [ShotLie.Sand] =
            [
                new(30, 2.65),
                new(40, 2.86),
                new(50, 3.00),
                new(75, 3.15),
                new(100, 3.25),
                new(125, 3.40),
                new(150, 3.60),
                new(175, 3.85),
                new(200, 4.05)
            ],
            [ShotLie.Recovery] =
            [
                new(30, 3.15),
                new(40, 3.30),
                new(50, 3.45),
                new(75, 3.60),
                new(100, 3.80),
                new(125, 4.00),
                new(150, 4.20),
                new(175, 4.40),
                new(200, 4.65)
            ]
        };

    private readonly IStrokesGainedPuttingService puttingService;
    private readonly IStrokesGainedAroundGreenService aroundGreenService;

    public StrokesGainedApproachService(
        IStrokesGainedPuttingService puttingService,
        IStrokesGainedAroundGreenService aroundGreenService)
    {
        this.puttingService = puttingService;
        this.aroundGreenService = aroundGreenService;
    }

    /// <summary>
    /// Par 3 tee shots are SG:APP. Par 4 and par 5 tee shots are excluded because they belong
    /// to SG Off-the-Tee, and shots inside the around-the-green zone are excluded because the
    /// app already has SG Around-the-Green.
    /// </summary>
    public bool IsApproachShot(GolfShot shot)
    {
        if (shot is null)
        {
            return false;
        }

        if (shot.StartLie is ShotLie.Green or ShotLie.Holed)
        {
            return false;
        }

        if (shot.Par == 3 && shot.IsTeeShot)
        {
            return true;
        }

        if ((shot.Par == 4 || shot.Par == 5) && !shot.IsTeeShot)
        {
            return shot.StartDistanceToGreenEdgeYards > 30;
        }

        return false;
    }

    /// <summary>
    /// SG Approach uses the same strokes-gained formula as other categories. Putting finish
    /// values are delegated to SG Putting, and around-the-green finish values are delegated to SG:ARG.
    /// </summary>
    public double CalculateShotSgApproach(GolfShot shot)
    {
        if (!IsApproachShot(shot))
        {
            return 0;
        }

        ValidateShot(shot);

        var startExpected = GetApproachExpectedStrokes(
            shot.StartDistanceToPin,
            shot.StartDistanceUnit,
            shot.StartLie);

        var finishExpected = GetFinishExpectedStrokes(shot);

        return startExpected - finishExpected - 1 - shot.PenaltyStrokes;
    }

    public double CalculateRoundSgApproach(IEnumerable<GolfShot> shots)
    {
        ArgumentNullException.ThrowIfNull(shots);

        return shots.Sum(CalculateShotSgApproach);
    }

    /// <summary>
    /// Uses project-maintained approximate approach benchmark values. They are not official or endorsed
    /// tour statistics.
    /// </summary>
    public double GetApproachExpectedStrokes(double distance, DistanceUnit unit, ShotLie lie)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        // Short-cut grass uses the fairway approach baseline beyond the around-green range.
        if (lie == ShotLie.FairwayCut)
        {
            lie = ShotLie.Fairway;
        }

        if (!ApproachBaseline.TryGetValue(lie, out var baseline))
        {
            throw new ArgumentOutOfRangeException(nameof(lie), lie, "Approach expected strokes require a tee, fairway, rough, sand, or recovery lie.");
        }

        var distanceYards = unit switch
        {
            DistanceUnit.Feet => DistanceConversions.FeetToYards(distance),
            DistanceUnit.Yards => distance,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported distance unit.")
        };

        return InterpolateExpectedStrokes(distanceYards, baseline);
    }

    private double GetFinishExpectedStrokes(GolfShot shot)
    {
        if (shot.Holed || shot.EndLie == ShotLie.Holed)
        {
            return 0;
        }

        if (shot.EndLie == ShotLie.Green)
        {
            return puttingService.GetExpectedPutts(shot.EndDistanceToPin, shot.EndDistanceUnit);
        }

        if (ToYards(shot.EndDistanceToPin, shot.EndDistanceUnit) <= 30 && shot.EndLie != ShotLie.Tee)
        {
            return aroundGreenService.GetAroundGreenExpectedStrokes(
                shot.EndDistanceToPin,
                shot.EndDistanceUnit,
                shot.EndLie == ShotLie.Fairway ? ShotLie.FairwayCut : shot.EndLie);
        }

        return GetApproachExpectedStrokes(
            shot.EndDistanceToPin,
            shot.EndDistanceUnit,
            shot.EndLie);
    }

    private static double ToYards(double distance, DistanceUnit unit)
    {
        return unit switch
        {
            DistanceUnit.Feet => DistanceConversions.FeetToYards(distance),
            DistanceUnit.Yards => distance,
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unsupported distance unit.")
        };
    }

    private static void ValidateShot(GolfShot shot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shot.StartDistanceToPin);
        ArgumentOutOfRangeException.ThrowIfNegative(shot.StartDistanceToGreenEdgeYards);
        ArgumentOutOfRangeException.ThrowIfNegative(shot.EndDistanceToPin);
        ArgumentOutOfRangeException.ThrowIfNegative(shot.EndDistanceToGreenEdgeYards);
        ArgumentOutOfRangeException.ThrowIfNegative(shot.PenaltyStrokes);

        if (shot.Par is < 3 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(shot), shot.Par, "Par must be 3, 4, or 5.");
        }

        if (shot.StartLie == ShotLie.Holed)
        {
            throw new ArgumentException("Start lie cannot be holed.", nameof(shot));
        }

        if (shot.StartLie == ShotLie.Tee && !shot.IsTeeShot)
        {
            throw new ArgumentException("Tee lie is only valid for tee shots.", nameof(shot));
        }
    }

    private static double InterpolateExpectedStrokes(
        double distanceYards,
        IReadOnlyList<ApproachReferencePoint> baseline)
    {
        if (distanceYards <= baseline[0].DistanceYards)
        {
            return baseline[0].ExpectedStrokes;
        }

        if (distanceYards >= baseline[^1].DistanceYards)
        {
            return baseline[^1].ExpectedStrokes;
        }

        for (var i = 0; i < baseline.Count - 1; i++)
        {
            var lower = baseline[i];
            var upper = baseline[i + 1];

            if (distanceYards < lower.DistanceYards || distanceYards > upper.DistanceYards)
            {
                continue;
            }

            var ratio = (distanceYards - lower.DistanceYards) / (upper.DistanceYards - lower.DistanceYards);
            return lower.ExpectedStrokes + ratio * (upper.ExpectedStrokes - lower.ExpectedStrokes);
        }

        return baseline[^1].ExpectedStrokes;
    }

    private sealed record ApproachReferencePoint(double DistanceYards, double ExpectedStrokes);
}
