namespace GolfSG.Application.Products;

public interface IProductsApiSettings
{
    string BaseAddress { get; set; }
    bool CanConfigureEndpoint => true;
}
