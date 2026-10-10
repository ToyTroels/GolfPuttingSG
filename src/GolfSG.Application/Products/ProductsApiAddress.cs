namespace GolfSG.Application.Products;

public static class ProductsApiAddress
{
    public static bool TryNormalize(string? address, out Uri? baseAddress)
    {
        baseAddress = null;
        if (!Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrEmpty(parsed.Host)
            || !string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        baseAddress = parsed.AbsoluteUri.EndsWith('/') ? parsed : new Uri(parsed.AbsoluteUri + "/");
        return true;
    }
}
