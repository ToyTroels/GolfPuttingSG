namespace GolfSG.Services;

public enum PuttingDistanceUnitPreference
{
    Meters,
    Feet
}

public interface IDistanceUnitSettings
{
    PuttingDistanceUnitPreference PuttingDistanceUnit { get; set; }
}

public sealed class FixedDistanceUnitSettings(PuttingDistanceUnitPreference puttingDistanceUnit) : IDistanceUnitSettings
{
    public static FixedDistanceUnitSettings Meters { get; } = new(PuttingDistanceUnitPreference.Meters);

    public PuttingDistanceUnitPreference PuttingDistanceUnit { get; set; } = puttingDistanceUnit;
}