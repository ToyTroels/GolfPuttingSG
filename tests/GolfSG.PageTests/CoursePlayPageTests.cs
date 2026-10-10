using System.Reflection;
using GolfSG.Application.Courses;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Views;

[assembly: DoNotParallelize]

namespace GolfSG.Tests;

[TestClass]
public sealed class CoursePlayPageTests
{
    [TestMethod]
    public async Task SaveButtonRecordsFirstAndSecondResultsAndAdvancesDisplayedShot()
    {
        var (page, store) = await CreatePageAsync();
        await ChooseShotAsync(page, "approach");

        SetOutcome(page, "2", 0, 1);
        await ClickAsync(page, GetField<Button>(page, "record"));

        AssertRecorded(page, store, 1, "approach", 2, 1, "bunker");
        AssertClearedInput(page);

        SetOutcome(page, "4,5", 4, 2);
        await ClickAsync(page, GetField<Button>(page, "record"));

        AssertRecorded(page, store, 2, "bunker", 4.5, 2, "putt", ShotLie.Sand);
        AssertClearedInput(page);
    }

    [TestMethod]
    public async Task SaveButtonRecordsSelectedNonFirstShotOnFirstClick()
    {
        var (page, store) = await CreatePageAsync();
        await ChooseShotAsync(page, "putt");

        SetOutcome(page, "0,8", 0, 0);
        await ClickAsync(page, GetField<Button>(page, "record"));

        AssertRecorded(page, store, 1, "putt", .8, 0, "approach");
        AssertClearedInput(page);
    }

    [TestMethod]
    public async Task SaveButtonKeepsInputPositionUntilAllSelectedPositionAttemptsAreRecorded()
    {
        var (page, store) = await CreatePageAsync(approachAttempts: 3);
        var scrollRequests = CaptureScrollRequests(page);
        await ChooseShotAsync(page, "approach");
        scrollRequests.Clear();

        SetOutcome(page, "2", 0, 1);
        await ClickAsync(page, GetField<Button>(page, "record"));

        AssertRecorded(page, store, 1, "approach", 2, 1, "approach");
        Assert.AreEqual("Slag 2 af 3", GetField<Label>(page, "attemptProgress").Text);
        AssertClearedInput(page);
        Assert.IsEmpty(scrollRequests, "Entering the next result at the same position must not scroll away from the inputs.");

        SetOutcome(page, "4,5", 4, 2);
        await ClickAsync(page, GetField<Button>(page, "record"));

        AssertRecorded(page, store, 2, "approach", 4.5, 2, "approach", ShotLie.Sand);
        Assert.AreEqual("Slag 3 af 3", GetField<Label>(page, "attemptProgress").Text);
        AssertClearedInput(page);
        Assert.IsEmpty(scrollRequests, "Saving another attempt at the same position must keep the input area visible.");

        SetOutcome(page, "1", 0, 0);
        await ClickAsync(page, GetField<Button>(page, "record"));

        AssertRecorded(page, store, 3, "approach", 1, 0, "bunker");
        Assert.AreEqual("Slag 1 af 1", GetField<Label>(page, "attemptProgress").Text);
        AssertClearedInput(page);
        Assert.HasCount(1, scrollRequests, "Changing position must reveal the next shot at the top of the screen.");
        Assert.AreEqual(0d, scrollRequests[0].ScrollX);
        Assert.AreEqual(0d, scrollRequests[0].ScrollY);
    }

