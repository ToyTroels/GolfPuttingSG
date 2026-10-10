using System.Text.Json.Serialization;

namespace GolfSG.Application.Products;

public sealed record ProductDto(
    [property: JsonRequired] int Id,
    [property: JsonRequired] string Name);
