using GolfSG.Core;
using GolfSG.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

public sealed class PuttingGamesPage : ContentPage
{
    private readonly IServiceProvider services;
    private readonly IActivePuttingGameRepository activeRepository;
    private readonly Button resumeButton = AppViews.PrimaryButton("Fortsæt putting-spil");
    private readonly Button discardButton = AppViews.SecondaryButton("Kassér igangværende spil");

    public PuttingGamesPage(IServiceProvider services)
    {
        this.services = services;
        activeRepository = services.GetRequiredService<IActivePuttingGameRepository>();
        resumeButton.IsVisible = discardButton.IsVisible = false;
        resumeButton.Clicked += async (_, _) => await OpenResumeAsync();
        discardButton.Clicked += async (_, _) => await this.RunActionOnceAsync(async () =>
        {
            try { await ConfirmReplaceAsync(); await RefreshResumeAsync(); }
            catch { await ShowStorageErrorAsync(); }
        });
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
                    resumeButton,
                    discardButton,
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
        await this.RunNavigationOnceAsync(async () =>
        {
            try
            {
                if (!await ConfirmReplaceAsync()) return;
                var page = services.GetRequiredService<PuttingGamePage>();
                page.Start(PuttingGame.LadderMode);
                await Navigation.PushAsync(page);
            }
            catch { await ShowStorageErrorAsync(); }
        });
    }

    private async Task OpenTourRoundAsync()
    {
        await this.RunNavigationOnceAsync(async () =>
        {
            try
            {
                if (!await ConfirmReplaceAsync()) return;
                var page = services.GetRequiredService<PuttingGamePage>();
                page.Start(PuttingGame.TourRoundMode);
                await Navigation.PushAsync(page);
            }
            catch { await ShowStorageErrorAsync(); }
        });
    }

    private async Task OpenBenchmarkAsync()
    {
        await this.RunNavigationOnceAsync(async () =>
        {
            try
            {
                if (await ConfirmReplaceAsync()) await Navigation.PushAsync(new PuttingBenchmarkPage(services));
            }
            catch { await ShowStorageErrorAsync(); }
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await RefreshResumeAsync(); }
        catch { await ShowStorageErrorAsync(); }
    }

    private async Task RefreshResumeAsync()
    {
        var session = await activeRepository.GetAsync();
        resumeButton.IsVisible = discardButton.IsVisible = session is not null;
        if (session is not null) resumeButton.Text = $"Fortsæt {session.Definition.DisplayName} · {session.CompletedPutts.Count}/{session.Distances.Count}";
    }

    private async Task<bool> ConfirmReplaceAsync()
    {
        if (await activeRepository.GetAsync() is null) return true;
        if (!await DisplayAlertAsync("Kassér igangværende spil?", "Fortsæt dit gemte spil, eller kassér det før et nyt spil.", "Kassér", "Annuller")) return false;
        await activeRepository.DeleteAsync();
        return true;
    }

    private async Task OpenResumeAsync() => await this.RunNavigationOnceAsync(async () =>
    {
        try
        {
            var session = await activeRepository.GetAsync();
            if (session is null) { await RefreshResumeAsync(); return; }
            var page = services.GetRequiredService<PuttingGamePage>();
            page.Resume(session);
            await Navigation.PushAsync(page);
        }
        catch { await ShowStorageErrorAsync(); }
    });

    private Task ShowStorageErrorAsync() => DisplayAlertAsync("Spillet kunne ikke indlæses eller gemmes", "Prøv igen, eller tjek lagring under Indstillinger.", "OK");

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