    [TestMethod]
    public async Task SaveButtonRevealsOverviewWhenLastRoundResultIsRecorded()
    {
        var (page, store) = await CreatePageAsync();
        var scrollRequests = CaptureScrollRequests(page);
        await ChooseShotAsync(page, "approach");
        SetOutcome(page, "2", 0, 0);
        await ClickAsync(page, GetField<Button>(page, "record"));
        SetOutcome(page, "1", 0, 0);
        await ClickAsync(page, GetField<Button>(page, "record"));
        scrollRequests.Clear();

        SetOutcome(page, "0,5", 0, 0);
        await ClickAsync(page, GetField<Button>(page, "record"));

        var active = store.Document.Active!;
        var displayed = GetField<CoursePracticeSession>(page, "session");
        Assert.IsTrue(active.IsComplete);
        Assert.IsTrue(displayed.IsComplete);
        Assert.IsNull(displayed.CurrentShot);
        Assert.HasCount(3, active.Results);
        Assert.AreEqual("putt", active.Results[^1].ShotId);
        Assert.AreEqual(.5, active.Results[^1].Outcome.EndDistanceMeters);
        Assert.StartsWith("3/3", GetField<Label>(page, "progress").Text);
        Assert.IsTrue(GetField<VerticalStackLayout>(page, "overview").IsVisible);
        Assert.IsFalse(GetField<VerticalStackLayout>(page, "play").IsVisible);
        Assert.IsFalse(GetField<Button>(page, "record").IsVisible);
        Assert.IsTrue(GetField<Button>(page, "finish").IsVisible);
        Assert.HasCount(1, scrollRequests, "Finishing the round must bring its completed overview into view.");
        Assert.AreEqual(0d, scrollRequests[0].ScrollX);
        Assert.AreEqual(0d, scrollRequests[0].ScrollY);
    }

    [TestMethod]
    public async Task UndoButtonReopensSavedShotAndRestoresOutcomeInDisplayedInput()
    {
        var (page, store) = await CreatePageAsync();
        await ChooseShotAsync(page, "putt");
        SetOutcome(page, "0,8", 0, 1);
        await ClickAsync(page, GetField<Button>(page, "record"));

        await ClickAsync(page, GetField<Button>(page, "undo"));

        var displayed = GetField<CoursePracticeSession>(page, "session");
        Assert.IsEmpty(store.Document.Active!.Results);
        Assert.IsEmpty(displayed.Results);
        Assert.AreEqual("putt", displayed.CurrentShot!.Id);
        Assert.AreEqual(store.Document.Active.Input.Distance, GetField<Entry>(page, "distance").Text);
        Assert.AreEqual(0, GetField<Picker>(page, "lie").SelectedIndex);
        Assert.AreEqual(1d, GetField<Stepper>(page, "penalties").Value);
        Assert.StartsWith("0/3", GetField<Label>(page, "progress").Text);
    }

    [TestMethod]
    public async Task CountButtonsChangeSelectedPositionForThisRoundAndSaveRepeatsIt()
    {
        var (page, store) = await CreatePageAsync();
        PreviewShot(page, "putt");
        Assert.IsFalse(CountControl<Button>(page, "CoursePractice.DecreaseAttempts-putt").IsEnabled);
        await ClickAsync(page, CountControl<Button>(page, "CoursePractice.IncreaseAttempts-putt"));
        await ClickAsync(page, CountControl<Button>(page, "CoursePractice.IncreaseAttempts-putt"));
        await ClickAsync(page, CountControl<Button>(page, "CoursePractice.DecreaseAttempts-putt"));

        Assert.AreEqual("2", CountControl<Label>(page, "CoursePractice.AttemptCount-putt").Text);
        Assert.AreEqual(2, store.Document.Active!.Course.Shots.Single(shot => shot.Id == "putt").Attempts);
        Assert.AreEqual(1, store.Document.Courses.Single().Shots.Single(shot => shot.Id == "putt").Attempts);
        Assert.StartsWith("0/4", GetField<Label>(page, "progress").Text);
        Assert.AreEqual("approach", store.Document.Active.CurrentShot!.Id);

        await ChooseShotAsync(page, "putt");
        Assert.AreEqual("Slag 1 af 2", GetField<Label>(page, "attemptProgress").Text);
        SetOutcome(page, "0,8", 0, 0);
        await ClickAsync(page, GetField<Button>(page, "record"));
        AssertRecorded(page, store, 1, "putt", .8, 0, "putt");
        Assert.AreEqual("Slag 2 af 2", GetField<Label>(page, "attemptProgress").Text);
        AssertClearedInput(page);

        await ClickAsync(page, GetField<Button>(page, "showOverview"));
        Assert.IsFalse(CountControl<Button>(page, "CoursePractice.DecreaseAttempts-putt").IsEnabled);
        Assert.IsFalse(CountControl<Button>(page, "CoursePractice.IncreaseAttempts-putt").IsEnabled);
        PreviewShot(page, "bunker");
        Assert.IsTrue(CountControl<Button>(page, "CoursePractice.IncreaseAttempts-bunker").IsEnabled);

        await ChooseShotAsync(page, "putt");
        SetOutcome(page, "1", 0, 0);
        await ClickAsync(page, GetField<Button>(page, "record"));
        AssertRecorded(page, store, 2, "putt", 1, 0, "approach");
        AssertClearedInput(page);
    }

