namespace GolfSG.Application.Services;

public readonly record struct GreenSlope(double RightPercent, double ForwardPercent)
{
    // With the hole at the phone's right edge, the golfer's right is screen-down.
    public GreenSlope WithHoleAtRightEdge() => new(-ForwardPercent, RightPercent);
    // Android/MAUI device axes: +X right, +Y toward the top, +Z out of the screen.
    // Positive results mean the surface rises in that direction.
    public static GreenSlope FromGravity(double x, double y, double z,
        double rightOffsetRadians = 0, double forwardOffsetRadians = 0)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || z < 0.8)
            throw new ArgumentOutOfRangeException(nameof(z), "Place the phone face up and nearly flat.");
        return new(100 * Math.Tan(Math.Atan2(x, z) - rightOffsetRadians),
            100 * Math.Tan(Math.Atan2(y, z) - forwardOffsetRadians));
    }
}
