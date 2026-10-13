using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GolfSG.Application.Courses;
using GolfSG.Application.Products;
using GolfSG.Application.Services;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Courses;
using GolfSG.Infrastructure.Persistence;
using GolfSG.Tests.Infrastructure.TestDoubles;
using GolfSG.Views;
using Microsoft.Maui.Dispatching;

namespace GolfSG.Tests;

[TestClass]
public sealed class CourseCommunityPageTests
{
    private IDispatcherProvider? previous;
    [TestInitialize] public void Dispatch() { previous = DispatcherProvider.Current; DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider()); }
    [TestCleanup] public void Restore() => DispatcherProvider.SetCurrent(previous!);

    [TestMethod]
    public async Task LibrarySearchAndMineFilterUpdateVisibleCourseCards()
    {
        string? query = null;
        await using var fixture = new Fixture((request, _) =>
        {
            query = request.RequestUri!.Query;
            return Task.FromResult(Json(JsonSerializer.Serialize(new List<LibraryCourse>
                { new(Guid.NewGuid(), Guid.NewGuid(), "owner", "Creator", "Shared course", 1, 2, true, DateTimeOffset.UtcNow) }, CommunityJsonContext.Default.ListLibraryCourse)));
        });
        var page = new CourseLibraryPage(fixture.Workflow);
        Appear(page); await Ready(page);
        Assert.IsTrue(Descendants(page).OfType<Label>().Any(l => l.Text == "Shared course"));
        Control<Entry>(page, "CourseLibrary.Search").Text = "my course";
        Control<CheckBox>(page, "CourseLibrary.Mine").IsChecked = true;
        Click(Control<Button>(page, "CourseLibrary.Find")); await Ready(page);
        Assert.IsTrue(query!.Contains("search=my%20course") && query.Contains("mine=true"));
    }

    [TestMethod]
    public async Task JoinPreviewDisclosesSharedResultsAndClosedGameDisablesJoin()
    {
        var game = new CourseGameInfo(Guid.NewGuid(), "Closed game", "H7K2Q9", "host", "Host", true,
            CourseCommunityService.ScoringVersion, Package().Course, DateTimeOffset.UtcNow);
        await using var fixture = new Fixture((_, _) => Task.FromResult(Json(JsonSerializer.Serialize(game, CommunityJsonContext.Default.CourseGameInfo))));
        var page = new CourseGameJoinPage(fixture.Workflow, fixture.Practice);
        Appear(page); await Ready(page);
        Control<Entry>(page, "CourseGame.Code").Text = "h7k2q9";
        Click(Control<Button>(page, "CourseGame.Find")); await Ready(page);
        Assert.IsFalse(Control<Button>(page, "CourseGame.Join.Confirm").IsEnabled);
        Assert.IsTrue(Descendants(page).OfType<Label>().Any(l => l.Text?.Contains("indsendte slagresultater") == true));
    }

    [TestMethod]
    public async Task LibraryPreviewDownloadSavesOfflineCopyAndHidesForeignOwnerControls()
    {
        await using var fixture = new Fixture((_, _) => throw new InvalidOperationException("No request expected"));
        var package = Package();
        var library = new LibraryCourse(Guid.NewGuid(), Guid.NewGuid(), "other-user", "Other", "Course", 1, 2, true, DateTimeOffset.UtcNow);
        var page = new CourseLibraryPreviewPage(fixture.Workflow, library, package);
        Appear(page); await Ready(page);
        Assert.IsFalse(Control<Button>(page, "CourseLibrary.Visibility").IsVisible);
        Click(Control<Button>(page, "CourseLibrary.Download")); await Ready(page);
        var downloaded = (await fixture.Practice.LoadAsync()).Courses.Single();
        Assert.AreEqual(package.Course.Name, downloaded.Name);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.Directory, "course-images", downloaded.ImageFile)));
    }

    [TestMethod]
    public async Task LeavingDuringLibraryRequestCancelsAndReturningReloadsWithoutDuplicateRequests()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        await using var fixture = new Fixture(async (_, token) =>
        {
            if (++calls == 1) { started.SetResult(); await Task.Delay(Timeout.Infinite, token); }
            return Json("[]");
        });
        var page = new CourseLibraryPage(fixture.Workflow);
        Appear(page); await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Click(Control<Button>(page, "CourseLibrary.Page.Refresh"));
        Assert.AreEqual(1, calls);
        Lifecycle(page, "OnDisappearing"); Appear(page); await Ready(page);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task LeaderboardOnlyFetchesPagedOutcomesWhenExpandedAndReusesVisibleDetails()
    {
        var course = Package().Course with { Shots = [Package().Course.Shots[0] with { Attempts = 100 }, Package().Course.Shots[0] with { Id = "second", Attempts = 1 }] };
        var game = new CourseGameInfo(Guid.NewGuid(), "Game", "H7K2Q9", "host", "Host", false, CourseCommunityService.ScoringVersion, course, DateTimeOffset.UtcNow);
        var board = new CourseGameDetails(game, [new("other", "Other", Guid.NewGuid().ToString("N"), true, 0, 1)]);
        var resultCalls = 0;
        await using var fixture = new Fixture((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/results"))
            {
                resultCalls++;
                var offset = request.RequestUri.Query.Contains("offset=100") ? 100 : 0;
                var results = Enumerable.Range(offset, offset == 0 ? 100 : 1).Select(i =>
                    new CourseShotResult(new(0, ShotLie.Holed, 0, true), 1, 0, 0, i == 100 ? "second" : "shot")).ToArray();
                return Task.FromResult(Json(JsonSerializer.Serialize(new CourseGamePlayerResults("other", "Other", offset, 101, results), CommunityJsonContext.Default.CourseGamePlayerResults)));
            }
            return Task.FromResult(Json(JsonSerializer.Serialize(board, CommunityJsonContext.Default.CourseGameDetails)));
        });
        var page = new CourseGamePage(fixture.Workflow, fixture.Practice, game.Id);
        Appear(page); await Ready(page); Assert.AreEqual(0, resultCalls);
        var toggle = Control<Button>(page, "CourseGame.Outcomes-other");
        Click(toggle); await Ready(page); Assert.AreEqual(1, resultCalls);
        var more = Control<Button>(page, "CourseGame.MoreOutcomes-other"); Assert.IsTrue(more.IsVisible);
        Click(toggle); Click(toggle); await Ready(page); Assert.AreEqual(1, resultCalls);
        Click(more); await Ready(page); Assert.AreEqual(2, resultCalls); Assert.IsFalse(more.IsVisible);
        Assert.IsTrue(Descendants(page).OfType<Label>().Any(l => l.Text?.StartsWith("101.") == true));
    }

    [TestMethod]
    public async Task MyGamesChecksLocalHistoryOnceForThirtyPendingGames()
    {
        var games = Enumerable.Range(0, 30).Select(i => new MyCourseGame(new(Guid.NewGuid(), "Game " + i, "H7K2Q9", "host", false, DateTimeOffset.UtcNow),
            Guid.NewGuid().ToString("N"), false)).ToList();
        await using var fixture = new Fixture((_, _) => Task.FromResult(Json(JsonSerializer.Serialize(games, CommunityJsonContext.Default.ListMyCourseGame))));
        var page = new MyCourseGamesPage(fixture.Workflow, fixture.Practice);
        Appear(page); await Ready(page);
        Assert.AreEqual(1, fixture.History.Reads, "A list page must not reparse History for every item.");
    }

    [TestMethod]
    public async Task MyGamesDoesNotReadHistoryForAlreadySubmittedGames()
    {
        var games = new List<MyCourseGame> { new(new(Guid.NewGuid(), "Game", "H7K2Q9", "host", false, DateTimeOffset.UtcNow), Guid.NewGuid().ToString("N"), true) };
        await using var fixture = new Fixture((_, _) => Task.FromResult(Json(JsonSerializer.Serialize(games, CommunityJsonContext.Default.ListMyCourseGame))));
        var page = new MyCourseGamesPage(fixture.Workflow, fixture.Practice);
        Appear(page); await Ready(page); Assert.AreEqual(0, fixture.History.Reads);
    }

    private static CoursePackage Package() => new(new("course", "Course", "course.png", 1, 20, "Measured",
        [new("shot", "Putting", new(.1, .1), new(.2, .2), ShotLie.Green, PracticeShotCategory.Putting, 3, 2)]),
        [137, 80, 78, 71, 13, 10, 26, 10]);
    private static void Appear(ContentPage page) => Lifecycle(page, "OnAppearing");
    private static void Lifecycle(ContentPage page, string method) => typeof(CourseCommunityPage).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.Invoke(page, null);
    private static void Click(Button button) => ((IButtonController)button).SendClicked();
    private static async Task Ready(ContentPage page)
    {
        var busy = typeof(CourseCommunityPage).GetField("busy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        do { await Task.Delay(10); } while ((bool)busy.GetValue(page)! && DateTime.UtcNow < deadline);
        Assert.IsFalse((bool)busy.GetValue(page)!);
        var error = (Label)typeof(CourseCommunityPage).GetField("error", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(page)!;
        Assert.IsTrue(string.IsNullOrEmpty(error.Text), error.Text);
    }
    private static T Control<T>(ContentPage page, string id) where T : VisualElement => Descendants(page).OfType<T>().Single(v => v.AutomationId == id);
    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren()) foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class Fixture : IAsyncDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        private readonly HttpClient client;
        public CourseCommunityWorkflow Workflow { get; }
        public CoursePracticeService Practice { get; }
        public CountingHistory History { get; }
        public Fixture(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action)
        {
            var settings = new Settings(); var accounts = new TestApiAccountService(settings);
            client = new HttpClient(new Handler(action));
            var rounds = new CountingHistory(new FileRoundRepository(Directory)); History = rounds;
            var putting = new StrokesGainedPuttingService(); var around = new StrokesGainedAroundGreenService(putting);
            Practice = new(new FileCoursePracticeRepository(Directory), rounds, putting, new StrokesGainedApproachService(putting, around), around);
            Workflow = new(new CourseCommunityApi(client, accounts), accounts, settings, Practice, rounds, Directory);
        }
        public ValueTask DisposeAsync()
        { client.Dispose(); if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true); return ValueTask.CompletedTask; }
    }
    private sealed class CountingHistory(IRoundRepository inner) : IRoundRepository
    {
        public int Reads { get; private set; }
        public string ActiveStoragePath => inner.ActiveStoragePath;
        public bool WasLastReadRecoveredFromBackup => inner.WasLastReadRecoveredFromBackup;
        public bool WasLastReadMigratedFromLegacyStorage => inner.WasLastReadMigratedFromLegacyStorage;
        public string? LastMigrationSourcePath => inner.LastMigrationSourcePath;
        public bool WasLastUnreadableActiveFilePreserved => inner.WasLastUnreadableActiveFilePreserved;
        public string? LastPreservedUnreadableFilePath => inner.LastPreservedUnreadableFilePath;
        public Task<IReadOnlyList<Round>> GetRoundsAsync() { Reads++; return inner.GetRoundsAsync(); }
        public Task<Round?> GetRoundAsync(string id) { Reads++; return inner.GetRoundAsync(id); }
        public Task SaveRoundAsync(Round round) => inner.SaveRoundAsync(round);
        public Task DeleteRoundAsync(string id) => inner.DeleteRoundAsync(id);
        public Task ExportRoundsAsync(string path) => inner.ExportRoundsAsync(path);
        public Task ImportRoundsAsync(string path) => inner.ImportRoundsAsync(path);
    }
    private sealed class Settings : IProductsApiSettings { public string BaseAddress { get; set; } = "https://localhost/"; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken); }
    private sealed class ImmediateDispatcherProvider : IDispatcherProvider
    { public IDispatcher GetForCurrentThread() => new ImmediateDispatcher(); }
    private sealed class ImmediateDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) => throw new NotSupportedException();
        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }
}
