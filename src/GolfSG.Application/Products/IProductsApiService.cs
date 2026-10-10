namespace GolfSG.Application.Products;

public interface IProductsApiService
{
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default);
}
