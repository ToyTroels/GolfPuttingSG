using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class StagingAuthenticationIsolationTests
{
    [TestMethod]
    public async Task StagingAndProductionRejectEachOthersTokensEvenWithTheSameKeyDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "golfsg-staging-keys-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var staging = new EnvironmentApi("Staging", directory);
            await using var production = new EnvironmentApi("Production", directory);
            var stagingOptions = Options(staging);
            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")],
                    IdentityConstants.BearerScheme)),
                new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) },
                IdentityConstants.BearerScheme);
            var stagingToken = stagingOptions.BearerTokenProtector.Protect(ticket);
            var productionOptions = Options(production);
            var productionToken = productionOptions.BearerTokenProtector.Protect(ticket);

            Assert.IsNotNull(stagingOptions.BearerTokenProtector.Unprotect(stagingToken));
            Assert.IsNotNull(productionOptions.BearerTokenProtector.Unprotect(productionToken));
            Assert.IsNull(productionOptions.BearerTokenProtector.Unprotect(stagingToken));
            Assert.IsNull(stagingOptions.BearerTokenProtector.Unprotect(productionToken));
            Assert.AreEqual("GolfSG.Api", production.Services
                .GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static BearerTokenOptions Options(EnvironmentApi factory) => factory.Services
        .GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);

    private sealed class EnvironmentApi(string environment, string keysPath) : WebApplicationFactory<GolfSG.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment(environment)
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=golfsg_test_unused;Username=unused",
                    ["Auth:DataProtectionKeysPath"] = keysPath
                }));
    }
}
