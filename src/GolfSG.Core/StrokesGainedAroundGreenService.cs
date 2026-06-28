using GolfSG.Core.Models;

namespace GolfSG.Core;

public sealed class StrokesGainedAroundGreenService : IStrokesGainedAroundGreenService
{
    private const double FeetPerYard = 3;

    private static readonly IReadOnlyDictionary<ShotLie, AroundGreenBaselinePoint[]> AroundGreenBaseline =
        new Dictionary<ShotLie, AroundGreenBaselinePoint[]>
        {
            [ShotLie.FairwayCut] =
            [
                new(3, 1.98),
                new(5, 2.07),
                new(10, 2.14),
                new(15, 2.28),
                new(20, 2.38),
                new(25, 2.45),
                new(30, 2.51),
                new(35, 2.56),
                new(40, 2.61),
                new(45, 2.64),
                new(50, 2.66)
            ],
            [ShotLie.Rough] =
            [
                new(5, 2.15),
                new(10, 2.31),
                new(15, 2.49),
                new(20, 2.58),
                new(25, 2.66),
                new(30, 2.70),
                new(35, 2.75),
                new(40, 2.80),
                new(45, 2.84),
                new(50, 2.87)
            ],
            [ShotLie.Sand] =
            [
                new(5, 2.37),
                new(10, 2.45),
                new(15, 2.49),
                new(20, 2.53),
                new(25, 2.58),
                new(30, 2.65),
                new(35, 2.74),
                new(40, 2.86),
                new(45, 2.95),
                new(50, 3.00)
            ],
            [ShotLie.Recovery] =
            [
                new(5, 2.60),
                new(10, 2.75),
                new(15, 2.90),
                new(20, 3.00),
                new(25, 3.08),
                new(30, 3.15),
                new(35, 3.22),
                new(40, 3.30),
                new(45, 3.38),
                new(50, 3.45)
            ]
        };

    public static IReadOnlyList<AroundGreenReferencePoint> Reference { get; } =
        AroundGreenBaseline
            .SelectMany(lieBaseline => lieBaseline.Value
                .Select(point => new AroundGreenReferencePoint(
                    lieBaseline.Key,
                    point.DistanceYards,
                    point.ExpectedStrokes)))
            .ToList();

    private readonly IStrokesGainedPuttingService puttingService;

    public StrokesGainedAroundGreenService(IStrokesGainedPuttingService puttingService)
    {
        this.puttingService = puttingService;
    }

    public bool IsAroundGreenShot(GolfShot shot)
    {
        if (shot is null)
        {
            return false;
        }

        if (shot.StartLie is ShotLie.Green or ShotLie.Holed)
        {
            return false;
        }

        if (shot.IsTeeShot)
        {
            return false;
        }

        if (shot.StartDistanceToGreenEdgeYards > 30)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// SG:ARG uses the same strokes-gained formula as other categories:
    /// start expected strokes minus finish expected strokes minus the shot taken and penalties.
    /// Putting expected strokes are delegated to the existing putting service when an ARG shot finishes on the green.
    /// </summary>
    public double CalculateShotSgAroundGreen(GolfShot shot)
    {
        if (!IsAroundGreenShot(shot))
        {
            return 0;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(shot.StartDistanceToPin);
        ArgumentOutOfRangeException.ThrowIfNegative(shot.StartDistanceToGreenEdgeYards);
        ArgumentOutOfRangeException.ThrowIfNegative(shot.EndDistanceToPin);

        var startExpectedStrokes = GetAroundGreenExpectedStrokes(
            shot.StartDistanceToPin,
            shot.StartDistanceUnit,
            shot.StartLie);

        var finishExpectedStrokes = GetFinishExpectedStrokes(shot);

        return startExpectedStrokes - finishExpectedStrokes - 1 - shot.PenaltyStrokes;
    }

    public double CalculateRoundSgAroundGreen(IEnumerable<GolfShot> shots)
    {
        ArgumentNullException.ThrowIfNull(shots);

        return shots.Sum(CalculateShotSgAroundGreen);
    }

    /// <summary>
    /// Uses an approximate PGA Tour average around-the-green benchmark. Official PGA Tour ShotLink
    /// expected-strokes tables are not public, so these values are suitable as app-level estimates.
    /// </summary>
    public double GetAroundGreenExpectedStrokes(double distance, DistanceUnit unit, ShotLie lie)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distance);

        if (!AroundGreenBaseline.TryGetValue(lie, out var baseline))
        {
            throw new ArgumentOutOfRangeException(nameof(lie), lie, "Around-the-green expected strokes require an off-green lie.");
        }

        var distanceYards = unit switch
        {
            DistanceUnit.Feet => distance / FeetPerYard,
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

        return shot.EndLie == ShotLie.Green
            ? puttingService.GetExpectedPutts(shot.EndDistanceToPin, shot.EndDistanceUnit)
            : GetAroundGreenExpectedStrokes(shot.EndDistanceToPin, shot.EndDistanceUnit, shot.EndLie);
    }

    private static double InterpolateExpectedStrokes(
        double distanceYards,
        IReadOnlyList<AroundGreenBaselinePoint> baseline)
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

    private sealed record AroundGreenBaselinePoint(double DistanceYards, double ExpectedStrokes);
}
