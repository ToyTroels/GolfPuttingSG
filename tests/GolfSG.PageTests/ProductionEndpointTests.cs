using GolfSG.Services;

namespace GolfSG.Tests;

[TestClass]
public sealed class ProductionEndpointTests
{
#if GOLFSG_STAGING
    [TestMethod]
    public void StagingUsesItsBuildAddressAndIgnoresEndpointWrites()
    {
        var settings = new PreferenceProductsApiSettings();
        Assert.IsFalse(settings.CanConfigureEndpoint);
        Assert.AreNotEqual(PreferenceProductsApiSettings.ProductionAddress, settings.BaseAddress);
        Assert.AreEqual(StagingApiAddress.Value, settings.BaseAddress);
        settings.BaseAddress = PreferenceProductsApiSettings.ProductionAddress;
        Assert.AreEqual(StagingApiAddress.Value, settings.BaseAddress);
        settings.BaseAddress = "https://localhost:7151/";
        Assert.AreEqual(StagingApiAddress.Value, settings.BaseAddress);
    }
#else
    [TestMethod]
    public void ProductionAlwaysUsesRenderAndIgnoresEndpointWritesIncludingDebug()
    {
        var settings = new PreferenceProductsApiSettings();
        Assert.IsFalse(settings.CanConfigureEndpoint);
        settings.BaseAddress = "https://localhost:7151/";
        Assert.AreEqual("https://golfputtingsg.onrender.com/", settings.BaseAddress);
    }
#endif
}
