using GolfSG.Application.Courses;
using GolfSG.Application.ViewModels;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Courses;

namespace GolfSG.Views;

public sealed class CoursePreviewPage : ContentPage
{
    private readonly CoursePracticeService service;
    private readonly CourseCommunityWorkflow? community;
    private PracticeCourse course;
    private readonly CourseMapView map = new("CoursePractice.CoursePreview.Map") { Editable = false, PictureHeight = 350 };
    private readonly Label heading = AppViews.PageTitle("", 26);
    private readonly Label summary = new() { FontSize = 14, TextColor = GolfTheme.Colors.MutedText };
    private readonly VerticalStackLayout shots = new() { Spacing = 8 };
    private readonly Label activeDescription = new() { FontSize = 14, TextColor = GolfTheme.Colors.WarningText, IsVisible = false };
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText, IsVisible = false };
    private readonly Button start = CoursePracticeViews.PrimaryButton("Start runde");
    private readonly Button retry = CoursePracticeViews.SecondaryButton("Prøv igen");
    private readonly VerticalStackLayout body = new() { Padding = 16, Spacing = 12 };
    private readonly VerticalStackLayout footer = new()
    {
        Padding = new Thickness(16, 8, 16, 16), Spacing = 6, BackgroundColor = GolfTheme.Colors.PageBackground
    };
    private bool busy, canStart;

    public CoursePreviewPage(CoursePracticeService service, PracticeCourse course, CourseCommunityWorkflow? community = null)
    {
        this.service = service; this.course = course; this.community = community;
        CoursePracticeViews.ConfigurePage(this);
        BackgroundColor = GolfTheme.Colors.PageBackground;
        SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.All;
        this.Accessible("CoursePractice.CoursePreview", "Se banens slag, før du starter runden");
        NavigationPage.SetHasBackButton(this, false);
        ToolbarItems.Add(new ToolbarItem { Text = "Tilbage", Command = new Command(async () => await RunAsync(() => Navigation.PopAsync())) });

        body.Children.Add(heading); body.Children.Add(summary); body.Children.Add(map);
        body.Children.Add(AppViews.PageTitle("Planlagte slag", 20)); body.Children.Add(shots);
        start.Accessible($"CoursePractice.Start-{course.Id}", $"Start en træningsrunde på banen {course.Name}");
        start.IsEnabled = false;
        start.Clicked += async (_, _) => await RunAsync(StartAsync);
        retry.IsVisible = false;
        retry.Clicked += async (_, _) => await RunAsync(ReloadAsync);
        footer.Children.Add(activeDescription); footer.Children.Add(error); footer.Children.Add(retry); footer.Children.Add(start);
        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { new ScrollView { Content = body }.Row(0), footer.Row(1) }
        };
        RenderCourse();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAsync(ReloadAsync);
        try
        {
            if (course.ImageFile == CoursePracticeImages.SampleFile) await CoursePracticeImages.EnsureSampleAsync();
            var path = CoursePracticeImages.PathFor(course.ImageFile);
            if (!File.Exists(path)) throw new FileNotFoundException();
            map.SetImage(path);
        }
        catch
        {
            error.Text = "Banebilledet kunne ikke åbnes. Du kan stadig se slagene på listen.";
            error.IsVisible = true;
        }
    }

    private async Task ReloadAsync()
    {
        canStart = false;
        var document = await service.LoadAsync();
        course = document.Courses.FirstOrDefault(item => item.Id == course.Id)
            ?? throw new InvalidOperationException("Banen findes ikke længere. Gå tilbage til banelisten.");
        SetActiveRound(document.Active is not null);
        RenderCourse();
    }

    private void SetActiveRound(bool hasActiveRound)
    {
        canStart = !hasActiveRound;
        activeDescription.IsVisible = hasActiveRound;
        activeDescription.Text = hasActiveRound ? "Fortsæt, afslut eller opgiv den igangværende runde fra banelisten, før du starter en ny." : "";
    }

    private void RenderCourse()
    {
        Title = $"{course.Name} · Beta";
        heading.Text = course.Name;
        summary.Text = $"{course.Shots.Count} {(course.Shots.Count == 1 ? "position" : "positioner")} · {course.Shots.Sum(shot => shot.Attempts)} slag";
        SemanticProperties.SetDescription(start, $"Start en træningsrunde på banen {course.Name}");
        map.ImageAspectRatio = course.ImageAspectRatio; map.SavedShots = course.Shots; map.Refresh();
        shots.Children.Clear();
        foreach (var (shot, index) in course.Shots.Select((shot, index) => (shot, index)))
        {
            var category = shot.Category switch
            {
                PracticeShotCategory.Putting => "Putting",
                PracticeShotCategory.Approach => "Approach",
                _ => "Omkring green"
            };
            shots.Children.Add(AppViews.Card(new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    AppViews.PageTitle($"{index + 1}. {shot.Name}", 18),
                    new Label { Text = $"{CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(course))} · {ShotLieLabels.Format(shot.Lie)} · {category}", FontSize = 14 },
                    new Label { Text = $"{shot.Attempts} slag fra denne position", FontSize = 14, TextColor = GolfTheme.Colors.MutedText }
                }
            }, padding: GolfTheme.Spacing.CompactCardPadding));
        }
    }

    private async Task StartAsync()
    {
        // Recheck immediately before starting; browsing the preview never creates a session.
        var document = await service.LoadAsync();
        if (document.Active is not null)
        {
            SetActiveRound(true);
            throw new InvalidOperationException("Fortsæt, afslut eller opgiv den igangværende træningsrunde først.");
        }
        if (!document.Courses.Any(item => item.Id == course.Id))
        {
            canStart = false;
            throw new InvalidOperationException("Banen findes ikke længere. Gå tilbage til banelisten.");
        }
        var session = await service.StartAsync(course);
        canStart = false;
        await Navigation.PushAsync(new CoursePlayPage(service, session, community));
        Navigation.RemovePage(this);
    }

    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; body.IsEnabled = footer.IsEnabled = false; start.IsEnabled = false;
        error.Text = ""; error.IsVisible = retry.IsVisible = false;
        try { await action(); }
        catch (Exception ex)
        {
            error.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Kunne ikke åbne banen eller starte runden. Prøv igen.";
            error.IsVisible = retry.IsVisible = true;
        }
        finally { busy = false; body.IsEnabled = footer.IsEnabled = true; start.IsEnabled = canStart; }
    }
}
