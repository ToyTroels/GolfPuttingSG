using GolfSG.Application.Products;
using GolfSG.Application.Server;
using GolfSG.Server.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresProductStore(AppDbContext db) : IServerProductStore
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Products.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new ProductDto(p.Id, p.Name)).ToListAsync(cancellationToken);

    public Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new ProductDto(p.Id, p.Name)).SingleOrDefaultAsync(cancellationToken);

    public async Task<ProductDto> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var product = new Product { Name = name };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return new ProductDto(product.Id, product.Name);
    }

    public async Task<bool> UpdateAsync(int id, string name, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null) return false;
        product.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null) return false;
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
