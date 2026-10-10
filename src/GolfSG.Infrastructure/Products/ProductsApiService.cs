using System.Net.Http.Json;
using System.Text.Json;
using GolfSG.Application.Products;

namespace GolfSG.Infrastructure.Products;

public sealed class ProductsApiService(HttpClient client, IProductsApiSettings settings) : IProductsApiService
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        if (!ProductsApiAddress.TryNormalize(settings.BaseAddress, out var address))
            throw new ProductsApiException("Angiv serverens HTTPS-adresse for at hente produkter.");

        try
        {
            using var response = await client.GetAsync(new Uri(address!, "api/products"), cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new ProductsApiException("Serveren kunne ikke hente produkterne. Prøv igen.");

            var products = await response.Content.ReadFromJsonAsync(
                ProductsJsonContext.Default.ListProductDto, cancellationToken).ConfigureAwait(false);
            if (products is null || products.Any(p => p is null || p.Name is null))
                throw new ProductsApiException("Serveren sendte produktdata, der ikke kunne læses.");

            return products;
        }
        catch (HttpRequestException exception)
        {
            throw new ProductsApiException("Kan ikke kontakte serveren. Kontrollér forbindelsen og serveradressen, og prøv igen.", exception);
        }
        catch (JsonException exception)
        {
            throw new ProductsApiException("Serveren sendte produktdata, der ikke kunne læses.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProductsApiException("Serveren svarede ikke i tide. Prøv igen.", exception);
        }
    }
}
