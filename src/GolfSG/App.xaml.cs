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
        return new Window(new LoadingPage(appShell));
    }
}
