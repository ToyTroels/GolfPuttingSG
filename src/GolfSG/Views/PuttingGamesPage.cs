using GolfSG.Core;
using GolfSG.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

public sealed class PuttingGamesPage : ContentPage
{
    private readonly IServiceProvider services;

    public PuttingGamesPage(IServiceProvider services)
    {
        this.services = services;
        Title = "Putting-spil";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        BuildLayout();
    }

    private void BuildLayout()
    {
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 10,
                Children =
                {
                    AppViews.PageTitle("Putting-spil"),
                    GameItem(
                        "Træningsspil",
                        "Vælg antal putts og afstandsinterval.",
                        OpenConfiguredGameAsync),
                    GameItem(
                        "Tour-runde",
                        "18 putts fra faste tour-lignende afstande.",
                        OpenTourRoundAsync),
                    GameItem(
                        "Benchmark",
                        "Bell-curve test og beta ladder benchmarks.",
                        OpenBenchmarkAsync),
                    BetaGameItem(
                        "Benchmark historie beta",
                        "Se seneste, bedste og udvikling pr. benchmark.",
                        OpenBenchmarkHistoryAsync)
                }
            }
        };
    }

    private async Task OpenConfiguredGameAsync()
    {
        var page = services.GetRequiredService<PuttingGamePage>();
        page.Start(PuttingGame.LadderMode);
        await this.RunNavigationOnceAsync(() => Navigation.PushAsync(page));
    }

    private async Task OpenTourRoundAsync()
    {
        var page = services.GetRequiredService<PuttingGamePage>();
        page.Start(PuttingGame.TourRoundMode);
        await this.RunNavigationOnceAsync(() => Navigation.PushAsync(page));
    }

    private async Task OpenBenchmarkAsync()
    {
        await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new PuttingBenchmarkPage(services)));
    }

    private async Task OpenBenchmarkHistoryAsync()
    {
        await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<BenchmarkHistoryPage>()));
    }

    private static View BetaGameItem(string title, string subtitle, Func<Task> openAsync)
    {
        var item = GameItem(title, subtitle, openAsync);
        item.IsVisible = FeatureSettings.EnableBetaFeatures;
        return item;
    }

    private static View GameItem(string title, string subtitle, Func<Task> openAsync)
    {
        var card = AppViews.Card(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label
                        {
                            Text = title,
                            FontSize = 17,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = GolfTheme.Colors.Text
                        },
                        new Label
                        {
                            Text = subtitle,
                            FontSize = 13,
                            TextColor = GolfTheme.Colors.MutedText
                        }
                    }
                }.Column(0),
                new Label
                {
                    Text = ">",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GolfTheme.Colors.PrimaryGreen,
                    VerticalTextAlignment = TextAlignment.Center
                }.Column(1)
            }
        });

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await openAsync();
        card.GestureRecognizers.Add(tap);

        return card;
    }
}
