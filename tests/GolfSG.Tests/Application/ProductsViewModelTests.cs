using GolfSG.Application.Products;
using GolfSG.Application.ViewModels;

namespace GolfSG.Tests.Application;

[TestClass]
public sealed class ProductsViewModelTests
{
    [TestMethod]
    public async Task LoadingTransitionsToProductsAndEmptyStateAfterRefresh()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<ProductDto>>();
        var api = new Api { Get = _ => pending.Task };
        var model = new ProductsViewModel(api, new Settings());
        var load = model.LoadAsync();
        Assert.IsTrue(model.IsBusy);
        Assert.IsFalse(model.CanRefresh);
        Assert.IsFalse(model.IsEmpty);
        pending.SetResult([new(1, "Product 1")]);
        await load;
        Assert.IsFalse(model.IsBusy);
        Assert.HasCount(1, model.Products);
        api.Get = _ => Task.FromResult<IReadOnlyList<ProductDto>>([]);
        await model.LoadAsync();
        Assert.IsTrue(model.IsEmpty);
        Assert.IsTrue(model.CanRefresh);
    }

    [TestMethod]
    public async Task FailedRefreshKeepsPreviouslyLoadedProductsAndCanRecover()
    {
        var api = new Api();
        var model = new ProductsViewModel(api, new Settings());
        await model.LoadAsync();
        api.Get = _ => throw new ProductsApiException("Kan ikke kontakte serveren.");
        await model.LoadAsync();
        Assert.HasCount(1, model.Products);
        Assert.IsTrue(model.HasError);
        Assert.IsFalse(model.IsBusy);
        Assert.IsFalse(model.IsEmpty);
        api.Get = _ => Task.FromResult<IReadOnlyList<ProductDto>>([new(2, "Recovered")]);
        await model.LoadAsync();
        Assert.IsFalse(model.HasError);
        Assert.AreEqual("Recovered", model.Products[0].Name);
    }

    [TestMethod]
    public async Task NavigationCancellationDoesNotDisplayErrorOrApplyLateResults()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<ProductDto>>();
        var api = new Api { Get = _ => pending.Task };
        var model = new ProductsViewModel(api, new Settings());
        using var cancellation = new CancellationTokenSource();
        var load = model.LoadAsync(cancellation.Token);
        cancellation.Cancel();
        pending.SetResult([new(1, "Late result")]);
        await load;
        Assert.IsFalse(model.HasError);
        Assert.IsFalse(model.IsBusy);
        Assert.IsEmpty(model.Products);
    }

    [TestMethod]
    public async Task OlderRequestCannotOverwriteNewRefreshOrBusyState()
    {
        var oldRequest = new TaskCompletionSource<IReadOnlyList<ProductDto>>();
        var oldToken = CancellationToken.None;
        var api = new Api { Get = token => { oldToken = token; return oldRequest.Task; } };
        var model = new ProductsViewModel(api, new Settings());
        var oldLoad = model.LoadAsync();
        api.Get = _ => Task.FromResult<IReadOnlyList<ProductDto>>([new(2, "Newest")]);
        await model.LoadAsync();
        Assert.IsTrue(oldToken.IsCancellationRequested);
        oldRequest.SetResult([new(1, "Old")]);
        await oldLoad;
        Assert.AreEqual("Newest", model.Products.Single().Name);
        Assert.IsFalse(model.IsBusy);
    }

    [TestMethod]
    public async Task InvalidAddressIsNotSavedAndDoesNotMakeRequest()
    {
        var settings = new Settings();
        var api = new Api();
        var model = new ProductsViewModel(api, settings) { ServerAddress = "http://example.com" };
        await model.SaveServerAddressAsync();
        Assert.IsTrue(model.HasError);
        Assert.AreEqual("https://localhost:7151/", settings.BaseAddress);
        Assert.AreEqual(0, api.Calls);
    }

    [TestMethod]
    public async Task ChangingServerClearsPreviousServersProductsAndNormalizesSavedAddress()
    {
        var settings = new Settings();
        var api = new Api();
        var model = new ProductsViewModel(api, settings);
        await model.LoadAsync();
        api.Get = _ => throw new ProductsApiException("Unavailable");
        model.ServerAddress = " https://example.com/backend ";
        await model.SaveServerAddressAsync();
        Assert.AreEqual("https://example.com/backend/", settings.BaseAddress);
        Assert.AreEqual(settings.BaseAddress, model.ServerAddress);
        Assert.IsEmpty(model.Products);
        Assert.IsTrue(model.HasError);
        Assert.IsFalse(model.IsEmpty);
    }

    [TestMethod]
    public async Task UnexpectedFailureIsNotShownToUser()
    {
        var model = new ProductsViewModel(new Api { Get = _ => throw new InvalidOperationException("internal secret") }, new Settings());
        await model.LoadAsync();
        Assert.IsTrue(model.HasError);
        Assert.DoesNotContain("secret", model.ErrorMessage);
        Assert.IsFalse(model.IsBusy);
    }

    private sealed class Settings : IProductsApiSettings
    {
        public string BaseAddress { get; set; } = "https://localhost:7151/";
    }

    private sealed class Api : IProductsApiService
    {
        public int Calls;
        public Func<CancellationToken, Task<IReadOnlyList<ProductDto>>> Get =
            _ => Task.FromResult<IReadOnlyList<ProductDto>>([new(1, "Product 1")]);

        public Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Get(cancellationToken);
        }
    }
}
