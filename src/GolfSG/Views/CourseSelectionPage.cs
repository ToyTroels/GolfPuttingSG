using GolfSG.Application.Courses;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Infrastructure.Courses;

namespace GolfSG.Views;

public sealed class CourseSelectionPage : ContentPage
{
    private readonly CoursePracticeService service;
    private readonly CourseCommunityWorkflow? community;
    private readonly VerticalStackLayout layout = new() { Padding = 16, Spacing = 12 };
    private readonly VerticalStackLayout courseList = new() { Spacing = 10 };
    private readonly Label activeDescription = new() { TextColor = GolfTheme.Colors.Text };
    private readonly Border activeCard;
    private readonly Button finish = CompactButton("Afslut og gem");
    private readonly Border emptyCard;
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText };
    private readonly Button retry = CoursePracticeViews.SecondaryButton("Prøv at indlæse igen");
    private bool busy;

    public CourseSelectionPage(CoursePracticeService service, CourseCommunityWorkflow? community = null)
    {
        this.service = service;
        this.community = community;
        CoursePracticeViews.ConfigurePage(this);
        Title = "Spil bane · Beta";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible("CoursePractice.Courses", "Vælg en gemt bane at spille, beta");
        NavigationPage.SetHasBackButton(this, false);
        ToolbarItems.Add(new ToolbarItem { Text = "Tilbage", Command = new Command(async () => await RunAsync(() => Navigation.PopAsync())) });

        var resume = CoursePracticeViews.PrimaryButton("Fortsæt træningsrunde").Accessible("CoursePractice.Courses.Resume", "Fortsæt den igangværende træningsrunde");
        var discard = CoursePracticeViews.SecondaryButton("Opgiv træningsrunde").Accessible("CoursePractice.Courses.Discard", "Opgiv den igangværende træningsrunde");
        resume.Clicked += async (_, _) => await RunAsync(ResumeAsync);
        discard.Clicked += async (_, _) => await RunAsync(DiscardAsync);
        finish.Accessible("CoursePractice.Courses.Finish", "Afslut runden og gem de registrerede slag");
        finish.Clicked += async (_, _) => await RunAsync(FinishAsync);
        activeCard = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children = { AppViews.PageTitle("Igangværende runde", 20), activeDescription, resume, finish, discard }
        });
        activeCard.IsVisible = false;

        var create = CoursePracticeViews.PrimaryButton("Opsæt ny bane").Accessible("CoursePractice.Courses.Create", "Opsæt din første bane på et billede");
        create.Clicked += async (_, _) => await RunAsync(() => CourseSetupPage.OpenNewAsync(this, service));
        emptyCard = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children = { new Label { Text = "Du har ingen gemte baner endnu. Opsæt en bane på et billede, og gem den for at spille den her.", TextColor = GolfTheme.Colors.Text }, create }
        });
        emptyCard.IsVisible = false;
        retry.IsVisible = false;
        retry.Clicked += async (_, _) => await RunAsync(ReloadAsync);

        layout.Children.Add(AppViews.PageTitle("Vælg en bane", 26));
        layout.Children.Add(new Label { Text = "Gemte baner kan spilles offline.", TextColor = GolfTheme.Colors.MutedText, FontSize = 14 });
        layout.Children.Add(activeCard);
        var import = CompactButton("Importér bane fra fil").Accessible("CoursePractice.Courses.Import", "Importér en delt bane med billede");
        import.Clicked += async (_, _) => await RunAsync(ImportAsync);
        layout.Children.Add(import);
        layout.Children.Add(emptyCard);
        layout.Children.Add(courseList);
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
        emptyCard.IsVisible = document.Courses.Count == 0;
        courseList.Children.Clear();
        foreach (var course in document.Courses)
        {
            var menu = CompactButton("☰").Accessible($"CoursePractice.CourseMenu-{course.Id}", $"Flere muligheder for banen {course.Name}",
                community is null ? "Rediger eller del banen" : "Rediger, del eller offentliggør banen");
            menu.WidthRequest = 44;
            menu.FontSize = 20;
            menu.FontAttributes = FontAttributes.None;
            menu.Padding = 0;
            menu.BorderWidth = 0;
            menu.BackgroundColor = Colors.Transparent;
            menu.VerticalOptions = LayoutOptions.Start;
            menu.Clicked += async (_, _) => await RunAsync(() => ShowCourseMenuAsync(course.Id));
            var name = AppViews.PageTitle($"{course.Name} ›", 18);
            name.VerticalOptions = LayoutOptions.Center;
            var preview = new VerticalStackLayout
            {
                Spacing = 4, MinimumHeightRequest = 44, VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    name,
                    new Label
                    {
                        Text = $"{course.Shots.Count} {(course.Shots.Count == 1 ? "position" : "positioner")} · {course.Shots.Sum(shot => shot.Attempts)} slag",
                        TextColor = GolfTheme.Colors.MutedText, FontSize = 14
                    }
                }
            };
            preview.Accessible($"CoursePractice.Preview-{course.Id}", $"Se banen {course.Name} og dens slag", "Åbner en forhåndsvisning. Runden starter først, når du vælger Start runde.");
            preview.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await RunAsync(() => PreviewAsync(course.Id)))
            });
            var card = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 8,
                Children = { preview.Column(0), menu.Column(1) }
            };
            courseList.Children.Add(AppViews.Card(card, padding: GolfTheme.Spacing.CompactCardPadding));
        }
    }

    private async Task ShowCourseMenuAsync(string courseId)
    {
        var course = FindCourse(await service.LoadAsync(), courseId);
        string[] actions = community is null
            ? ["Rediger bane", "Del bane"]
            : ["Rediger bane", "Del bane", "Offentliggør bane"];
        var choice = await DisplayActionSheetAsync(course.Name, "Annuller", null, actions);
        switch (choice)
        {
            case "Rediger bane":
                await EditAsync(courseId);
                break;
            case "Del bane":
                await ShareAsync(courseId);
                break;
            case "Offentliggør bane" when community is not null:
                course = FindCourse(await service.LoadAsync(), courseId);
                await Navigation.PushAsync(new CoursePublishPage(community, course));
                break;
        }
    }

    private static Button CompactButton(string text)
    {
        var button = CoursePracticeViews.SecondaryButton(text);
        button.FontSize = 14;
        button.MinimumHeightRequest = 44;
        button.Padding = new Thickness(10, 6);
        button.BorderWidth = 1;
        return button;
    }

    private async Task PreviewAsync(string courseId)
    {
        var document = await service.LoadAsync();
        var course = FindCourse(document, courseId);
        await Navigation.PushAsync(new CoursePreviewPage(service, course, community));
    }

    private CourseShareService Sharing() => new(service, Path.GetDirectoryName(CoursePracticeImages.PathFor(CoursePracticeImages.SampleFile))!);

    private async Task ShareAsync(string courseId)
    {
        var course = FindCourse(await service.LoadAsync(), courseId);
        if (course.ImageFile == CoursePracticeImages.SampleFile) await CoursePracticeImages.EnsureSampleAsync();
        var path = Path.Combine(FileSystem.CacheDirectory, "golfsg-course-" + Guid.NewGuid().ToString("N") + ".golfsgcourse");
        await using (var output = File.Create(path)) await Sharing().ExportAsync(course, output);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = $"Del {course.Name}", File = new ShareFile(path, "application/json") });
    }

    private async Task ImportAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Vælg en GolfSG-banefil (.golfsgcourse)" });
        if (file is null) return;
        await using var input = await file.OpenReadAsync();
        var course = await Sharing().ImportAsync(input);
        await ReloadAsync();
        await DisplayAlertAsync("Bane gemt", $"{course.Name} er gemt på denne enhed og kan spilles offline. En allerede importeret bane bevares med dine ændringer.", "OK");
    }

    private async Task EditAsync(string courseId)
    {
        var document = await service.LoadAsync();
        var course = FindCourse(document, courseId);
        var page = new CourseSetupPage(service);
        page.EditCourse(course);
        await Navigation.PushAsync(page);
    }

    private static PracticeCourse FindCourse(CoursePracticeDocument document, string courseId) =>
        document.Courses.FirstOrDefault(c => c.Id == courseId) ?? throw new InvalidOperationException("Banen findes ikke længere. Indlæs listen igen.");

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

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; layout.IsEnabled = false; error.Text = ""; retry.IsVisible = false;
        try { await action(); }
        catch (Exception ex)
        {
            error.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Kunne ikke indlæse, dele eller gemme banen. Prøv igen; dine data er bevaret.";
            retry.IsVisible = true;
        }
        finally { busy = false; layout.IsEnabled = true; }
    }
}
