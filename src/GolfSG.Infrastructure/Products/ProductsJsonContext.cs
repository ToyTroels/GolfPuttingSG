using System.Text.Json.Serialization;
using GolfSG.Application.Products;

namespace GolfSG.Infrastructure.Products;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<ProductDto>))]
internal partial class ProductsJsonContext : JsonSerializerContext;
