using GolfSG.Core;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public static class SgDistanceInputPresets
{
    public static IReadOnlyList<double> PuttingMeters { get; } =
        StrokesGainedCalculator.PuttingReference
            .Select(point => point.DistanceMeters)
            .ToArray();

    public static IReadOnlyList<double> MetricPuttingQuickPickMeters { get; } =
    [
        0.3,
        0.5,
        0.6,
        0.9,
        1.0,
        1.2,
        1.5,
        1.8,
        2.0,
        2.5,
        3.0,
        3.5,
        4.0,
        4.5,
        5.0,
        5.5,
        6.0,
        7.0,
        8.0,
        9.0,
        10.0,
        12.0,
        15.0,
        18.0,
        20.0,
        25.0,
        30.0
    ];

    public static IReadOnlyList<double> ApproachMeters { get; } =
    [
        Yard(30),
        Yard(40),
        Yard(50),
        Yard(60),
        Yard(75),
        Yard(100),
        Yard(125),
        Yard(150),
        Yard(175),
        Yard(200),
        Yard(225),
        Yard(250)
    ];

    public static IReadOnlyList<double> AroundGreenMeters { get; } =
    [
        Yard(3),
        Yard(5),
        Yard(10),
        Yard(15),
        Yard(20),
        Yard(25),
        Yard(30),
        Yard(35),
        Yard(40),
        Yard(45),
        Yard(50)
    ];

    public static IReadOnlyList<double> OffGreenFinishMeters { get; } =
        AroundGreenMeters
            .Concat(ApproachMeters)
            .DistinctBy(distance => Math.Round(distance, 3))
            .OrderBy(distance => distance)
            .ToArray();

    public static IReadOnlyList<double> FinishMeters { get; } =
        PuttingMeters
            .Concat(OffGreenFinishMeters)
            .DistinctBy(distance => Math.Round(distance, 3))
            .OrderBy(distance => distance)
            .ToArray();

    public static double Next(double currentValue, IReadOnlyList<double> intervals, double maximum)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        if (intervals.Count == 0)
        {
            return Math.Clamp(currentValue, 0, maximum);
        }

        var current = Math.Clamp(currentValue, 0, maximum);
        foreach (var interval in intervals.Where(interval => interval <= maximum))
        {
            if (interval > current + 0.001)
            {
                return interval;
            }
        }

        return intervals.Where(interval => interval <= maximum).DefaultIfEmpty(maximum).Last();
    }

    public static double Previous(double currentValue, IReadOnlyList<double> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        if (currentValue <= 0 || intervals.Count == 0)
        {
            return 0;
        }

        for (var index = intervals.Count - 1; index >= 0; index--)
        {
            var interval = intervals[index];
            if (interval < currentValue - 0.001)
            {
                return interval;
            }
        }

        return 0;
    }

    public static double Nearest(double currentValue, IReadOnlyList<double> intervals, double maximum)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        if (currentValue <= 0 || intervals.Count == 0)
        {
            return 0;
        }

        var current = Math.Clamp(currentValue, 0, maximum);
        return intervals
            .Where(interval => interval <= maximum)
            .DefaultIfEmpty(current)
            .MinBy(interval => Math.Abs(interval - current));
    }

    public static IReadOnlyList<DistanceQuickPick> ToQuickPicks(
        IReadOnlyList<double> intervals,
        double maximum,
        int decimals,
        PuttingDistanceUnitPreference? puttingDistanceUnit = null)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        return intervals
            .Where(distance => distance <= maximum)
            .Select(distance => new DistanceQuickPick(
                FormatLabel(distance, decimals, puttingDistanceUnit),
                distance))
            .ToArray();
    }

    public static IReadOnlyList<double> GetPuttingQuickPickMeters(
        PuttingDistanceUnitPreference puttingDistanceUnit) =>
        puttingDistanceUnit == PuttingDistanceUnitPreference.Meters
            ? MetricPuttingQuickPickMeters
            : PuttingMeters;

    private static string FormatLabel(
        double distanceMeters,
        int decimals,
        PuttingDistanceUnitPreference? puttingDistanceUnit)
    {
        if (puttingDistanceUnit is { } unit)
        {
            return decimals == 0
                ? UiFormat.WholePuttingDistance(distanceMeters, unit)
                : UiFormat.PuttingDistance(distanceMeters, unit);
        }

        return decimals == 0
            ? UiFormat.WholeMeters(distanceMeters)
            : UiFormat.Meters(distanceMeters);
    }

    private static double Yard(double yards) => Math.Round(DistanceConversions.YardsToMeters(yards));
}

public sealed record DistanceQuickPick(string Text, double Meters);
