using GolfSG.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

public sealed class AccountPage : ContentPage
{
    private readonly AccountViewModel viewModel;
    private readonly AccountRequestActions requests = new();
    private readonly Func<string, Task<bool>>? confirmServerSwitch;
    private CancellationTokenSource? pageCancellation;

    public AccountPage(AccountViewModel viewModel, IServiceProvider? services = null, Func<Task>? openLogin = null,
        Func<string, Task<bool>>? confirmServerSwitch = null)
    {
        this.viewModel = viewModel;
        this.confirmServerSwitch = confirmServerSwitch;
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
        var save = ActionButton("Gem serveradresse", UiAutomationIds.AccountSaveServer, SaveServerAsync);
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
                    AppViews.PageTitle(viewModel.CanConfigureServer ? "Konto og server" : "Konto"),
                    AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { identity,
                        new Label { Text = "Du kan spille og gemme offline. Login bruges til manuel synkronisering af runder.", TextColor = GolfTheme.Colors.MutedText }, login, signOut } }),
                    AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { AppViews.FieldLabel("Serveradresse"), server, save, checkConnection, connectionStatus } }),
                    new Label { Text = "Skifter du server, bliver du logget ud. Dine lokale runder bevares og kan uploades til din nye konto via Historik → Synkroniser. Logout alene bevarer tilknytningen til den nuværende konto.", TextColor = GolfTheme.Colors.MutedText },
                    new Label { Text = "Forbindelsen testes kun, når du vælger Tjek forbindelse. Appen kan bruges offline, mens serveren er i dvale.", TextColor = GolfTheme.Colors.MutedText },
                    busy, requestWait, cancel, error, status
                }
            }
        };
        if (!viewModel.CanConfigureServer)
        {
            var layout = (VerticalStackLayout)((ScrollView)Content).Content;
            // Release exposes account management and connection checks without endpoint controls.
            var serverControls = (VerticalStackLayout)server.Parent;
            serverControls.Children.RemoveAt(0); // Server address field label.
            serverControls.Children.Remove(server);
            serverControls.Children.Remove(save);
            layout.Children.RemoveAt(3); // Debug server-switch explanation.
        }
    }

    private async Task SaveServerAsync()
    {
        if (!viewModel.CanAct) return;
        if (!viewModel.RequiresServerSwitch)
        {
            await viewModel.SaveServerAddressAsync(PageToken);
            return;
        }
        var target = viewModel.ServerAddress;
        var confirmed = confirmServerSwitch is not null ? await confirmServerSwitch(target) : await DisplayAlertAsync("Skift server?",
            $"Skift til {target}? Du bliver logget ud. Dine lokale runder bevares og kan uploades til den konto, du logger ind på på den nye server. Gamle synkroniseringsoplysninger nulstilles. Der uploades ikke noget nu.",
            "Skift server", "Annuller");
        if (confirmed && !PageToken.IsCancellationRequested && target == viewModel.ServerAddress)
            await viewModel.SwitchServerAsync(PageToken);
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
