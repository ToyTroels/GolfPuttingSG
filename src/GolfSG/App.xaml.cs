using GolfSG.Views;

namespace GolfSG;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AppShell appShell;

    public App(AppShell appShell)
    {
        this.appShell = appShell;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new LoadingPage(appShell));
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
