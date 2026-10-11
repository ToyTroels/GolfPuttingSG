using GolfSG.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

public sealed class AccountPage : ContentPage
{
    private readonly AccountViewModel viewModel;
    private readonly AccountRequestActions requests = new();
    private CancellationTokenSource? pageCancellation;

    public AccountPage(AccountViewModel viewModel, IServiceProvider? services = null, Func<Task>? openLogin = null)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Konto";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible(UiAutomationIds.AccountPage, "Konto og server");

        var server = AccountViews.TextEntry("https://localhost/", Keyboard.Url)
            .Accessible(UiAutomationIds.AccountServer, "HTTPS-serveradresse");
        server.SetBinding(Entry.TextProperty, nameof(AccountViewModel.ServerAddress));
        server.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        var identity = new Label { TextColor = GolfTheme.Colors.Text, LineBreakMode = LineBreakMode.WordWrap }
            .Accessible(UiAutomationIds.AccountIdentity, "Aktuel konto");
        identity.SetBinding(Label.TextProperty, nameof(AccountViewModel.CurrentIdentity));
        var error = new Label { TextColor = GolfTheme.Colors.DangerText }
            .Accessible(UiAutomationIds.AccountError, "Kontofejl");
        error.SetBinding(Label.TextProperty, nameof(AccountViewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(AccountViewModel.HasError));
        var status = new Label { TextColor = GolfTheme.Colors.MutedText };
        status.SetBinding(Label.TextProperty, nameof(AccountViewModel.StatusMessage));
        var connectionStatus = new Label { TextColor = GolfTheme.Colors.Text, LineBreakMode = LineBreakMode.WordWrap }
            .Accessible(UiAutomationIds.AccountConnectionStatus, "Forbindelse til server og database");
        connectionStatus.SetBinding(Label.TextProperty, nameof(AccountViewModel.ConnectionStatusMessage));
        var busy = new ActivityIndicator { Color = GolfTheme.Colors.PrimaryGreen };
        busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(AccountViewModel.IsBusy));
        busy.SetBinding(IsVisibleProperty, nameof(AccountViewModel.IsBusy));
        var save = ActionButton("Gem serveradresse", UiAutomationIds.AccountSaveServer, () => viewModel.SaveServerAddressAsync(PageToken));
        var checkConnection = ActionButton("Tjek forbindelse", UiAutomationIds.AccountCheckConnection,
            () => requests.RunAsync(viewModel.CheckConnectionAsync, PageToken));
        var requestWait = new Label { TextColor = GolfTheme.Colors.MutedText, LineBreakMode = LineBreakMode.WordWrap }
            .Accessible(UiAutomationIds.AccountRequestWait, "Serveren kan være i dvale");
        requestWait.SetBinding(Label.TextProperty, nameof(AccountViewModel.RequestWaitMessage));
        requestWait.SetBinding(IsVisibleProperty, nameof(AccountViewModel.HasRequestWaitMessage));
        var cancel = AppViews.SecondaryButton("Annuller")
            .Accessible(UiAutomationIds.AccountCancelRequest, "Annuller forespørgslen til serveren");
        cancel.SetBinding(IsVisibleProperty, nameof(AccountViewModel.CanCancelRequest));
        cancel.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanCancelRequest));
        cancel.Clicked += (_, _) => requests.Cancel();
        var login = AppViews.PrimaryButton("Log ind / Opret konto").Accessible(UiAutomationIds.AccountOpenLogin, "Åbn login og opret konto");
        login.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        login.SetBinding(IsVisibleProperty, nameof(AccountViewModel.IsSignedOut));
        login.Clicked += async (_, _) =>
        {
            if (openLogin is not null)
                await this.RunNavigationOnceAsync(openLogin);
            else if (services is not null)
                await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<LoginPage>()));
        };
        var signOut = ActionButton("Log ud", UiAutomationIds.AccountSignOut, () => viewModel.SignOutAsync(PageToken), nameof(AccountViewModel.CanSignOut));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 14,
                Children =
                {
                    AppViews.PageTitle("Konto og server"),
                    AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { identity,
                        new Label { Text = "Du kan spille og gemme offline. Login bruges til manuel synkronisering af runder.", TextColor = GolfTheme.Colors.MutedText }, login, signOut } }),
                    AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { AppViews.FieldLabel("Serveradresse"), server, save, checkConnection, connectionStatus } }),
                    new Label { Text = "Denne installations historik knyttes til den første konto og server, du logger ind på. Tilknytningen bevares ved logout. Brug en separat installation til en anden konto.", TextColor = GolfTheme.Colors.MutedText },
                    new Label { Text = "Forbindelsen testes kun, når du vælger Tjek forbindelse. Appen kan bruges offline, mens serveren er i dvale.", TextColor = GolfTheme.Colors.MutedText },
                    busy, requestWait, cancel, error, status
                }
            }
        };
    }

    private Button ActionButton(string title, string id, Func<Task> action, string enabled = nameof(AccountViewModel.CanAct))
    {
        var button = AppViews.PrimaryButton(title).Accessible(id, title);
        button.SetBinding(IsEnabledProperty, enabled);
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private CancellationToken PageToken => pageCancellation?.Token ?? new CancellationToken(canceled: true);
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = new();
        await viewModel.LoadAsync(PageToken);
    }
    protected override void OnDisappearing()
    {
        pageCancellation?.Cancel();
        viewModel.ClearCredentials();
        base.OnDisappearing();
    }
}
