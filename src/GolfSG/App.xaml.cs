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
        window.Stopped += async (_, _) =>
        {
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
