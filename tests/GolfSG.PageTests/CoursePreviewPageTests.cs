using System.Reflection;
using GolfSG.Application.Courses;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Views;
using Microsoft.Maui.Controls.Internals;
using Microsoft.Maui.Dispatching;

namespace GolfSG.Tests;

[TestClass]
public sealed class CoursePreviewPageTests
{
    private IDispatcherProvider? previous;
    [TestInitialize] public void Dispatch() { previous = DispatcherProvider.Current; DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider()); }
    [TestCleanup] public void Restore() => DispatcherProvider.SetCurrent(previous!);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ActiveRoundCardOffersEarlyFinishAfterAResultIsRecorded(bool selectionPage)
    {
        var fixture = new Fixture();
        var active = await fixture.Service.StartAsync(fixture.Course);
        ContentPage page = selectionPage ? new CourseSelectionPage(fixture.Service) : new CoursePracticeMenuPage(fixture.Service);
        await InvokeAsync(page, "ReloadAsync");
        var finish = Descendants(page).OfType<Button>().Single(button =>
            button.AutomationId == (selectionPage ? "CoursePractice.Courses.Finish" : "CoursePractice.Menu.Finish"));
        Assert.IsFalse(finish.IsVisible);
        await fixture.Service.RecordAsync(active.Id, 0, new(2, ShotLie.Green, 0, false), "approach");
        await InvokeAsync(page, "ReloadAsync");
        Assert.IsTrue(finish.IsVisible); Assert.AreEqual("Afslut og gem", finish.Text);
        var document = fixture.Store.Document;
        Assert.HasCount(1, document.Active!.Results);
        Assert.IsFalse(document.Active.IsComplete);
        Assert.AreEqual("Practice · 1/3 slag registreret.",
            ((Label)page.GetType().GetField("activeDescription", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!).Text);
    }

    [TestMethod]
    public async Task TappingCourseOpensShotPreviewWithoutCreatingRound()
    {
        var fixture = new Fixture();
        var page = new CourseSelectionPage(fixture.Service);
        var navigation = AttachNavigation(page);
        await InvokeAsync(page, "ReloadAsync");
        var card = Descendants(page).OfType<View>().Single(view => view.AutomationId == "CoursePractice.Preview-course");
        var tap = card.GestureRecognizers.OfType<TapGestureRecognizer>().Single();
        tap.Command!.Execute(null);
        await ReadyAsync(page);

        var preview = navigation.NavigationStack.OfType<CoursePreviewPage>().Single();
        Assert.IsNull(fixture.Store.Document.Active);
        Assert.AreEqual(0, fixture.Store.Writes);
        Assert.IsTrue(Descendants(preview).OfType<Label>().Any(label => label.Text == "1. Fairway approach"));
        Assert.IsTrue(Descendants(preview).OfType<Label>().Any(label => label.Text == "2. Short putt"));
    }

    [TestMethod]
    public async Task ExplicitStartButtonCreatesRoundAndOpensShotOverview()
    {
        var fixture = new Fixture();
        var page = new CoursePreviewPage(fixture.Service, fixture.Course);
        var navigation = AttachNavigation(page);
        await ReloadAsync(page);
        var start = Descendants(page).OfType<Button>().Single(button => button.AutomationId == "CoursePractice.Start-course");
        Assert.IsTrue(start.IsEnabled);
        Assert.IsNull(fixture.Store.Document.Active);

        ((IButtonController)start).SendClicked();
        await ReadyAsync(page);

        Assert.AreEqual("course", fixture.Store.Document.Active!.Course.Id);
        Assert.HasCount(2, fixture.Store.Document.Active.Course.Shots);
        Assert.AreEqual(1, fixture.Store.Writes);
        Assert.IsInstanceOfType<CoursePlayPage>(navigation.NavigationStack.Single());
    }

    [TestMethod]
    public async Task RoundStartedWhilePreviewOpenIsPreservedAndBlocksAnotherStart()
    {
        var fixture = new Fixture();
        var page = new CoursePreviewPage(fixture.Service, fixture.Course);
        var navigation = AttachNavigation(page);
        await ReloadAsync(page);
        var existing = await fixture.Service.StartAsync(fixture.Course);
        var writes = fixture.Store.Writes;
        var start = Descendants(page).OfType<Button>().Single(button => button.AutomationId == "CoursePractice.Start-course");
        ((IButtonController)start).SendClicked();
        await ReadyAsync(page, expectError: true);

        Assert.AreEqual(existing.Id, fixture.Store.Document.Active!.Id);
        Assert.AreEqual(writes, fixture.Store.Writes);
        Assert.IsFalse(start.IsEnabled);
        Assert.AreSame(page, navigation.NavigationStack.Single());
    }

    private static Task ReloadAsync(CoursePreviewPage page) =>
        (Task)typeof(CoursePreviewPage).GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(page, [new Func<Task>(() => InvokeAsync(page, "ReloadAsync"))])!;

    private static Task InvokeAsync(ContentPage page, string method) =>
        (Task)page.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;

    private static async Task ReadyAsync(ContentPage page, bool expectError = false)
    {
        var busy = page.GetType().GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((bool)busy.GetValue(page)! && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse((bool)busy.GetValue(page)!, "Page action did not complete.");
        if (!expectError)
        {
            var error = (Label)page.GetType().GetField("error", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;
            Assert.IsTrue(string.IsNullOrEmpty(error.Text), error.Text);
        }
    }

    private static RecordingNavigation AttachNavigation(ContentPage page)
    {
        var navigation = new RecordingNavigation(page);
        ((NavigationProxy)page.Navigation).Inner = navigation;
        return navigation;
    }

    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private sealed class Fixture
    {
        public PracticeCourse Course { get; } = new("course", "Practice", "course.png", .5, 100, "Measured", [
            new("approach", "Fairway approach", new(.2, .8), new(.2, .3), ShotLie.Fairway, PracticeShotCategory.Approach, Attempts: 2),
            new("putt", "Short putt", new(.5, .2), new(.5, .175), ShotLie.Green, PracticeShotCategory.Putting, 5)
        ]);
        public Store Store { get; }
        public CoursePracticeService Service { get; }
        public Fixture()
        {
            Store = new(CoursePracticeDocument.Empty with { Courses = [Course] });
            var putting = new StrokesGainedPuttingService();
            var around = new StrokesGainedAroundGreenService(putting);
            Service = new(Store, new TestRoundRepository(), putting, new StrokesGainedApproachService(putting, around), around);
        }
    }

    private sealed class Store(CoursePracticeDocument document) : ICoursePracticeRepository
    {
        public CoursePracticeDocument Document { get; private set; } = document;
        public int Writes { get; private set; }
        public Task<CoursePracticeDocument> LoadAsync() => Task.FromResult(Document);
        public Task SaveAsync(CoursePracticeDocument value) { Document = value; Writes++; return Task.CompletedTask; }
    }

    private sealed class RecordingNavigation(Page root) : INavigation
    {
        private readonly List<Page> pages = [root];
        public IReadOnlyList<Page> NavigationStack => pages;
        public IReadOnlyList<Page> ModalStack => [];
        public Task PushAsync(Page page) => PushAsync(page, true);
        public Task PushAsync(Page page, bool animated) { pages.Add(page); return Task.CompletedTask; }
        public void RemovePage(Page page) => pages.Remove(page);
        public void InsertPageBefore(Page page, Page before) => pages.Insert(pages.IndexOf(before), page);
        public Task<Page> PopAsync() => PopAsync(true);
        public Task<Page> PopAsync(bool animated) { var page = pages[^1]; pages.RemoveAt(pages.Count - 1); return Task.FromResult(page); }
        public Task PopToRootAsync() => PopToRootAsync(true);
        public Task PopToRootAsync(bool animated) { pages.RemoveRange(1, pages.Count - 1); return Task.CompletedTask; }
        public Task PushModalAsync(Page page) => throw new NotSupportedException();
        public Task PushModalAsync(Page page, bool animated) => throw new NotSupportedException();
        public Task<Page> PopModalAsync() => throw new NotSupportedException();
        public Task<Page> PopModalAsync(bool animated) => throw new NotSupportedException();
    }

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
