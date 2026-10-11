using GolfSG.Application.Accounts;
using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class LoginPage : ContentPage
{
    private readonly AccountViewModel viewModel;
    private readonly Func<Task>? enterApp;
    private readonly AccountRequestActions requests = new();
    private CancellationTokenSource? pageCancellation;
    private bool hasEnteredApp;

    public LoginPage(AccountViewModel viewModel, Func<Page>? openHistory = null, Func<Page>? openServerSettings = null,
        Func<Task>? enterApp = null)
    {
        this.viewModel = viewModel;
        this.enterApp = enterApp;
        BindingContext = viewModel;
        Title = "Login";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible(UiAutomationIds.LoginPage, "Login og opret konto");

        var title = AppViews.PageTitle("Log ind");
        title.SetBinding(Label.TextProperty, nameof(AccountViewModel.AuthenticationTitle));
        var username = AccountViews.TextEntry("Brugernavn").Accessible(UiAutomationIds.AccountUsername, "Brugernavn");
        username.SetBinding(Entry.TextProperty, nameof(AccountViewModel.Username));
        username.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        username.ReturnType = ReturnType.Next;
        var password = AccountViews.TextEntry("Adgangskode", isPassword: true).Accessible(UiAutomationIds.AccountPassword, "Adgangskode");
        password.SetBinding(Entry.TextProperty, nameof(AccountViewModel.Password));
        password.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        var confirmation = AccountViews.TextEntry("Gentag adgangskode", isPassword: true)
            .Accessible(UiAutomationIds.AccountConfirmPassword, "Gentag adgangskode");
        confirmation.SetBinding(Entry.TextProperty, nameof(AccountViewModel.ConfirmPassword));
        confirmation.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        username.Completed += (_, _) => password.Focus();
        var signup = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                AppViews.Field("Gentag adgangskode", confirmation),
                new Label
                {
                    Text = "Brugernavn: 3–50 tegn (a–z, tal, punktum, bindestreg eller understregning). Ny adgangskode: " + ApiPasswordPolicy.RegistrationDescription,
                    FontSize = 13, TextColor = GolfTheme.Colors.MutedText
                }
            }
        };
        signup.SetBinding(IsVisibleProperty, nameof(AccountViewModel.IsCreatingAccount));
        var submit = AppViews.PrimaryButton("Log ind").Accessible(UiAutomationIds.LoginSubmit, "Log ind eller opret konto");
        submit.SetBinding(Button.TextProperty, nameof(AccountViewModel.AuthenticationActionText));
        submit.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        submit.Clicked += async (_, _) => await requests.RunAsync(async token =>
        {
            await viewModel.SubmitCredentialsAsync(token);
            if (viewModel.IsSignedIn) await EnterAppAsync(token);
        }, PageToken);
        var switchMode = AppViews.SecondaryButton("Opret en ny konto").Accessible(UiAutomationIds.LoginSwitchMode, "Skift mellem login og opret konto");
        switchMode.SetBinding(Button.TextProperty, nameof(AccountViewModel.AuthenticationSwitchText));
        switchMode.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        switchMode.Clicked += (_, _) => viewModel.SwitchAuthenticationMode();
        var form = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children = { AppViews.Field("Brugernavn", username), AppViews.Field("Adgangskode", password), signup, submit, switchMode }
        });
        form.SetBinding(IsVisibleProperty, nameof(AccountViewModel.IsSignedOut));

        var identity = new Label { TextColor = GolfTheme.Colors.Text, FontAttributes = FontAttributes.Bold }
            .Accessible(UiAutomationIds.AccountIdentity, "Aktuel konto");
        identity.SetBinding(Label.TextProperty, nameof(AccountViewModel.CurrentIdentity));
        var history = AppViews.PrimaryButton("Åbn historik").Accessible(UiAutomationIds.LoginOpenHistory, "Åbn historik for at importere og synkronisere runder");
        history.IsVisible = openHistory is not null;
        history.Clicked += async (_, _) =>
        {
            if (openHistory is not null)
                await this.RunNavigationOnceAsync(() => Navigation.PushAsync(openHistory()));
        };
        var offline = AppViews.SecondaryButton("Fortsæt uden login")
            .Accessible(UiAutomationIds.LoginContinueOffline, "Åbn appen og spil offline uden login");
        offline.IsVisible = enterApp is not null;
        offline.Clicked += async (_, _) => await EnterAppAsync(PageToken);
        var signedIn = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                identity,
                new Label { Text = "Du kan nu importere runder fra en fil i Historik og synkronisere dem til din konto.", TextColor = GolfTheme.Colors.MutedText },
                history
            }
        });
        signedIn.SetBinding(IsVisibleProperty, nameof(AccountViewModel.IsSignedIn));
        var server = new Label { FontSize = 13, TextColor = GolfTheme.Colors.MutedText, LineBreakMode = LineBreakMode.WordWrap };
        server.SetBinding(Label.TextProperty, nameof(AccountViewModel.ServerAddress));
        var connection = new Label { FontSize = 13, TextColor = GolfTheme.Colors.Text, LineBreakMode = LineBreakMode.WordWrap }
            .Accessible(UiAutomationIds.AccountConnectionStatus, "Forbindelse til server og database");
        connection.SetBinding(Label.TextProperty, nameof(AccountViewModel.ConnectionStatusMessage));
        var retry = AppViews.SecondaryButton("Tjek forbindelse").Accessible(UiAutomationIds.AccountCheckConnection, "Tjek server og database");
        retry.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        retry.Clicked += async (_, _) => await requests.RunAsync(viewModel.CheckConnectionAsync, PageToken);
        var settings = AppViews.SecondaryButton("Konto og serverindstillinger").Accessible(UiAutomationIds.LoginServerSettings, "Åbn konto og serverindstillinger");
        settings.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanAct));
        settings.Clicked += async (_, _) =>
        {
            if (openServerSettings is not null)
                await this.RunNavigationOnceAsync(() => Navigation.PushAsync(openServerSettings()));
        };
        var error = new Label { TextColor = GolfTheme.Colors.DangerText, LineBreakMode = LineBreakMode.WordWrap }
            .Accessible(UiAutomationIds.AccountError, "Loginfejl");
        error.SetBinding(Label.TextProperty, nameof(AccountViewModel.ErrorMessage));
        error.SetBinding(IsVisibleProperty, nameof(AccountViewModel.HasError));
        var status = new Label { TextColor = GolfTheme.Colors.MutedText, LineBreakMode = LineBreakMode.WordWrap };
        status.SetBinding(Label.TextProperty, nameof(AccountViewModel.StatusMessage));
        var busy = new ActivityIndicator { Color = GolfTheme.Colors.PrimaryGreen };
        busy.SetBinding(ActivityIndicator.IsRunningProperty, nameof(AccountViewModel.IsBusy));
        busy.SetBinding(IsVisibleProperty, nameof(AccountViewModel.IsBusy));
        var requestWait = new Label { TextColor = GolfTheme.Colors.MutedText, LineBreakMode = LineBreakMode.WordWrap }
            .Accessible(UiAutomationIds.AccountRequestWait, "Serveren kan være i dvale");
        requestWait.SetBinding(Label.TextProperty, nameof(AccountViewModel.RequestWaitMessage));
        requestWait.SetBinding(IsVisibleProperty, nameof(AccountViewModel.HasRequestWaitMessage));
        var cancel = AppViews.SecondaryButton("Annuller")
            .Accessible(UiAutomationIds.AccountCancelRequest, "Annuller forespørgslen til serveren");
        cancel.SetBinding(IsVisibleProperty, nameof(AccountViewModel.CanCancelRequest));
        cancel.SetBinding(IsEnabledProperty, nameof(AccountViewModel.CanCancelRequest));
        cancel.Clicked += (_, _) => requests.Cancel();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20, Spacing = 16,
                Children =
                {
                    new Label
                    {
                        Text = "GolfSG", FontSize = 32, FontAttributes = FontAttributes.Bold,
                        TextColor = GolfTheme.Colors.PrimaryGreen, IsVisible = enterApp is not null
                    },
                    title,
                    new Label { Text = "Din GolfSG-konto samler dine synkroniserede runder. Du kan også spille og gemme offline.", TextColor = GolfTheme.Colors.MutedText },
                    form, busy, requestWait, cancel, error, status, signedIn, offline,
                    AppViews.Card(new VerticalStackLayout { Spacing = 8, Children = { AppViews.FieldLabel("Forbindelse"), server, connection, retry, settings } })
                }
            }
        };
    }

    private CancellationToken PageToken => pageCancellation?.Token ?? new CancellationToken(canceled: true);

    private async Task EnterAppAsync(CancellationToken token)
    {
        if (enterApp is null || token.IsCancellationRequested) return;
        await this.RunNavigationOnceAsync(async () =>
        {
            if (hasEnteredApp || token.IsCancellationRequested) return;
            hasEnteredApp = true;
            try { await enterApp(); }
            catch { hasEnteredApp = false; throw; }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        pageCancellation?.Cancel();
        pageCancellation?.Dispose();
        pageCancellation = new();
        var token = pageCancellation.Token;
        await viewModel.LoadAsync(token);
        if (viewModel.IsSignedIn) await EnterAppAsync(token);
    }

    protected override void OnDisappearing()
    {
        pageCancellation?.Cancel();
        viewModel.ClearCredentials();
        base.OnDisappearing();
    }
}
