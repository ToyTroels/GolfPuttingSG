namespace GolfSG.Application.Services;

// Smooths normalized gravity and checks short-term repeatability, not absolute accuracy.
public sealed class GreenLevelFilter
{
    private const int WindowSize = 12;
    private const int MinimumSamples = 10;
    private const double SmoothingFactor = 0.2;
    private const double MaximumRmsVariation = 0.006;
    private readonly Queue<(double X, double Y, double Z)> recent = new(WindowSize);
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Z { get; private set; }
    public bool HasReading { get; private set; }
    public bool IsStable { get; private set; }

    public bool Add(double x, double y, double z)
    {
        var magnitude = Math.Sqrt(x * x + y * y + z * z);
        if (!double.IsFinite(magnitude) || magnitude < 0.85 || magnitude > 1.15 || z / magnitude < 0.8)
        {
            Reset();
            return false;
        }
        x /= magnitude;
        y /= magnitude;
        z /= magnitude;
        if (!HasReading)
        {
            X = x;
            Y = y;
            Z = z;
        }
        else
        {
            X += SmoothingFactor * (x - X);
            Y += SmoothingFactor * (y - Y);
            Z += SmoothingFactor * (z - Z);
        }
        HasReading = true;
        recent.Enqueue((x, y, z));
        if (recent.Count > WindowSize) recent.Dequeue();
        var mx = recent.Average(s => s.X);
        var my = recent.Average(s => s.Y);
        var mz = recent.Average(s => s.Z);
        var variance = recent.Average(s =>
            (s.X - mx) * (s.X - mx) + (s.Y - my) * (s.Y - my) + (s.Z - mz) * (s.Z - mz));
        IsStable = recent.Count >= MinimumSamples && variance < MaximumRmsVariation * MaximumRmsVariation;
        return true;
    }

    public void Reset()
    {
        recent.Clear();
        HasReading = IsStable = false;
        X = Y = Z = 0;
    }
}
