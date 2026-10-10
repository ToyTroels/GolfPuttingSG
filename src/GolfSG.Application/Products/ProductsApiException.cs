namespace GolfSG.Application.Products;

public sealed class ProductsApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
