using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GolfSG.Application.Products;
using GolfSG.Application.Accounts;

namespace GolfSG.Infrastructure.Products;

public sealed class ProductsApiService(HttpClient client, IProductsApiSettings settings, IApiAccountService accounts) : IProductsApiService
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        if (!ProductsApiAddress.TryNormalize(settings.BaseAddress, out var address))
            throw new ProductsApiException("Angiv serverens HTTPS-adresse for at hente produkter.");

        try
        {
            var session = await accounts.GetSessionAsync(address!, cancellationToken).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(session.Server, "api/products"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            using var response = await client.SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ProductsApiException("Din login-session er udløbet. Log ind igen via Konto og login på startskærmen.");
            if (!response.IsSuccessStatusCode)
                throw new ProductsApiException("Serveren kunne ikke hente produkterne. Prøv igen.");

            var products = await response.Content.ReadFromJsonAsync(
                ProductsJsonContext.Default.ListProductDto, cancellationToken).ConfigureAwait(false);
            if (products is null || products.Any(p => p is null || p.Name is null))
                throw new ProductsApiException("Serveren sendte produktdata, der ikke kunne læses.");
            return await accounts.WithSessionAsync<IReadOnlyList<ProductDto>>(session,
                () => Task.FromResult<IReadOnlyList<ProductDto>>(products), cancellationToken).ConfigureAwait(false);
        }
        catch (ApiAccountException exception) { throw new ProductsApiException(exception.Message, exception); }
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
