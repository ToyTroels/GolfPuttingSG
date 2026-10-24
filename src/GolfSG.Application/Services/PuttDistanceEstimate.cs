namespace GolfSG.Application.Services;

public static class PuttDistanceEstimate
{
    public const double DefaultStepLengthCentimeters = 75;
    public const double MinimumStepLengthCentimeters = 20;
    public const double MaximumStepLengthCentimeters = 150;
    public const double MaximumDistanceMeters = 30;

    public static bool IsValidStepLength(double centimeters) => double.IsFinite(centimeters) &&
        centimeters >= MinimumStepLengthCentimeters && centimeters <= MaximumStepLengthCentimeters;

    public static double Meters(int steps, double stepLengthCentimeters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(steps);
        if (!IsValidStepLength(stepLengthCentimeters))
            throw new ArgumentOutOfRangeException(nameof(stepLengthCentimeters));
        return Math.Round(steps * stepLengthCentimeters / 100, 1);
    }

    public static bool CanApply(double meters) => double.IsFinite(meters) && meters > 0 && meters <= MaximumDistanceMeters;
}
