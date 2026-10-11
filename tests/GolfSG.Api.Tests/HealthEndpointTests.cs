using System.Data.Common;
using System.Net;
using System.Text.Json;
using GolfSG.Api.Controllers;
using GolfSG.Application.Server;
using GolfSG.Server.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class HealthEndpointTests
{
    [TestMethod]
    [DataRow(true, HttpStatusCode.OK)]
    [DataRow(false, HttpStatusCode.ServiceUnavailable)]
    public async Task AnonymousHealthRequestsReportReadinessWithoutInternalDetails(bool ready, HttpStatusCode status)
    {
        await using var factory = new HealthApi(new Check(ready));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        using var response = await client.GetAsync("/api/health");

        Assert.AreEqual(status, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType!.MediaType);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(2, result.RootElement.EnumerateObject().Count());
        Assert.AreEqual("GolfSG.Api", result.RootElement.GetProperty("service").GetString());
        Assert.AreEqual(ready, result.RootElement.GetProperty("databaseAvailable").GetBoolean());
    }

    [TestMethod]
    public async Task RequestAbortedTokenReachesTheReadinessCheck()
    {
        var check = new Check(true);
        using var aborted = new CancellationTokenSource();
        var controller = new HealthController(check)
        {
            ControllerContext = new ControllerContext
                { HttpContext = new DefaultHttpContext { RequestAborted = aborted.Token } }
        };

        await controller.Get();

        Assert.AreEqual(aborted.Token, check.LastToken);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExpectedDatabaseAndTimeoutFailuresReturnUnavailable(bool timeout)
    {
        Exception error = timeout ? new TimeoutException("private database detail") : new DatabaseFailure("private database detail");
        await using var db = Context(error);

        Assert.IsFalse(await new PostgresConnectionCheck(db).IsReadyAsync(default));
    }

    [TestMethod]
    public async Task DatabaseCancellationPropagates()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await using var db = Context(new OperationCanceledException(canceled.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new PostgresConnectionCheck(db).IsReadyAsync(canceled.Token));
    }

    [TestMethod]
    public async Task UnexpectedProgrammingFailuresAreNotReportedAsDatabaseOutages()
    {
        await using var db = Context(new InvalidOperationException("configuration/programming failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => new PostgresConnectionCheck(db).IsReadyAsync(default));
    }

    private static AppDbContext Context(Exception error) => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=golfsg_test_unused;Username=unused")
        .AddInterceptors(new FailingConnection(error)).Options);

    private sealed class DatabaseFailure(string message) : DbException(message);

    private sealed class FailingConnection(Exception error) : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult>(error);
    }

    private sealed class Check(bool ready) : IServerConnectionCheck
    {
        public CancellationToken LastToken { get; private set; }
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            LastToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ready);
        }
    }

    private sealed class HealthApi(IServerConnectionCheck check) : WebApplicationFactory<GolfSG.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=golfsg_test_unused;Username=unused",
                ["Auth:DataProtectionKeysPath"] = ""
            }))
            .ConfigureServices(services =>
            {
                services.RemoveAll<IServerConnectionCheck>();
                services.AddSingleton(check);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
    }
}
