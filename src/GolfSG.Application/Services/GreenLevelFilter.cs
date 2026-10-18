namespace GolfSG.Application.Services;

// Filters gravity, while treating stability as feedback rather than a display gate.
public sealed class GreenLevelFilter
{
    private readonly Queue<(double X, double Y, double Z)> recent = new();
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
        if (!HasReading) { X = x; Y = y; Z = z; }
        else { X += 0.2 * (x - X); Y += 0.2 * (y - Y); Z += 0.2 * (z - Z); }
        HasReading = true;
        recent.Enqueue((x, y, z));
        if (recent.Count > 12) recent.Dequeue();
        var mx = recent.Average(s => s.X);
        var my = recent.Average(s => s.Y);
        var mz = recent.Average(s => s.Z);
        var variance = recent.Average(s =>
            (s.X - mx) * (s.X - mx) + (s.Y - my) * (s.Y - my) + (s.Z - mz) * (s.Z - mz));
        IsStable = recent.Count >= 10 && variance < 0.006 * 0.006;
        return true;
    }

    public void Reset()
    {
        recent.Clear();
        HasReading = IsStable = false;
        X = Y = Z = 0;
    }
}
