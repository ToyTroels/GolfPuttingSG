using Microsoft.Maui.Storage;

namespace GolfSG.Application.Services;

public static class FeatureSettings
{
    // Temporarily unavailable while short-distance step detection is unreliable.
    public static bool EnableStepDistanceMeasurement => false;

    public static bool RecordGreenInRegulation
    {
        get => Preferences.Default.Get("record-green-in-regulation", false);
        set => Preferences.Default.Set("record-green-in-regulation", value);
    }
    private const string EnableBetaFeaturesKey = "enable-beta-features";
    private const string UseGuidedHoleEntryKey = "use-guided-hole-entry";

    public static bool EnableBetaFeatures
    {
        get => Preferences.Default.Get(EnableBetaFeaturesKey, false);
        set => Preferences.Default.Set(EnableBetaFeaturesKey, value);
    }

    public static bool UseGuidedHoleEntry
    {
        get => Preferences.Default.Get(UseGuidedHoleEntryKey, true);
        set => Preferences.Default.Set(UseGuidedHoleEntryKey, value);
    }
}
