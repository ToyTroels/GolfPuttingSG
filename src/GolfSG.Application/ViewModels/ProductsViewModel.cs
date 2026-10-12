using System.Collections.ObjectModel;
using GolfSG.Application.Products;

namespace GolfSG.Application.ViewModels;

public sealed class ProductsViewModel : ViewModelBase
{
    private readonly IProductsApiService api;
    private readonly IProductsApiSettings settings;
    private CancellationTokenSource? activeLoad;
    private int loadVersion;
    private bool isBusy;
    private bool hasLoaded;
    private string errorMessage = string.Empty;
    private string serverAddress;

    public ProductsViewModel(IProductsApiService api, IProductsApiSettings settings)
    {
        this.api = api;
        this.settings = settings;
        serverAddress = settings.BaseAddress;
        Products.CollectionChanged += (_, _) => NotifyDisplayState();
    }

    public ObservableCollection<ProductDto> Products { get; } = [];

    public string ServerAddress
    {
        get => serverAddress;
        set => SetProperty(ref serverAddress, value ?? string.Empty);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value)) NotifyDisplayState();
        }
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value)) NotifyDisplayState();
        }
    }

    public bool CanRefresh => !IsBusy;
    public bool CanConfigureServer => settings.CanConfigureEndpoint;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsEmpty => hasLoaded && !IsBusy && !HasError && Products.Count == 0;

    public async Task SaveServerAddressAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfigureServer) { ServerAddress = settings.BaseAddress; return; }
        if (!ProductsApiAddress.TryNormalize(ServerAddress, out var address))
        {
            ErrorMessage = "Angiv en gyldig HTTPS-adresse, f.eks. https://server.example/.";
            return;
        }

        try
        {
            var changed = settings.BaseAddress != address!.AbsoluteUri;
            settings.BaseAddress = address!.AbsoluteUri;
            ServerAddress = settings.BaseAddress;
            if (changed)
            {
                Products.Clear();
                hasLoaded = false;
                NotifyDisplayState();
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Serveradressen kunne ikke gemmes. Prøv igen.";
            return;
        }

        await LoadAsync(cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        // A newer refresh supersedes any older request, including one still finishing after navigation.
        var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = activeLoad;
        activeLoad = request;
        var version = ++loadVersion;
        previous?.Cancel();
        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            var products = await api.GetProductsAsync(request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (version != loadVersion) return;

            Products.Clear();
            foreach (var product in products) Products.Add(product);
            hasLoaded = true;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            // Leaving the page is expected and should not produce an error.
        }
        catch (ProductsApiException exception)
        {
            if (version == loadVersion) ErrorMessage = exception.Message;
        }
        catch (Exception)
        {
            if (version == loadVersion) ErrorMessage = "Produkterne kunne ikke hentes. Prøv igen.";
        }
        finally
        {
            if (version == loadVersion)
            {
                activeLoad = null;
                IsBusy = false;
                NotifyDisplayState();
            }
            request.Dispose();
        }
    }

    private void NotifyDisplayState()
    {
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
