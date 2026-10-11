using GolfSG.Services;

namespace GolfSG.Tests.Infrastructure;

[TestClass]
public sealed class ProductsHttpClientFactoryTests
{
    [TestMethod]
    public void ExplicitRequestsAllowAOneMinuteWakeUpWithProcessingTime()
    {
        using var client = ProductsHttpClientFactory.Create();
        Assert.IsTrue(client.Timeout > TimeSpan.FromMinutes(1),
            "An API that starts in one minute still needs time to process the requested operation.");
        Assert.IsTrue(client.DefaultRequestHeaders.Accept.Any(value => value.MediaType == "application/json"));
    }
}
