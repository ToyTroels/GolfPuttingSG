namespace GolfSG.Core;

internal static class PuttingDistanceGenerator
{
    public static IReadOnlyList<double> OrderDistances(
        IReadOnlyList<double> distancesMeters,
        PuttingDistanceOrder order,
        Random? random = null)
    {
        return order switch
        {
            PuttingDistanceOrder.Ascending => distancesMeters.Order().ToList(),
            PuttingDistanceOrder.Descending => distancesMeters.OrderDescending().ToList(),
            PuttingDistanceOrder.Random => ShuffleDistances(distancesMeters, random ?? Random.Shared),
            _ => throw new ArgumentOutOfRangeException(nameof(order), "Unknown distance order.")
        };
    }

    public static IReadOnlyList<double> BuildBellCurveDistancesMeters(
        int holeCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters)
    {
        if (holeCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(holeCount), "Hole count must be at least 1.");
        }

        if (minimumDistanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDistanceMeters), "Minimum distance must be greater than 0.");
        }

        if (maximumDistanceMeters < minimumDistanceMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistanceMeters), "Maximum distance must be greater than or equal to the minimum distance.");
        }

        if (holeCount == 1)
        {
            return [(minimumDistanceMeters + maximumDistanceMeters) / 2];
        }

        if (Math.Abs(maximumDistanceMeters - minimumDistanceMeters) < 0.001)
        {
            return Enumerable.Repeat(minimumDistanceMeters, holeCount).ToList();
        }

        var binCount = Math.Min(Math.Max(holeCount, 5), 21);
        var weights = Enumerable.Range(0, binCount)
            .Select(index =>
            {
                var centerOffset = (index - (binCount - 1) / 2d) / ((binCount - 1) / 2d);
                return Math.Exp(-0.5 * Math.Pow(centerOffset / 0.45, 2));
            })
            .ToList();

        var totalWeight = weights.Sum();
        var allocations = weights
            .Select((weight, index) =>
            {
                var exact = weight / totalWeight * holeCount;
                return new DistanceAllocation(index, (int)Math.Floor(exact), exact - Math.Floor(exact));
            })
            .ToList();

        var allocated = allocations.Sum(allocation => allocation.Count);
        foreach (var allocation in allocations
            .OrderByDescending(allocation => allocation.Remainder)
            .ThenBy(allocation => Math.Abs(allocation.Index - (binCount - 1) / 2d))
            .Take(holeCount - allocated))
        {
            allocation.Count++;
        }

        var distances = allocations
            .SelectMany(allocation => Enumerable.Repeat(
                RoundToNearestTenth(minimumDistanceMeters + (maximumDistanceMeters - minimumDistanceMeters) * allocation.Index / (binCount - 1)),
                allocation.Count))
            .OrderBy(distance => Math.Abs(distance - (minimumDistanceMeters + maximumDistanceMeters) / 2))
            .ThenBy(distance => distance)
            .ToList();

        return distances;
    }

    public static IReadOnlyList<double> BuildTrainingDistancesMeters(
        int puttCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters,
        PuttingTrainingDistanceDistribution distribution,
        Random? random = null)
    {
        return distribution switch
        {
            PuttingTrainingDistanceDistribution.BellCurve => BuildBellCurveDistancesMeters(
                puttCount,
                minimumDistanceMeters,
                maximumDistanceMeters),
            PuttingTrainingDistanceDistribution.Short => BuildSkewedRandomDistancesMeters(
                puttCount,
                minimumDistanceMeters,
                maximumDistanceMeters,
                favorShort: true,
                random ?? Random.Shared),
            PuttingTrainingDistanceDistribution.Long => BuildSkewedRandomDistancesMeters(
                puttCount,
                minimumDistanceMeters,
                maximumDistanceMeters,
                favorShort: false,
                random ?? Random.Shared),
            PuttingTrainingDistanceDistribution.Random => BuildUniformRandomDistancesMeters(
                puttCount,
                minimumDistanceMeters,
                maximumDistanceMeters,
                random ?? Random.Shared),
            _ => throw new ArgumentOutOfRangeException(nameof(distribution), "Unknown training distance distribution.")
        };
    }

    public static IReadOnlyList<double> BuildLadderDistancesMeters(
        double startDistanceMeters,
        double endDistanceMeters,
        double distanceStepMeters,
        int puttsPerDistance)
    {
        if (startDistanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startDistanceMeters), "Start distance must be greater than 0.");
        }

        if (endDistanceMeters < startDistanceMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(endDistanceMeters), "End distance must be greater than or equal to the start distance.");
        }

        if (distanceStepMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceStepMeters), "Distance step must be greater than 0.");
        }

        if (puttsPerDistance < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(puttsPerDistance), "Putts per distance must be at least 1.");
        }

        var distances = new List<double>();
        for (var distance = startDistanceMeters; distance <= endDistanceMeters + 0.0001; distance += distanceStepMeters)
        {
            distances.AddRange(Enumerable.Repeat(RoundToNearestTenth(distance), puttsPerDistance));
        }

        return distances;
    }

    public static IReadOnlyList<double> BuildLadderDistancesMeters(
        IReadOnlyList<double> distanceStopsMeters,
        int puttsPerDistance)
    {
        if (distanceStopsMeters.Count == 0)
        {
            throw new ArgumentException("At least one distance stop is required.", nameof(distanceStopsMeters));
        }

        if (distanceStopsMeters.Any(distance => distance <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(distanceStopsMeters), "All distance stops must be greater than 0.");
        }

        if (puttsPerDistance < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(puttsPerDistance), "Putts per distance must be at least 1.");
        }

        return distanceStopsMeters
            .Order()
            .SelectMany(distance => Enumerable.Repeat(RoundToNearestTenth(distance), puttsPerDistance))
            .ToList();
    }

    private static double RoundToNearestTenth(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private static IReadOnlyList<double> BuildUniformRandomDistancesMeters(
        int puttCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters,
        Random random)
    {
        ValidateRandomDistanceInputs(puttCount, minimumDistanceMeters, maximumDistanceMeters);

        return Enumerable.Range(0, puttCount)
            .Select(_ => RoundToNearestTenth(minimumDistanceMeters + random.NextDouble() * (maximumDistanceMeters - minimumDistanceMeters)))
            .ToList();
    }

    private static IReadOnlyList<double> BuildSkewedRandomDistancesMeters(
        int puttCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters,
        bool favorShort,
        Random random)
    {
        ValidateRandomDistanceInputs(puttCount, minimumDistanceMeters, maximumDistanceMeters);

        return Enumerable.Range(0, puttCount)
            .Select(_ =>
            {
                var skewed = Math.Pow(random.NextDouble(), 2);
                var position = favorShort ? skewed : 1 - skewed;
                return RoundToNearestTenth(minimumDistanceMeters + position * (maximumDistanceMeters - minimumDistanceMeters));
            })
            .ToList();
    }

    private static void ValidateRandomDistanceInputs(
        int puttCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters)
    {
        if (puttCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(puttCount), "Putt count must be at least 1.");
        }

        if (minimumDistanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDistanceMeters), "Minimum distance must be greater than 0.");
        }

        if (maximumDistanceMeters < minimumDistanceMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistanceMeters), "Maximum distance must be greater than or equal to the minimum distance.");
        }
    }

    private static IReadOnlyList<double> ShuffleDistances(IReadOnlyList<double> distancesMeters, Random random)
    {
        var distances = distancesMeters.ToList();
        for (var index = distances.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (distances[index], distances[swapIndex]) = (distances[swapIndex], distances[index]);
        }

        return distances;
    }

    private sealed class DistanceAllocation(int index, int count, double remainder)
    {
        public int Index { get; } = index;
        public int Count { get; set; } = count;
        public double Remainder { get; } = remainder;
    }
}
