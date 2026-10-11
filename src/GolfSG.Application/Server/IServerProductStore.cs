using GolfSG.Application.Products;

namespace GolfSG.Application.Server;

public interface IServerProductStore
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, string name, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
