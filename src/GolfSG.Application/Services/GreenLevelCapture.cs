namespace GolfSG.Application.Services;

// Times are relative to sensor start; the tap-settling pause is outside the capture.
public sealed class GreenLevelCapture
{
    public static TimeSpan MaxDuration { get; } = TimeSpan.FromSeconds(3);
    public GreenLevelFilter Filter { get; } = new();
    private TimeSpan? stableSince;
    private TimeSpan? lastReading;

    public void Add(double x, double y, double z, TimeSpan elapsed)
    {
        if (lastReading is { } previous && elapsed - previous > TimeSpan.FromMilliseconds(250))
        {
            Filter.Reset();
            stableSince = null;
        }
        Filter.Add(x, y, z);
        lastReading = elapsed;
        if (Filter.IsStable) stableSince ??= elapsed;
        else stableSince = null;
    }

    public bool IsComplete(TimeSpan elapsed) =>
        elapsed <= MaxDuration && Filter.IsStable &&
        stableSince is { } stable && elapsed - stable >= TimeSpan.FromMilliseconds(500) &&
        lastReading is { } last && elapsed - last <= TimeSpan.FromMilliseconds(250);
}