    [TestMethod]
    [DataRow(1, false, true)]
    [DataRow(100, true, false)]
    public async Task CountButtonsRespectMinimumAndMaximum(int attempts, bool canDecrease, bool canIncrease)
    {
        var (page, _) = await CreatePageAsync(attempts);
        Assert.AreEqual(canDecrease, CountControl<Button>(page, "CoursePractice.DecreaseAttempts-approach").IsEnabled);
        Assert.AreEqual(canIncrease, CountControl<Button>(page, "CoursePractice.IncreaseAttempts-approach").IsEnabled);
    }

    [TestMethod]
    public async Task FailedCountSaveKeepsCountAndDraftAndRetryPreservesInput()
    {
        var (page, store) = await CreatePageAsync();
        await ChooseShotAsync(page, "approach");
        SetOutcome(page, "2", 0, 1);
        await ClickAsync(page, GetField<Button>(page, "showOverview"));
        store.Failure = new IOException();
        await ClickAsync(page, CountControl<Button>(page, "CoursePractice.IncreaseAttempts-approach"), expectError: true);
        Assert.AreEqual("1", CountControl<Label>(page, "CoursePractice.AttemptCount-approach").Text);
        Assert.AreEqual(1, store.Document.Active!.Course.Shots[0].Attempts);
        Assert.AreEqual("2", store.Document.Active.Input.Distance);
        Assert.IsFalse(string.IsNullOrWhiteSpace(GetField<Label>(page, "error").Text));

        store.Failure = null;
        await ClickAsync(page, CountControl<Button>(page, "CoursePractice.IncreaseAttempts-approach"));
        await ChooseShotAsync(page, "approach");
        Assert.AreEqual("2", GetField<Entry>(page, "distance").Text);
        Assert.AreEqual(1d, GetField<Stepper>(page, "penalties").Value);
        Assert.AreEqual("Slag 1 af 2", GetField<Label>(page, "attemptProgress").Text);
    }

    private static async Task<(CoursePlayPage Page, Store Store)> CreatePageAsync(int approachAttempts = 1)
    {
        var store = new Store();
        var putting = new StrokesGainedPuttingService();
        var around = new StrokesGainedAroundGreenService(putting);
        var service = new CoursePracticeService(store, new TestRoundRepository(), putting,
            new StrokesGainedApproachService(putting, around), around);
        var course = new PracticeCourse("course", "Practice", "sample.jpg", .5, 100, "Measured", [
            new("approach", "Approach", new(.5, .8), new(.5, .25), ShotLie.Rough, PracticeShotCategory.Approach, Attempts: approachAttempts),
            new("bunker", "Bunker", new(.3, .5), new(.4, .45), ShotLie.Sand, PracticeShotCategory.AroundGreen, 15),
            new("putt", "Putt", new(.5, .2), new(.5, .175), ShotLie.Green, PracticeShotCategory.Putting, 5)
        ]);
        await service.SaveCourseAsync(course);
        var session = await service.StartAsync(course);
        var page = new CoursePlayPage(service, session);
        return (page, store);
    }

    private static async Task ChooseShotAsync(CoursePlayPage page, string shotId)
    {
        PreviewShot(page, shotId);
        var card = (Border)GetField<VerticalStackLayout>(page, "shotList").Children.Single();
        var choose = ((VerticalStackLayout)card.Content!).Children.OfType<Button>().Single();
        await ClickAsync(page, choose);
        Assert.AreEqual(shotId, GetField<CoursePracticeSession>(page, "session").CurrentShot!.Id);
    }

