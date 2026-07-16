using GolfSG.Views;

namespace GolfSG;

public sealed class LoadingPage : ContentPage
{
    private readonly AppShell appShell;
    private bool hasNavigated;

    public LoadingPage(AppShell appShell)
    {
        this.appShell = appShell;
        BackgroundColor = GolfTheme.Colors.LoadingBackground;
        Shell.SetNavBarIsVisible(this, false);

        Content = new Grid
        {
            Children =
            {
                new Image
                {
                    Source = "loading_screen.png",
                    Aspect = Aspect.AspectFill,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (hasNavigated)
        {
            return;
        }

        hasNavigated = true;
        await Task.Delay(1200);

        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault() is Window window)
        {
            window.Page = appShell;
        }
    }
}
