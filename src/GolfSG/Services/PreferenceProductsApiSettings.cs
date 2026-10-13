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
#if DEBUG && !GOLFSG_STAGING
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
#if DEBUG && !GOLFSG_STAGING
            return Preferences.Default.Get(PreferenceKey, DefaultAddress);
#elif GOLFSG_STAGING
            return StagingApiAddress.Value;
#else
            return ProductionAddress;
#endif
        }
        set
        {
#if DEBUG && !GOLFSG_STAGING
            Preferences.Default.Set(PreferenceKey, value);
#endif
        }
    }

    private static string DefaultAddress
    {
        get
        {
#if DEBUG && !GOLFSG_STAGING
            return DeviceInfo.Current.Platform == DevicePlatform.Android
                && DeviceInfo.Current.DeviceType == DeviceType.Virtual
                    ? "https://10.0.2.2:7151/"
                    : "https://localhost:7151/";
#elif GOLFSG_STAGING
            return StagingApiAddress.Value;
#else
            return ProductionAddress;
#endif
        }
    }
}
