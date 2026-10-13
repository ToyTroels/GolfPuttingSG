using GolfSG.Application.Courses;
using GolfSG.Infrastructure.Courses;
using GolfSG.Core.Models;

namespace GolfSG.Views;

public sealed class CoursePublishPage : CourseCommunityPage
{
    private readonly PracticeCourse course;
    private readonly CourseMapView map = new() { Editable = false, PictureHeight = 350 };
    private readonly Label message = Text("");
    public CoursePublishPage(CourseCommunityWorkflow community, PracticeCourse course)
        : base(community, "Offentliggør bane", "CourseLibrary.Publish")
    {
        this.course = course;
        Body.Children.Add(AppViews.PageTitle(course.Name)); Body.Children.Add(map);
        Body.Children.Add(Text($"{course.Shots.Count} positioner · {course.Shots.Sum(s => s.Attempts)} slag"));
        Body.Children.Add(Text("Banens billede, opsætning og dit spillernavn bliver synlige for andre brugere i biblioteket. Dine resultater deles ikke. Senere offentliggørelse af denne bane opdaterer bibliotekets version; hentede kopier og eksisterende spil ændres ikke."));
        Body.Children.Add(Action("Offentliggør i biblioteket", "CourseLibrary.Publish.Confirm", () => RunAsync(async token =>
        {
            var package = await Community.PackageAsync(course, token);
            await Community.Api.PublishAsync(Session, package, token);
            message.Text = "Banen er offentliggjort. Du kan skjule den i Banebibliotek → Mine delte baner.";
        })));
        Body.Children.Add(message);
    }
    protected override async Task LoadAsync(CancellationToken token)
    {
        if (course.ImageFile == CoursePracticeImages.SampleFile) await CoursePracticeImages.EnsureSampleAsync(); token.ThrowIfCancellationRequested();
        map.ImageAspectRatio = course.ImageAspectRatio; map.SavedShots = course.Shots; map.Refresh();
        map.SetImage(CoursePracticeImages.PathFor(course.ImageFile));
    }
}

public sealed class CourseLibraryPage : CourseCommunityPage
{
    private readonly Entry search = new() { Placeholder = "Søg efter banens navn", MaxLength = 100 };
    private readonly CheckBox mine = new();
    private readonly VerticalStackLayout items = new() { Spacing = 12 };
    private readonly Button more;
    private int offset;
    private string currentSearch = "";
    private bool currentMine;

    public CourseLibraryPage(CourseCommunityWorkflow community) : base(community, "Banebibliotek", "CourseLibrary.Page")
    {
        Body.Children.Add(AppViews.PageTitle("Banebibliotek"));
        Body.Children.Add(Text("Hent en bane og spil offline. For at dele din egen bane skal du vælge Offentliggør bane under Spil bane."));
        search.Accessible("CourseLibrary.Search", "Søg efter bane"); mine.Accessible("CourseLibrary.Mine", "Vis mine offentliggjorte baner");
        Body.Children.Add(search);
        Body.Children.Add(new HorizontalStackLayout { Children = { mine, Text("Mine delte baner") } });
        Body.Children.Add(Action("Søg", "CourseLibrary.Find", () => RunAsync(LoadAsync)));
        Body.Children.Add(items);
        more = Action("Vis flere", "CourseLibrary.More", () => RunAsync(token => FetchAsync(false, token)));
        more.IsVisible = false; Body.Children.Add(more);
    }

    protected override Task LoadAsync(CancellationToken token) => FetchAsync(true, token);
    private async Task FetchAsync(bool reset, CancellationToken token)
    {
        if (reset) { offset = 0; currentSearch = search.Text ?? ""; currentMine = mine.IsChecked; }
        var courses = await Community.Api.BrowseAsync(Session, currentSearch, offset, currentMine, token);
        if (reset) items.Children.Clear();
        if (courses.Count == 0 && offset == 0) items.Children.Add(Text("Ingen baner fundet."));
        foreach (var course in courses)
        {
            var card = new VerticalStackLayout { Spacing = 8 };
            card.Children.Add(AppViews.PageTitle(course.Name, 20));
            card.Children.Add(Text($"Af {course.Creator} · {course.Positions} positioner · {course.Attempts} slag{(course.Published ? "" : " · Skjult fra biblioteket")}"));
            card.Children.Add(Action("Se bane", $"CourseLibrary.Open-{course.Id}", () => RunAsync(async ct =>
            {
                var package = await Community.Api.DownloadAsync(Session, course.Id, course.RevisionId, ct);
                await Navigation.PushAsync(new CourseLibraryPreviewPage(Community, course, package));
            })));
            items.Children.Add(AppViews.Card(card));
        }
        offset += courses.Count; more.IsVisible = courses.Count == CourseCommunityService.PageSize;
    }
}

public sealed class CourseLibraryPreviewPage : CourseCommunityPage
{
    private readonly LibraryCourse library;
    private readonly CoursePackage package;
    private readonly CourseMapView map = new() { Editable = false, PictureHeight = 400 };
    private readonly Label message = Text("");
    private readonly Button visibility;

    public CourseLibraryPreviewPage(CourseCommunityWorkflow community, LibraryCourse library, CoursePackage package)
        : base(community, library.Name, "CourseLibrary.Preview")
    {
        this.library = library; this.package = package;
        Body.Children.Add(AppViews.PageTitle(library.Name)); Body.Children.Add(Text($"Af {library.Creator}"));
        Body.Children.Add(map);
        foreach (var (shot, i) in package.Course.Shots.Select((s, i) => (s, i)))
            Body.Children.Add(Text($"{i + 1}. {shot.Name} · {CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(package.Course))} · {shot.Attempts} slag"));
        Body.Children.Add(Action("Gem kopi til offline spil", "CourseLibrary.Download", () => RunAsync(async token =>
        {
            await Community.SaveDownloadAsync(Session, package, token);
            message.Text = "Kopien er gemt under Spil bane. Du kan spille og redigere den offline. Senere opdateringer overskriver ikke dine ændringer.";
        })));
        Body.Children.Add(message);
        visibility = Action(library.Published ? "Fjern fra biblioteket" : "Vis i biblioteket", "CourseLibrary.Visibility", () => RunAsync(async token =>
        {
            await Community.Api.SetVisibilityAsync(Session, library.Id, !library.Published, token);
            await Navigation.PopAsync();
        }));
        Body.Children.Add(visibility);
    }

    protected override Task LoadAsync(CancellationToken token)
    {
        visibility.IsVisible = Session.AccountId == library.OwnerId;
        map.ImageAspectRatio = package.Course.ImageAspectRatio; map.SavedShots = package.Course.Shots; map.Refresh();
        map.SetImage(ImageSource.FromStream(() => new MemoryStream(package.Image)));
        return Task.CompletedTask;
    }
}
