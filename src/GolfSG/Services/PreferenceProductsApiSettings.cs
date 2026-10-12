using GolfSG.Application.Products;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace GolfSG.Services;

public sealed class PreferenceProductsApiSettings : IProductsApiSettings
{
    private const string PreferenceKey = "products-api-base-address";
    public const string ProductionAddress = "https://golfputtingsg.onrender.com/";

    public bool CanConfigureEndpoint
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public string BaseAddress
    {
        get
        {
#if DEBUG
            return Preferences.Default.Get(PreferenceKey, DefaultAddress);
#else
            return ProductionAddress;
#endif
        }
        set
        {
#if DEBUG
            Preferences.Default.Set(PreferenceKey, value);
#endif
        }
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
            return ProductionAddress;
#endif
        }
    }
}
