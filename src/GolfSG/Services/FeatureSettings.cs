using Microsoft.Maui.Storage;

namespace GolfSG.Services;

public static class FeatureSettings
{
    private const string EnableBetaFeaturesKey = "enable-beta-features";

    public static bool EnableBetaFeatures
    {
        get => Preferences.Default.Get(EnableBetaFeaturesKey, false);
        set => Preferences.Default.Set(EnableBetaFeaturesKey, value);
    }
}
