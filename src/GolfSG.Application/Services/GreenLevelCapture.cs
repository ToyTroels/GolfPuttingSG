namespace GolfSG.Application.Services;

public readonly record struct GreenLevelReading(double X, double Y, double Z)
{
    public GreenSlope ToSlope(double rightOffsetRadians = 0, double forwardOffsetRadians = 0) =>
        GreenSlope.FromGravity(X, Y, Z, rightOffsetRadians, forwardOffsetRadians);
}

// A capture is single-use. Sensor callbacks can arrive on another thread; freeze the
// first valid result at sample time, independently of when the UI next checks it.
public sealed class GreenLevelCapture
{
    public static TimeSpan MaxDuration { get; } = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaximumSampleGap = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RequiredStableDuration = TimeSpan.FromMilliseconds(500);
    private readonly object gate = new();
    private readonly GreenLevelFilter filter = new();
    private TimeSpan? stableSince;
    private TimeSpan? lastReading;
    private GreenLevelReading? result;

    public void Add(double x, double y, double z, TimeSpan elapsed)
    {
        lock (gate)
        {
            if (result.HasValue || elapsed < TimeSpan.Zero || elapsed > MaxDuration ||
                lastReading is { } last && elapsed <= last)
                return;

            if (lastReading is { } previous && elapsed - previous > MaximumSampleGap)
            {
                filter.Reset();
                stableSince = null;
            }
            filter.Add(x, y, z);
            lastReading = elapsed;
            if (!filter.IsStable)
            {
                stableSince = null;
                return;
            }
            stableSince ??= elapsed;
            if (elapsed - stableSince.Value >= RequiredStableDuration)
                result = new GreenLevelReading(filter.X, filter.Y, filter.Z);
        }
    }

    public bool TryGetResult(out GreenLevelReading reading)
    {
        lock (gate)
        {
            reading = result.GetValueOrDefault();
            return result.HasValue;
        }
    }
}
