using Microsoft.Maui.Storage;

namespace GolfSG.Services;

public static class FeatureSettings
{
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
