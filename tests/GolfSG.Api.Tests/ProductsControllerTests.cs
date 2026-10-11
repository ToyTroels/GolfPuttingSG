using GolfSG.Api.Controllers;
using GolfSG.Api.DTOs;
using GolfSG.Application.Products;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Reflection;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class ProductsControllerTests
{
    [TestMethod]
    public void ProductEndpointsRequireIdentityBearerAuthentication()
    {
        var authorization = typeof(ProductsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.IsNotNull(authorization);
        Assert.AreEqual(IdentityConstants.BearerScheme, authorization.AuthenticationSchemes);
    }

    [TestMethod]
    public async Task ReadsReturnApplicationDtosAndForwardCancellation()
    {
        var product = new ProductDto(7, "Practice ball");
        var store = new TestProductStore { Products = [product] };
        var controller = new ProductsController(store);
        using var cancellation = new CancellationTokenSource();

        var collection = (await controller.GetAll(cancellation.Token)).Result;
        Assert.IsInstanceOfType<OkObjectResult>(collection);
        CollectionAssert.AreEqual(new[] { product },
            ((IEnumerable<ProductDto>)((OkObjectResult)collection).Value!).ToArray());
        Assert.AreEqual(cancellation.Token, store.LastToken);

        var single = (await controller.GetById(product.Id, cancellation.Token)).Result;
        Assert.IsInstanceOfType<OkObjectResult>(single);
        Assert.AreSame(product, ((OkObjectResult)single).Value);
        Assert.AreEqual(product.Id, store.LastId);
        Assert.AreEqual(cancellation.Token, store.LastToken);
    }

    [TestMethod]
    public async Task MissingProductReturnsNotFound()
    {
        var response = await new ProductsController(new TestProductStore()).GetById(42, default);

        Assert.IsInstanceOfType<NotFoundResult>(response.Result);
    }

    [TestMethod]
    public async Task CreateReturnsDtoAndLinkToReadNewProduct()
    {
        var product = new ProductDto(7, "Practice ball");
        var store = new TestProductStore { Created = product };
        using var cancellation = new CancellationTokenSource();

        var response = (await new ProductsController(store).Create(
            new CreateProductDto { Name = product.Name }, cancellation.Token)).Result;

        Assert.IsInstanceOfType<CreatedAtActionResult>(response);
        var created = (CreatedAtActionResult)response;
        Assert.AreSame(product, created.Value);
        Assert.AreEqual(nameof(ProductsController.GetById), created.ActionName);
        Assert.AreEqual(product.Id, created.RouteValues!["id"]);
        Assert.AreEqual(product.Name, store.LastName);
        Assert.AreEqual(cancellation.Token, store.LastToken);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task UpdateReturnsNoContentOrNotFound(bool exists)
    {
        var store = new TestProductStore { MutationSucceeded = exists };
        using var cancellation = new CancellationTokenSource();

        var response = await new ProductsController(store).Update(7,
            new UpdateProductDto { Name = "New name" }, cancellation.Token);

        Assert.AreEqual(exists ? typeof(NoContentResult) : typeof(NotFoundResult), response.GetType());
        Assert.AreEqual(7, store.LastId);
        Assert.AreEqual("New name", store.LastName);
        Assert.AreEqual(cancellation.Token, store.LastToken);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task DeleteReturnsNoContentOrNotFound(bool exists)
    {
        var store = new TestProductStore { MutationSucceeded = exists };
        using var cancellation = new CancellationTokenSource();

        var response = await new ProductsController(store).Delete(7, cancellation.Token);

        Assert.AreEqual(exists ? typeof(NoContentResult) : typeof(NotFoundResult), response.GetType());
        Assert.AreEqual(7, store.LastId);
        Assert.AreEqual(cancellation.Token, store.LastToken);
    }

    private sealed class TestProductStore : IServerProductStore
    {
        public IReadOnlyList<ProductDto> Products { get; init; } = [];
        public ProductDto Created { get; init; } = new(1, "Created product");
        public bool MutationSucceeded { get; init; }
        public int? LastId { get; private set; }
        public string? LastName { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            LastToken = cancellationToken;
            return Task.FromResult(Products);
        }

        public Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            LastId = id;
            LastToken = cancellationToken;
            return Task.FromResult(Products.SingleOrDefault(product => product.Id == id));
        }

        public Task<ProductDto> CreateAsync(string name, CancellationToken cancellationToken = default)
        {
            LastName = name;
            LastToken = cancellationToken;
            return Task.FromResult(Created);
        }

        public Task<bool> UpdateAsync(int id, string name, CancellationToken cancellationToken = default)
        {
            LastId = id;
            LastName = name;
            LastToken = cancellationToken;
            return Task.FromResult(MutationSucceeded);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            LastId = id;
            LastToken = cancellationToken;
            return Task.FromResult(MutationSucceeded);
        }
    }
}
