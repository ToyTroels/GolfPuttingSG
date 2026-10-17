using GolfSG.Application.Products;

namespace GolfSG.Services;

public sealed class PreferenceProductsApiSettings : IProductsApiSettings
{
    public const string ProductionAddress = "https://golfputtingsg.onrender.com/";
    public bool CanConfigureEndpoint => false;

    // The trusted endpoint belongs to the deployment, never to user preferences.
    // Debug and release builds follow the same policy.
    public string BaseAddress
    {
        get
        {
#if GOLFSG_STAGING
            return StagingApiAddress.Value;
#else
            return ProductionAddress;
#endif
        }
        set { }
    }
}
