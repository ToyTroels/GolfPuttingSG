using GolfSG.Application.Courses;
using GolfSG.Infrastructure.Courses;

namespace GolfSG.Views;

public sealed class CoursePracticeMenuPage : ContentPage
{
    private readonly CoursePracticeService service;
    private readonly CourseCommunityWorkflow? community;
    private readonly VerticalStackLayout layout = new() { Padding = new Thickness(16, 10, 16, 16), Spacing = 10 };
    private readonly Label activeDescription = new() { TextColor = GolfTheme.Colors.Text, FontSize = 14 };
    private readonly Border activeCard;
    private readonly Button finish = CompactButton("Afslut og gem");
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText, FontSize = 14, IsVisible = false };
    private readonly Button retry = CompactButton("Prøv at indlæse igen");
    private bool busy;

    public CoursePracticeMenuPage(CoursePracticeService service, CourseCommunityWorkflow? community = null)
    {
        this.service = service;
        this.community = community;
        CoursePracticeViews.ConfigurePage(this);
        Title = "Banerunde · Beta";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible("CoursePractice.Menu", "Menu for banerunde på billede, beta");
        NavigationPage.SetHasBackButton(this, false);
        ToolbarItems.Add(new ToolbarItem { Text = "Tilbage", Command = new Command(async () => await RunAsync(() => Navigation.PopAsync())) });

        var resume = CompactButton("Fortsæt træningsrunde", primary: true).Accessible("CoursePractice.Menu.Resume", "Fortsæt den igangværende træningsrunde");
        var discard = CompactButton("Opgiv træningsrunde").Accessible("CoursePractice.Menu.Discard", "Opgiv den igangværende træningsrunde");
        resume.Clicked += async (_, _) => await RunAsync(ResumeAsync);
        discard.Clicked += async (_, _) => await RunAsync(DiscardAsync);
        finish.Accessible("CoursePractice.Menu.Finish", "Afslut runden og gem de registrerede slag");
        finish.Clicked += async (_, _) => await RunAsync(FinishAsync);
        activeCard = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children = { AppViews.PageTitle("Igangværende runde", 18), activeDescription, resume, finish, discard }
        }, padding: GolfTheme.Spacing.CompactCardPadding);
        activeCard.IsVisible = false;

        var play = CompactButton("Spil bane", primary: true).Accessible("CoursePractice.Choose", "Vælg en gemt bane at spille");
        play.Clicked += async (_, _) => await RunAsync(() => Navigation.PushAsync(new CourseSelectionPage(service, community)));
        var create = CompactButton("Opsæt ny bane").Accessible("CoursePractice.Create", "Opsæt en ny bane på et billede");
        create.Clicked += async (_, _) => await RunAsync(() => CourseSetupPage.OpenNewAsync(this, service));
        retry.IsVisible = false;
        retry.Clicked += async (_, _) => await RunAsync(ReloadAsync);

        layout.Children.Add(AppViews.PageTitle("Banerunde på billede", 24));
        layout.Children.Add(new Label
        {
            Text = "Spil en gemt bane, eller placér slag på en ny.",
            TextColor = GolfTheme.Colors.MutedText, FontSize = 14
        });
        layout.Children.Add(activeCard);
        layout.Children.Add(AppViews.Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Dine baner · Kan spilles offline", TextColor = GolfTheme.Colors.MutedText, FontSize = 14 },
                play, create
            }
        }, padding: GolfTheme.Spacing.CompactCardPadding));
        if (community is not null)
        {
            var shared = new VerticalStackLayout { Spacing = 8 };
            shared.Children.Add(AppViews.PageTitle("Baner og spil med andre", 18));
            shared.Children.Add(new Label { Text = "Log ind for at dele baner og spille med andre.", TextColor = GolfTheme.Colors.MutedText, FontSize = 14 });
            var actions = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
                ColumnSpacing = 8, RowSpacing = 8
            };
            var actionIndex = 0;
            foreach (var (text, id, page) in new (string, string, Func<Page>)[]
            {
                ("Banebibliotek", "CoursePractice.Library", () => new CourseLibraryPage(community)),
                ("Opret spil", "CoursePractice.CreateGame", () => new CourseGameSetupPage(community, service)),
                ("Deltag med kode", "CoursePractice.JoinGame", () => new CourseGameJoinPage(community, service)),
                ("Mine spil", "CoursePractice.MyGames", () => new MyCourseGamesPage(community, service))
            })
            {
                var button = CompactButton(text).Accessible(id, text);
                button.Clicked += async (_, _) => await RunAsync(() => Navigation.PushAsync(page()));
                actions.Children.Add(button.Row(actionIndex / 2).Column(actionIndex % 2));
                actionIndex++;
            }
            shared.Children.Add(actions);
            layout.Children.Add(AppViews.Card(shared, padding: GolfTheme.Spacing.CompactCardPadding));
        }
        layout.Children.Add(error);
        layout.Children.Add(retry);
        Content = new ScrollView { Content = layout };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAsync(ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        var document = await service.LoadAsync();
        activeCard.IsVisible = document.Active is not null;
        finish.IsVisible = document.Active is { Results.Count: > 0 } activeRound && (activeRound.Game is null || activeRound.IsComplete);
        finish.Text = document.Active?.IsComplete == true ? "Gem rundens resultat" : "Afslut og gem";
        if (document.Active is { } active)
            activeDescription.Text = active.IsComplete
                ? $"{active.Course.Name} · Alle {active.TotalAttempts} slag er registreret. Fortsæt for at gemme resultatet."
                : $"{active.Course.Name} · {active.Results.Count}/{active.TotalAttempts} slag registreret.";
    }

    private async Task ResumeAsync()
    {
        var document = await service.LoadAsync();
        if (document.Active is not { } active) { await ReloadAsync(); return; }
        await Navigation.PushAsync(new CoursePlayPage(service, active, community));
    }

    private async Task DiscardAsync()
    {
        var document = await service.LoadAsync();
        if (document.Active is not { } active) { await ReloadAsync(); return; }
        if (!await DisplayAlertAsync("Opgiv træningsrunde?", "De registrerede resultater slettes. Den gemte bane bevares.", "Opgiv", "Annuller")) return;
        await service.DiscardAsync(active.Id);
        await ReloadAsync();
    }

    private async Task FinishAsync()
    {
        var document = await service.LoadAsync();
        if (document.Active is not { } active) { await ReloadAsync(); return; }
        if (!active.IsComplete && !await DisplayAlertAsync("Afslut runden?",
            $"Gem de {active.Results.Count} registrerede slag og spring resten over. En ikke registreret indtastning gemmes ikke som et resultat.",
            "Afslut og gem", "Annuller")) return;
        var round = await service.FinishEarlyAsync(active.Id);
        await ReloadAsync();
        await Navigation.PushAsync(new CoursePracticeResultPage(round, community));
    }

    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();

    private static Button CompactButton(string text, bool primary = false)
    {
        var button = primary ? CoursePracticeViews.PrimaryButton(text) : CoursePracticeViews.SecondaryButton(text);
        button.FontSize = 14;
        button.MinimumHeightRequest = 44;
        button.Padding = new Thickness(8, 6);
        button.BorderWidth = primary ? 0 : 1;
        return button;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; layout.IsEnabled = false; error.Text = ""; error.IsVisible = false; retry.IsVisible = false;
        try { await action(); }
        catch (Exception ex)
        {
            error.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Kunne ikke indlæse eller åbne banerunden. Prøv igen; dine data er bevaret.";
            error.IsVisible = true;
            retry.IsVisible = true;
        }
        finally { busy = false; layout.IsEnabled = true; }
    }
}
