namespace GolfSG.Application.Services;

public readonly record struct GreenLevelCalibration(double RightRadians, double ForwardRadians)
{
    public const double MaximumOffsetRadians = 0.2;

    public bool IsValid => double.IsFinite(RightRadians) && double.IsFinite(ForwardRadians) &&
        Math.Abs(RightRadians) <= MaximumOffsetRadians && Math.Abs(ForwardRadians) <= MaximumOffsetRadians;

    public static GreenLevelCalibration FromReading(GreenLevelReading reading)
    {
        // Validate orientation and non-finite readings before saving a calibration.
        _ = reading.ToSlope();
        var calibration = new GreenLevelCalibration(Math.Atan2(reading.X, reading.Z), Math.Atan2(reading.Y, reading.Z));
        if (!calibration.IsValid) throw new ArgumentOutOfRangeException(nameof(reading));
        return calibration;
    }
}
