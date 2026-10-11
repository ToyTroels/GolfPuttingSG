using System.Net.Http.Headers;
using GolfSG.Infrastructure.Products;

namespace GolfSG.Services;

public static class ProductsHttpClientFactory
{
    public static HttpClient Create()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
            LocalDevelopmentCertificate.IsAccepted(request.RequestUri, certificate, errors);
#endif
        // A sleeping API can take about a minute to start. Explicit requests allow startup plus normal processing.
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
