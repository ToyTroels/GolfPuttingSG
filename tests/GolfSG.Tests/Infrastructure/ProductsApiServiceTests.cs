using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using GolfSG.Application.Products;
using GolfSG.Infrastructure.Products;
using GolfSG.Tests.Infrastructure.TestDoubles;

namespace GolfSG.Tests.Infrastructure;

[TestClass]
public sealed class ProductsApiServiceTests
{
    [TestMethod]
    public async Task ReadsApiJsonAndUsesCurrentSavedAddressOnEachRequest()
    {
        var requests = new List<Uri>();
        using var client = new HttpClient(new Handler((request, _) =>
        {
            requests.Add(request.RequestUri!);
            Assert.AreEqual("Bearer test-access-token", request.Headers.Authorization!.ToString());
            return Task.FromResult(Json("[{\"id\":1,\"name\":\"Product 1\"},{\"id\":2,\"name\":\"Product 2\"}]"));
        }));
        var settings = new Settings { BaseAddress = "https://localhost:7151" };
        var service = new ProductsApiService(client, settings, new TestApiAccountService(settings));

        var products = await service.GetProductsAsync();
        Assert.HasCount(2, products);
        Assert.AreEqual(new ProductDto(1, "Product 1"), products[0]);
        Assert.AreEqual("https://localhost:7151/api/products", requests[0].AbsoluteUri);

        settings.BaseAddress = "https://example.com/backend";
        await service.GetProductsAsync();
        Assert.AreEqual("https://example.com/backend/api/products", requests[1].AbsoluteUri);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("localhost:7151")]
    [DataRow("http://localhost:5081")]
    [DataRow("https://user:password@example.com")]
    [DataRow("https://example.com/?key=secret")]
    [DataRow("https://example.com/#fragment")]
    public async Task RejectsUnsafeOrMissingAddressBeforeMakingRequest(string? address)
    {
        var called = false;
        using var client = new HttpClient(new Handler((_, _) =>
        {
            called = true;
            return Task.FromResult(Json("[]"));
        }));
        var service = new ProductsApiService(client, new Settings { BaseAddress = address! }, new TestApiAccountService());
        await Assert.ThrowsAsync<ProductsApiException>(() => service.GetProductsAsync());
        Assert.IsFalse(called);
    }

    [TestMethod]
    public async Task ServerFailureDoesNotExposeItsBody()
    {
        using var client = new HttpClient(new Handler((_, _) =>
            Task.FromResult(Json("database password: secret", HttpStatusCode.InternalServerError))));
        var exception = await Assert.ThrowsAsync<ProductsApiException>(() => Service(client).GetProductsAsync());
        Assert.DoesNotContain("secret", exception.Message);
        Assert.DoesNotContain("database", exception.Message);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("{}")]
    [DataRow("not json")]
    [DataRow("[null]")]
    [DataRow("[{\"id\":1}]")]
    [DataRow("[{\"name\":\"Missing ID\"}]")]
    [DataRow("[{\"id\":1,\"name\":null}]")]
    public async Task InvalidPayloadReturnsReadableError(string payload)
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Json(payload))));
        var exception = await Assert.ThrowsAsync<ProductsApiException>(() => Service(client).GetProductsAsync());
        Assert.Contains("produktdata", exception.Message);
    }

    [TestMethod]
    public async Task NetworkFailureDoesNotExposeInternalDetails()
    {
        using var client = new HttpClient(new Handler((_, _) => throw new HttpRequestException("internal secret")));
        var exception = await Assert.ThrowsAsync<ProductsApiException>(() => Service(client).GetProductsAsync());
        Assert.Contains("Kan ikke kontakte", exception.Message);
        Assert.DoesNotContain("secret", exception.Message);
    }

    [TestMethod]
    public async Task TimeoutHasDifferentMessageFromCallerCancellation()
    {
        using var client = new HttpClient(new Handler((_, _) => throw new TaskCanceledException()));
        var exception = await Assert.ThrowsAsync<ProductsApiException>(() => Service(client).GetProductsAsync());
        Assert.Contains("i tide", exception.Message);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service(client).GetProductsAsync(canceled.Token));
    }

    [TestMethod]
    public void CertificateExceptionIsRestrictedToValidLocalAspNetCertificates()
    {
        using var certificate = Certificate("CN=localhost", includeDevelopmentExtension: true);
        var errors = SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors;
        foreach (var host in new[] { "localhost", "127.0.0.1", "[::1]", "10.0.2.2", "192.168.50.189" })
            Assert.IsTrue(LocalDevelopmentCertificate.IsAccepted(new Uri($"https://{host}:7151/"), certificate, errors));

        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("https://example.com/"), certificate, errors));
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("http://localhost/"), certificate, errors));
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("https://localhost/"), null, errors));
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("https://localhost/"), certificate, SslPolicyErrors.RemoteCertificateNotAvailable));
        using var ordinary = Certificate("CN=localhost", includeDevelopmentExtension: false);
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("https://localhost/"), ordinary, errors));
        using var expired = Certificate("CN=localhost", includeDevelopmentExtension: true, expired: true);
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("https://localhost/"), expired, errors));
        using var wrongName = Certificate("CN=other", includeDevelopmentExtension: true);
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("https://localhost/"), wrongName, errors));
        Assert.IsTrue(LocalDevelopmentCertificate.IsAccepted(new Uri("https://example.com/"), wrongName, SslPolicyErrors.None));
    }

    [TestMethod]
    public void WifiCertificateExceptionRejectsOtherHostsAndInvalidCertificates()
    {
        var address = new Uri("https://192.168.50.189/");
        var errors = SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors;
        using var certificate = Certificate("CN=localhost", includeDevelopmentExtension: true);
        Assert.IsTrue(LocalDevelopmentCertificate.IsAccepted(address, certificate, errors));
        foreach (var host in new[] { "192.168.50.190", "192.168.1.1", "8.8.8.8", "192.168.50.189.example.com" })
            Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri($"https://{host}/"), certificate, errors));
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(new Uri("http://192.168.50.189/"), certificate, errors));
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(address, null, errors));
        Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(address, certificate, SslPolicyErrors.RemoteCertificateNotAvailable));
        using var ordinary = Certificate("CN=localhost", includeDevelopmentExtension: false);
        using var expired = Certificate("CN=localhost", includeDevelopmentExtension: true, expired: true);
        using var wrongName = Certificate("CN=other", includeDevelopmentExtension: true);
        foreach (var invalid in new[] { ordinary, expired, wrongName })
            Assert.IsFalse(LocalDevelopmentCertificate.IsAccepted(address, invalid, errors));
    }

    private static X509Certificate2 Certificate(string subject, bool includeDevelopmentExtension, bool expired = false)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (includeDevelopmentExtension)
            request.CertificateExtensions.Add(new X509Extension("1.3.6.1.4.1.311.84.1.1", new byte[] { 0x02 }, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(expired ? -1 : 1));
    }

    private static ProductsApiService Service(HttpClient client) => new(client, new Settings(), new TestApiAccountService());
    private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class Settings : IProductsApiSettings
    {
        public string BaseAddress { get; set; } = "https://localhost:7151/";
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
