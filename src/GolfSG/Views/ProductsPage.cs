using GolfSG.Application.Products;
using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class ProductsPage : ContentPage
{
    private readonly ProductsViewModel viewModel;
    private CancellationTokenSource? pageCancellation;

    public ProductsPage(ProductsViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Produkter";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible(UiAutomationIds.ProductsPage, "Produkter");

        var serverAddress = new Entry
        {
            Placeholder = "https://server.example/",
            Keyboard = Keyboard.Url,
            IsTextPredictionEnabled = false,
            IsSpellCheckEnabled = false,
            TextColor = GolfTheme.Colors.Text
        }.Accessible(UiAutomationIds.ProductsServerAddress, "Serveradresse");
        serverAddress.SetBinding(Entry.TextProperty, nameof(ProductsViewModel.ServerAddress), BindingMode.TwoWay);
        serverAddress.SetBinding(IsEnabledProperty, nameof(ProductsViewModel.CanRefresh));

        var save = AppViews.SecondaryButton("Gem og hent")
            .Accessible(UiAutomationIds.ProductsSaveAddress, "Gem serveradressen og hent produkter");
        save.SetBinding(IsEnabledProperty, nameof(ProductsViewModel.CanRefresh));
        save.Clicked += async (_, _) => await viewModel.SaveServerAddressAsync(PageToken);

        var refreshButton = AppViews.PrimaryButton("Opdater")
            .Accessible(UiAutomationIds.ProductsRefresh, "Opdater produkter");
        refreshButton.SetBinding(IsEnabledProperty, nameof(ProductsViewModel.CanRefresh));
        refreshButton.Clicked += async (_, _) => await viewModel.LoadAsync(PageToken);

        var error = new Label { TextColor = GolfTheme.Colors.DangerText }
            .Accessible(UiAutomationIds.ProductsError, "Produkter kunne ikke hentes");
        error.SetBinding(Label.TextProperty, nameof(ProductsViewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(ProductsViewModel.HasError));

        var loading = new Label { Text = "Henter produkter…", TextColor = GolfTheme.Colors.MutedText };
        loading.SetBinding(IsVisibleProperty, nameof(ProductsViewModel.IsBusy));

        var empty = new Label { Text = "Der er ingen produkter endnu.", TextColor = GolfTheme.Colors.MutedText }
            .Accessible(UiAutomationIds.ProductsEmpty, "Ingen produkter");
        empty.SetBinding(IsVisibleProperty, nameof(ProductsViewModel.IsEmpty));

        var products = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label
                {
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GolfTheme.Colors.Text
                };
                name.SetBinding(Label.TextProperty, nameof(ProductDto.Name));
                return AppViews.Card(name, new Thickness(0, 0, 0, 10));
            })
        }.Accessible(UiAutomationIds.ProductsList, "Produktliste");
        products.SetBinding(ItemsView.ItemsSourceProperty, nameof(ProductsViewModel.Products));

        var refresh = new RefreshView { Content = products, RefreshColor = GolfTheme.Colors.PrimaryGreen };
        refresh.SetBinding(RefreshView.IsRefreshingProperty, nameof(ProductsViewModel.IsBusy), BindingMode.OneWay);
        refresh.Refreshing += async (_, _) =>
        {
            if (!viewModel.IsBusy) await viewModel.LoadAsync(PageToken);
        };

        Content = new Grid
        {
            Padding = 16,
            RowSpacing = 14,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        AppViews.PageTitle("Produkter"),
                        AppViews.Card(new VerticalStackLayout
                        {
                            Spacing = 6,
                            Children = { AppViews.FieldLabel("Serveradresse"), serverAddress, save }
                        }),
                        refreshButton
                    }
                }.Row(0),
                new VerticalStackLayout { Spacing = 6, Children = { loading, error, empty } }.Row(1),
                refresh.Row(2)
            }
        };
        if (!viewModel.CanConfigureServer)
        {
            var serverControls = (VerticalStackLayout)serverAddress.Parent;
            var card = (Border)serverControls.Parent;
            ((VerticalStackLayout)card.Parent).Children.Remove(card);
        }
    }

    private CancellationToken PageToken => pageCancellation?.Token ?? new CancellationToken(canceled: true);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = new CancellationTokenSource();
        await viewModel.LoadAsync(pageCancellation.Token);
    }

    protected override void OnDisappearing()
    {
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = null;
        base.OnDisappearing();
    }
}
