using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class DeploymentEndpointTests
{
    [TestMethod]
    [DataRow("0123456789abcdef0123456789abcdef01234567")]
    [DataRow(null)]
    [DataRow("not-a-commit")]
    public async Task AnonymousDeploymentRequestsExposeOnlyAValidCommit(string? configuredCommit)
    {
        await using var factory = new DeploymentApi(configuredCommit);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync("/api/deployment");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(2, body.RootElement.EnumerateObject().Count());
        Assert.AreEqual("GolfSG.Api", body.RootElement.GetProperty("service").GetString());
        Assert.AreEqual(configuredCommit?.Length == 40 ? configuredCommit : null,
            body.RootElement.GetProperty("commit").GetString());
    }

    private sealed class DeploymentApi(string? commit) : WebApplicationFactory<GolfSG.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=golfsg_test_unused;Username=unused",
                    ["Auth:DataProtectionKeysPath"] = "",
                    ["RENDER_GIT_COMMIT"] = commit
                }));
    }
}
