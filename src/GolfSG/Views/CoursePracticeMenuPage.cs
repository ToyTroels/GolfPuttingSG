using GolfSG.Application.Courses;
using GolfSG.Infrastructure.Courses;

namespace GolfSG.Views;

public sealed class CoursePracticeMenuPage : ContentPage
{
    private readonly CoursePracticeService service;
    private readonly CourseCommunityWorkflow? community;
    private readonly VerticalStackLayout layout = new() { Padding = 16, Spacing = 14 };
    private readonly Label activeDescription = new() { TextColor = GolfTheme.Colors.Text };
    private readonly Border activeCard;
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText };
    private readonly Button retry = CoursePracticeViews.SecondaryButton("Prøv at indlæse igen");
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

        var resume = CoursePracticeViews.PrimaryButton("Fortsæt træningsrunde").Accessible("CoursePractice.Menu.Resume", "Fortsæt den igangværende træningsrunde");
        var discard = CoursePracticeViews.SecondaryButton("Opgiv træningsrunde").Accessible("CoursePractice.Menu.Discard", "Opgiv den igangværende træningsrunde");
        resume.Clicked += async (_, _) => await RunAsync(ResumeAsync);
        discard.Clicked += async (_, _) => await RunAsync(DiscardAsync);
        activeCard = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children = { AppViews.PageTitle("Igangværende runde", 20), activeDescription, resume, discard }
        });
        activeCard.IsVisible = false;

        var play = CoursePracticeViews.PrimaryButton("Spil bane").Accessible("CoursePractice.Choose", "Vælg en gemt bane at spille");
        play.Clicked += async (_, _) => await RunAsync(() => Navigation.PushAsync(new CourseSelectionPage(service, community)));
        var create = CoursePracticeViews.SecondaryButton("Opsæt ny bane").Accessible("CoursePractice.Create", "Opsæt en ny bane på et billede");
        create.Clicked += async (_, _) => await RunAsync(() => CourseSetupPage.OpenNewAsync(this, service));
        retry.IsVisible = false;
        retry.Clicked += async (_, _) => await RunAsync(ReloadAsync);

        layout.Children.Add(AppViews.PageTitle("Banerunde på billede"));
        layout.Children.Add(new Label
        {
            Text = "BETA · Spil en gemt bane, eller opsæt en ny bane med startpositioner og mål på et billede.",
            TextColor = GolfTheme.Colors.MutedText
        });
        layout.Children.Add(activeCard);
        layout.Children.Add(AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children = { new Label { Text = "Vælg mellem dine gemte baner, og spil det planlagte antal slag fra hver position.", TextColor = GolfTheme.Colors.Text }, play }
        }));
        layout.Children.Add(AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children = { new Label { Text = "Giv banen et navn, og placér slagene på det viste banebillede til senere runder.", TextColor = GolfTheme.Colors.Text }, create }
        }));
        if (community is not null)
        {
            var shared = new VerticalStackLayout { Spacing = 10 };
            shared.Children.Add(AppViews.PageTitle("Baner og spil med andre", 20));
            shared.Children.Add(new Label { Text = "Log ind for at hente og offentliggøre baner eller deltage i et spil med kode. Gemte baner og spil kan fortsættes offline.", TextColor = GolfTheme.Colors.MutedText });
            foreach (var (text, id, page) in new (string, string, Func<Page>)[]
            {
                ("Banebibliotek", "CoursePractice.Library", () => new CourseLibraryPage(community)),
                ("Opret spil", "CoursePractice.CreateGame", () => new CourseGameSetupPage(community, service)),
                ("Deltag med kode", "CoursePractice.JoinGame", () => new CourseGameJoinPage(community, service)),
                ("Mine spil", "CoursePractice.MyGames", () => new MyCourseGamesPage(community, service))
            })
            {
                var button = CoursePracticeViews.SecondaryButton(text).Accessible(id, text);
                button.Clicked += async (_, _) => await RunAsync(() => Navigation.PushAsync(page()));
                shared.Children.Add(button);
            }
            layout.Children.Add(AppViews.Card(shared));
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
        if (document.Active is { } active)
            activeDescription.Text = active.IsComplete
                ? $"{active.Course.Name} · Alle {active.TotalAttempts} slag er registreret. Fortsæt for at gemme resultatet."
                : $"{active.Course.Name} · {active.Results.Count}/{active.TotalAttempts} slag registreret. Fortsæt eller opgiv runden, før du starter en ny.";
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

    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; layout.IsEnabled = false; error.Text = ""; retry.IsVisible = false;
        try { await action(); }
        catch (Exception ex)
        {
            error.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Kunne ikke indlæse eller åbne banerunden. Prøv igen; dine data er bevaret.";
            retry.IsVisible = true;
        }
        finally { busy = false; layout.IsEnabled = true; }
    }
}
