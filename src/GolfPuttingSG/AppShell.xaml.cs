using GolfPuttingSG.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GolfPuttingSG;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        Items.Add(new ShellContent
        {
            Title = "Putting SG",
            Content = services.GetRequiredService<StartPage>()
        });
    }
}
