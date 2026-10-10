using GolfSG.Api.Data;
using GolfSG.Api.DTOs;
using GolfSG.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await dbContext.Products.AsNoTracking()
            .OrderBy(p => p.Id).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(CreateProductDto request, CancellationToken cancellationToken)
    {
        var product = new Product { Name = request.Name };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProductDto request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
            return NotFound();

        product.Name = request.Name;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
            return NotFound();

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
