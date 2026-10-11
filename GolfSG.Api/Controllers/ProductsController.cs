using GolfSG.Api.DTOs;
using GolfSG.Application.Products;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
[Route("api/products")]
public class ProductsController(IServerProductStore products) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await products.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto request, CancellationToken cancellationToken)
    {
        var product = await products.CreateAsync(request.Name, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateProductDto request, CancellationToken cancellationToken) =>
        await products.UpdateAsync(id, request.Name, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await products.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
