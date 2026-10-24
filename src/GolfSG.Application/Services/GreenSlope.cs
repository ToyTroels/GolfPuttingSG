namespace GolfSG.Application.Services;

public enum GreenLevelHoleDirection { Top, Right, Bottom, Left }

public readonly record struct GreenSlope(double RightPercent, double ForwardPercent)
{
    // Rotate device axes into the golfer's view looking from ball to hole.
    public GreenSlope WithHoleDirection(GreenLevelHoleDirection direction) => direction switch
    {
        GreenLevelHoleDirection.Top => this,
        GreenLevelHoleDirection.Right => new(-ForwardPercent, RightPercent),
        GreenLevelHoleDirection.Bottom => new(-RightPercent, -ForwardPercent),
        GreenLevelHoleDirection.Left => new(ForwardPercent, -RightPercent),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
    // Android/MAUI device axes: +X right, +Y toward the top, +Z out of the screen.
    // Positive results mean the surface rises in that direction.
    public static GreenSlope FromGravity(double x, double y, double z,
        double rightOffsetRadians = 0, double forwardOffsetRadians = 0)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || z < 0.8)
            throw new ArgumentOutOfRangeException(nameof(z), "Place the phone face up and nearly flat.");
        if (!new GreenLevelCalibration(rightOffsetRadians, forwardOffsetRadians).IsValid)
            throw new ArgumentOutOfRangeException(nameof(rightOffsetRadians), "Calibration offsets must be finite and near level.");
        return new(100 * Math.Tan(Math.Atan2(x, z) - rightOffsetRadians),
            100 * Math.Tan(Math.Atan2(y, z) - forwardOffsetRadians));
    }
}
