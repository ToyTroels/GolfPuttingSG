using GolfSG.Views;
using GolfSG.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell appShell;
    private readonly IServiceProvider services;

    public App(AppShell appShell, IServiceProvider services)
    {
        this.appShell = appShell;
        this.services = services;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window? startupWindow = null;
        LoginPage? login = null;
        login = new LoginPage(services.GetRequiredService<AccountViewModel>(),
            openServerSettings: () => new AccountPage(services.GetRequiredService<AccountViewModel>(), services,
                openLogin: () => login!.Navigation.PopAsync()),
            enterApp: () =>
            {
                if (startupWindow is not null) startupWindow.Page = appShell;
                return Task.CompletedTask;
            });
        NavigationPage.SetHasNavigationBar(login, false);
        var window = new Window(new NavigationPage(login)
        {
            BarBackgroundColor = GolfTheme.Colors.PageBackground,
            BarTextColor = GolfTheme.Colors.PrimaryGreen
        });
        startupWindow = window;
        GolfSG.Application.Services.UsageDiagnostics.StartSession();
        window.Resumed += (_, _) => GolfSG.Application.Services.UsageDiagnostics.StartSession();
        window.Destroying += (_, _) => GolfSG.Application.Services.UsageDiagnostics.EndSession();
        window.Stopped += async (_, _) =>
        {
            GolfSG.Application.Services.UsageDiagnostics.EndSession();
            var activeRoundPage = appShell.Navigation.NavigationStack
                .OfType<RoundInputPage>()
                .LastOrDefault();
            if (activeRoundPage is not null)
            {
                await activeRoundPage.FlushActiveRoundAsync();
            }
        };

        return window;
    }
}