    private static void PreviewShot(CoursePlayPage page, string shotId)
    {
        var selectors = GetField<HorizontalStackLayout>(page, "shotSelector");
        var shotIndex = GetField<CoursePracticeSession>(page, "session").Course.Shots
            .ToList().FindIndex(shot => shot.Id == shotId);
        ((IButtonController)selectors.Children[shotIndex]).SendClicked();
    }

    private static T CountControl<T>(CoursePlayPage page, string automationId) where T : View
    {
        var card = (Border)GetField<VerticalStackLayout>(page, "shotList").Children.Single();
        var control = ((VerticalStackLayout)card.Content!).Children.OfType<VerticalStackLayout>().Single();
        return control.Children.OfType<Grid>().Single().Children.OfType<T>().Single(view => view.AutomationId == automationId);
    }

    private static void SetOutcome(CoursePlayPage page, string distance, int lieIndex, int penalties)
    {
        GetField<Entry>(page, "distance").Text = distance;
        GetField<Picker>(page, "lie").SelectedIndex = lieIndex;
        GetField<Stepper>(page, "penalties").Value = penalties;
    }

    private static List<ScrollToRequestedEventArgs> CaptureScrollRequests(CoursePlayPage page)
    {
        var requests = new List<ScrollToRequestedEventArgs>();
        var scroll = GetField<ScrollView>(page, "roundScroll");
        scroll.ScrollToRequested += (_, args) =>
        {
            requests.Add(args);
            ((IScrollView)scroll).ScrollFinished();
        };
        return requests;
    }

    private static async Task ClickAsync(CoursePlayPage page, Button button, bool expectError = false)
    {
        ((IButtonController)button).SendClicked();
        // Clicked is an async void UI event; await the page's operation completion.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (GetField<bool>(page, "busy") && DateTime.UtcNow < deadline)
        {
            ((IScrollView)GetField<ScrollView>(page, "roundScroll")).ScrollFinished();
            await Task.Delay(10);
        }
        Assert.IsFalse(GetField<bool>(page, "busy"), "Page action did not complete.");
        if (!expectError)
            Assert.IsTrue(string.IsNullOrWhiteSpace(GetField<Label>(page, "error").Text), GetField<Label>(page, "error").Text);
    }

    private static void AssertRecorded(CoursePlayPage page, Store store, int count,
        string shotId, double distance, int penalties, string nextShotId, ShotLie lie = ShotLie.Green)
    {
        var active = store.Document.Active!;
        var displayed = GetField<CoursePracticeSession>(page, "session");
        Assert.HasCount(count, active.Results);
        Assert.HasCount(count, displayed.Results, "The page must show the session returned by the save.");
        Assert.AreEqual(shotId, active.Results[^1].ShotId);
        Assert.AreEqual(distance, active.Results[^1].Outcome.EndDistanceMeters);
        Assert.AreEqual(lie, active.Results[^1].Outcome.EndLie);
        Assert.AreEqual(penalties, active.Results[^1].Outcome.PenaltyStrokes);
        Assert.AreEqual(nextShotId, active.CurrentShot!.Id);
        Assert.AreEqual(nextShotId, displayed.CurrentShot!.Id);
        Assert.StartsWith($"{count}/{active.TotalAttempts}", GetField<Label>(page, "progress").Text);
    }

    private static void AssertClearedInput(CoursePlayPage page)
    {
        Assert.AreEqual("", GetField<Entry>(page, "distance").Text);
        Assert.AreEqual(0, GetField<Picker>(page, "lie").SelectedIndex);
        Assert.AreEqual(0d, GetField<Stepper>(page, "penalties").Value);
        Assert.IsFalse(GetField<CheckBox>(page, "holed").IsChecked);
    }

    private static T GetField<T>(CoursePlayPage page, string name) =>
        (T)typeof(CoursePlayPage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;

    private sealed class Store : ICoursePracticeRepository
    {
        public CoursePracticeDocument Document { get; private set; } = CoursePracticeDocument.Empty;
        public Exception? Failure { get; set; }
        public Task<CoursePracticeDocument> LoadAsync() => Task.FromResult(Document);
        public Task SaveAsync(CoursePracticeDocument document)
        {
            if (Failure is not null) return Task.FromException(Failure);
            Document = document;
            return Task.CompletedTask;
        }
    }
}
