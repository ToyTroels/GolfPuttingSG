using System.ComponentModel.DataAnnotations;

namespace GolfSG.Api.DTOs;

public sealed class UpdateProductDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;
}
