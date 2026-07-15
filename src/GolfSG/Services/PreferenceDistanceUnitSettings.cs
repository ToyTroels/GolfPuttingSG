using Microsoft.Maui.Storage;

namespace GolfSG.Services;

public sealed class PreferenceDistanceUnitSettings : IDistanceUnitSettings
{
    private const string PuttingDistanceUnitKey = "putting-distance-unit";
    private const string FeetValue = "feet";
    private const string MetersValue = "meters";

    public PuttingDistanceUnitPreference PuttingDistanceUnit
    {
        get
        {
            var value = Preferences.Default.Get(PuttingDistanceUnitKey, MetersValue);
            return string.Equals(value, FeetValue, StringComparison.OrdinalIgnoreCase)
                ? PuttingDistanceUnitPreference.Feet
                : PuttingDistanceUnitPreference.Meters;
        }
        set => Preferences.Default.Set(PuttingDistanceUnitKey, value == PuttingDistanceUnitPreference.Feet ? FeetValue : MetersValue);
    }
}