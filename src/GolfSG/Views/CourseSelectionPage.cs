using GolfSG.Application.Courses;
using GolfSG.Core.Models;

namespace GolfSG.Views;

public sealed class CourseSelectionPage : ContentPage
{
    private readonly CoursePracticeService service;
    private readonly VerticalStackLayout layout = new() { Padding = 16, Spacing = 14 };
    private readonly VerticalStackLayout courseList = new() { Spacing = 12 };
    private readonly Label activeDescription = new() { TextColor = GolfTheme.Colors.Text };
    private readonly Border activeCard;
    private readonly Border emptyCard;
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText };
    private readonly Button retry = CoursePracticeViews.SecondaryButton("Prøv at indlæse igen");
    private bool busy;

    public CourseSelectionPage(CoursePracticeService service)
    {
        this.service = service;
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
        activeCard = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children = { AppViews.PageTitle("Igangværende runde", 20), activeDescription, resume, discard }
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

        layout.Children.Add(AppViews.PageTitle("Vælg en bane"));
        layout.Children.Add(new Label { Text = "Spil en gemt bane, eller rediger dens planlagte slag før en ny runde.", TextColor = GolfTheme.Colors.MutedText });
        layout.Children.Add(activeCard);
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
        if (document.Active is { } active)
            activeDescription.Text = active.IsComplete
                ? $"{active.Course.Name} · Alle {active.TotalAttempts} slag er registreret. Fortsæt for at gemme resultatet."
                : $"{active.Course.Name} · {active.Results.Count}/{active.TotalAttempts} slag registreret. Fortsæt eller opgiv runden, før du starter en ny.";
        emptyCard.IsVisible = document.Courses.Count == 0;
        courseList.Children.Clear();
        foreach (var course in document.Courses)
        {
            var play = CoursePracticeViews.PrimaryButton("Spil bane").Accessible($"CoursePractice.Start-{course.Id}", $"Spil banen {course.Name}");
            play.IsEnabled = document.Active is null;
            play.Clicked += async (_, _) => await RunAsync(() => StartAsync(course.Id));
            var edit = CoursePracticeViews.SecondaryButton("Rediger bane").Accessible($"CoursePractice.Edit-{course.Id}", $"Rediger banen {course.Name}");
            edit.Clicked += async (_, _) => await RunAsync(() => EditAsync(course.Id));
            courseList.Children.Add(AppViews.Card(new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    AppViews.PageTitle(course.Name, 20),
                    new Label { Text = $"{course.Shots.Count} positioner · {course.Shots.Sum(shot => shot.Attempts)} planlagte slag", TextColor = GolfTheme.Colors.MutedText },
                    play,
                    edit
                }
            }));
        }
    }

    private async Task StartAsync(string courseId)
    {
        var document = await service.LoadAsync();
        if (document.Active is not null)
        {
            await ReloadAsync();
            throw new InvalidOperationException("Fortsæt eller opgiv den igangværende træningsrunde først.");
        }
        var course = FindCourse(document, courseId);
        var session = await service.StartAsync(course);
        await Navigation.PushAsync(new CoursePlayPage(service, session));
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
        await Navigation.PushAsync(new CoursePlayPage(service, active));
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
            error.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Kunne ikke indlæse eller åbne banen. Prøv igen; dine data er bevaret.";
            retry.IsVisible = true;
        }
        finally { busy = false; layout.IsEnabled = true; }
    }
}
