using GolfSG.Application.Products;

namespace GolfSG.Services;

public sealed class PreferenceProductsApiSettings : IProductsApiSettings
{
    private const string PreferenceKey = "products-api-base-address";

    public string BaseAddress
    {
        get => Preferences.Default.Get(PreferenceKey, DefaultAddress);
        set => Preferences.Default.Set(PreferenceKey, value);
    }

    private static string DefaultAddress
    {
        get
        {
#if DEBUG
            return DeviceInfo.Current.Platform == DevicePlatform.Android
                && DeviceInfo.Current.DeviceType == DeviceType.Virtual
                    ? "https://10.0.2.2:7151/"
                    : "https://localhost:7151/";
#else
            // Set the hosted HTTPS address on the Products page when the API is deployed.
            return string.Empty;
#endif
        }
    }
}
