using GolfSG.Services;

namespace GolfSG.Tests;

[TestClass]
public sealed class ProductionEndpointTests
{
#if !DEBUG
    [TestMethod]
    public void ReleaseAlwaysUsesRenderAndIgnoresEndpointWrites()
    {
        var settings = new PreferenceProductsApiSettings();
        Assert.IsFalse(settings.CanConfigureEndpoint);
        settings.BaseAddress = "https://localhost:7151/";
        Assert.AreEqual("https://golfputtingsg.onrender.com/", settings.BaseAddress);
    }
#endif
}
