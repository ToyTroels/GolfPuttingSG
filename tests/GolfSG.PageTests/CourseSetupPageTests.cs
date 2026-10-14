using System.Reflection;
using GolfSG.Application.Courses;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Views;

namespace GolfSG.Tests;

[TestClass]
public sealed class CourseSetupPageTests
{
    [TestMethod]
    public async Task NextShotUsesSuggestedNameAndReturnsToPictureReadyForAnotherShot()
    {
        var page = CreatePage();
        var map = Field<CourseMapView>(page, "map");
        var scroll = Field<ScrollView>(page, "courseScroll");
        var scrolled = false;
        scroll.ScrollToRequested += (_, _) => { scrolled = true; ((IScrollView)scroll).ScrollFinished(); };
        map.From = new(.2, .6); map.Target = new(.4, .3);
        Field<Entry>(page, "measured").Text = "25";
        ClickLie(page, ShotLie.Sand);
        Assert.AreEqual("Foreslået navn: Slag 1 · Bunker · 25 m", Field<Label>(page, "shotNameSuggestion").Text);

        var next = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.SaveShotAndContinue");
        Assert.AreEqual(Field<VerticalStackLayout>(page, "footer"), next.Parent);
        await ClickAsync(page, next);

        var shot = Field<List<CoursePracticeShot>>(page, "shots").Single();
        Assert.AreEqual("Slag 1 · Bunker · 25 m", shot.Name);
        Assert.AreEqual(ShotLie.Sand, shot.Lie);
        Assert.AreEqual(25d, shot.MeasuredDistanceMeters);
        Assert.IsTrue(scrolled);
        Assert.IsNull(map.From); Assert.IsNull(map.Target);
        Assert.IsTrue(map.PlaceFrom);
        Assert.IsFalse(map.DragSavedShots);
        Assert.IsNull(Field<ShotLie?>(page, "startingLie"));
        Assert.AreEqual("Foreslået navn: Slag 2", Field<Label>(page, "shotNameSuggestion").Text);
        Assert.AreEqual(2, map.CurrentShotNumber);
        Assert.IsFalse(map.ShowSavedShots, "Starting the next shot must retain the current-only view.");

        // A suggested name must not turn an otherwise empty editor into an unsaved draft.
        Field<Entry>(page, "courseName").Text = "Practice";
        Assert.IsTrue(await (Task<bool>)typeof(CourseSetupPage).GetMethod("SaveCourseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!);
    }

    [TestMethod]
    public async Task PictureFilterDefaultsToCurrentAndPreservesDraftAndSelectedViewOnNextShot()
    {
        var page = CreatePage();
        var map = Field<CourseMapView>(page, "map");
        Assert.IsFalse(map.ShowSavedShots);
        var scroll = Field<ScrollView>(page, "courseScroll");
        scroll.ScrollToRequested += (_, _) => ((IScrollView)scroll).ScrollFinished();
        var current = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.ShowCurrentShot");
        var all = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.ShowAllShots");
        var next = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.SaveShotAndContinue");

        map.From = new(.2, .6); map.Target = new(.4, .3);
        ClickLie(page, ShotLie.Fairway);
        await ClickAsync(page, next);
        var saved = map.SavedShots.Single();
        map.From = new(.6, .6); map.Target = new(.7, .3);
        var from = map.From; var target = map.Target;
        Field<Entry>(page, "shotName").Text = "Mit næste slag";
        Field<Entry>(page, "measured").Text = "25";
        ClickLie(page, ShotLie.Sand);

        ((IButtonController)all).SendClicked();
        Assert.IsTrue(map.ShowSavedShots);
        ((IButtonController)current).SendClicked();
        Assert.IsFalse(map.ShowSavedShots);
        Assert.AreEqual(from, map.From); Assert.AreEqual(target, map.Target);
        Assert.AreEqual(saved, map.SavedShots.Single());
        Assert.AreEqual(2, map.CurrentShotNumber);
        Assert.AreEqual("Mit næste slag", Field<Entry>(page, "shotName").Text);
        Assert.AreEqual("25", Field<Entry>(page, "measured").Text);
        Assert.AreEqual(ShotLie.Sand, Field<ShotLie?>(page, "startingLie"));

        ((IButtonController)all).SendClicked();
        await ClickAsync(page, next);
        Assert.IsTrue(map.ShowSavedShots, "Starting the next shot must retain the chosen all-shots view.");
        Assert.HasCount(2, map.SavedShots);
        Assert.AreEqual(3, map.CurrentShotNumber);
    }

    [TestMethod]
    public async Task PlacingSecondShotUpdatesSuggestionWithoutChangingNameInputOrRebuildingSavedShots()
    {
        var page = CreatePage();
        var scroll = Field<ScrollView>(page, "courseScroll");
        scroll.ScrollToRequested += (_, _) => ((IScrollView)scroll).ScrollFinished();
        var map = Field<CourseMapView>(page, "map");
        map.From = new(.2, .6); map.Target = new(.4, .3);
        ClickLie(page, ShotLie.Fairway);
        var next = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.SaveShotAndContinue");
        await ClickAsync(page, next);

        var shotCard = Field<VerticalStackLayout>(page, "shotList").Children.Single();
        var nameInput = Field<Entry>(page, "shotName");
        var entryChanges = new List<string?>();
        nameInput.PropertyChanged += (_, e) => entryChanges.Add(e.PropertyName);
        var scrollRequests = new List<ScrollToRequestedEventArgs>();
        scroll.ScrollToRequested += (_, args) => scrollRequests.Add(args);
        var canvas = DispatchProxy.Create<ICanvas, CourseMapViewTests.RecordingCanvas>();
        map.ImageAspectRatio = 300d / 570;
        map.Draw(canvas, new RectF(0, 0, 300, 570));
        var touch = (IGraphicsView)typeof(CourseMapView).GetField("overlay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;
        touch.StartInteraction([new(240, 450)]);
        touch.EndInteraction([new(240, 450)], true);
        touch.StartInteraction([new(240, 100)]);
        touch.EndInteraction([new(240, 100)], true);
        touch.StartInteraction([new(240, 100)]);
        touch.DragInteraction([new(210, 100)]);
        touch.EndInteraction([new(210, 100)], true);

        Assert.StartsWith("Foreslået navn: Slag 2", Field<Label>(page, "shotNameSuggestion").Text);
        Assert.AreEqual("Valgfrit navn", nameInput.Placeholder);
        Assert.DoesNotContain(nameof(Entry.Placeholder), entryChanges);
        Assert.DoesNotContain(nameof(Entry.Text), entryChanges);
        Assert.AreSame(shotCard, Field<VerticalStackLayout>(page, "shotList").Children.Single());
        Assert.IsEmpty(scrollRequests, "Placing and dragging the next shot must not scroll to an input.");
    }

    [TestMethod]
    public async Task QuickLieSelectionPreservesCustomNameAndEditingUpdatesExistingShot()
    {
        var page = CreatePage();
        var scroll = Field<ScrollView>(page, "courseScroll");
        scroll.ScrollToRequested += (_, _) => ((IScrollView)scroll).ScrollFinished();
        var map = Field<CourseMapView>(page, "map");
        map.From = new(.2, .6); map.Target = new(.4, .3);
        Field<Entry>(page, "shotName").Text = "Min indspilsøvelse";
        ClickLie(page, ShotLie.Green);
        Assert.AreEqual(0, Field<Picker>(page, "category").SelectedIndex);
        ClickLie(page, ShotLie.Tee);
        Assert.AreEqual(1, Field<Picker>(page, "category").SelectedIndex);
        Assert.AreEqual("Min indspilsøvelse", Field<Entry>(page, "shotName").Text);
        var next = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.SaveShotAndContinue");
        await ClickAsync(page, next);
        var original = Field<List<CoursePracticeShot>>(page, "shots").Single();
        Assert.AreEqual("Min indspilsøvelse", original.Name);

        var edit = Descendants(page).OfType<Button>().Single(b => b.Text == "Rediger slag");
        ((IButtonController)edit).SendClicked();
        Assert.AreEqual(ShotLie.Tee, Field<ShotLie?>(page, "startingLie"));
        Assert.AreEqual(1, map.CurrentShotNumber);
        ClickLie(page, ShotLie.Rough);
        await ClickAsync(page, next);
        var updated = Field<List<CoursePracticeShot>>(page, "shots").Single();
        Assert.AreEqual(original.Id, updated.Id);
        Assert.AreEqual("Min indspilsøvelse", updated.Name);
        Assert.AreEqual(ShotLie.Rough, updated.Lie);
    }

    [TestMethod]
    public async Task NextShotRequiresLieAndKeepsIncompleteDraft()
    {
        var page = CreatePage();
        var scroll = Field<ScrollView>(page, "courseScroll");
        View? scrollTarget = null;
        scroll.ScrollToRequested += (_, args) => { scrollTarget = args.Element as View; ((IScrollView)scroll).ScrollFinished(); };
        var map = Field<CourseMapView>(page, "map");
        map.From = new(.2, .6); map.Target = new(.4, .3);
        var from = map.From; var target = map.Target;
        Field<Entry>(page, "shotName").Text = "Behold dette slag";
        var next = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.SaveShotAndContinue");
        await ClickAsync(page, next, expectError: true);
        Assert.IsEmpty(Field<List<CoursePracticeShot>>(page, "shots"));
        Assert.AreEqual(from, map.From); Assert.AreEqual(target, map.Target);
        Assert.AreEqual("Behold dette slag", Field<Entry>(page, "shotName").Text);
        Assert.IsTrue(Field<Label>(page, "error").IsVisible);
        Assert.Contains("Fairway eller Bunker", Field<Label>(page, "error").Text);
        Assert.AreEqual(Field<View>(page, "startLieSection"), scrollTarget);
    }

    [TestMethod]
    public void HelpCanBeReopenedForCalibrationWithoutLosingPendingShot()
    {
        var page = CreatePage();
        var map = Field<CourseMapView>(page, "map");
        var from = new CoursePoint(.2, .6);
        var target = new CoursePoint(.4, .3);
        map.From = from;
        map.Target = target;
        Field<Entry>(page, "shotName").Text = "Bunkerslag";
        Field<Entry>(page, "measured").Text = "25";

        var help = Descendants(page).OfType<Button>().Single(b => b.AutomationId == "CoursePractice.MapHelp");
        var calibrate = Descendants(page).OfType<Button>().Single(b => b.Text == "Kalibrer målestok");
        var panel = (VerticalStackLayout)calibrate.Parent;
        Assert.IsFalse(panel.IsVisible);

        ((IButtonController)help).SendClicked();
        Assert.IsTrue(panel.IsVisible);
        ((IButtonController)help).SendClicked();
        Assert.IsFalse(panel.IsVisible);
        Assert.AreEqual(from, map.From);
        Assert.AreEqual(target, map.Target);
        Assert.AreEqual("Bunkerslag", Field<Entry>(page, "shotName").Text);
        Assert.AreEqual("25", Field<Entry>(page, "measured").Text);

        ((IButtonController)help).SendClicked();
        Assert.IsTrue(panel.IsVisible);
        ((IButtonController)calibrate).SendClicked();
        Assert.IsTrue(Field<bool>(page, "calibrationMode"));
        Assert.IsTrue(map.IsCalibration);
        Assert.IsNull(map.From);
        Assert.IsNull(map.Target);
        Assert.AreEqual(from, Field<CoursePoint>(page, "beforeFrom"));
        Assert.AreEqual(target, Field<CoursePoint>(page, "beforeTarget"));
        Assert.IsTrue(string.IsNullOrEmpty(Field<Label>(page, "error").Text));
    }

    private static CourseSetupPage CreatePage()
    {
        var putting = new StrokesGainedPuttingService();
        var around = new StrokesGainedAroundGreenService(putting);
        var service = new CoursePracticeService(new Store(), new TestRoundRepository(), putting,
            new StrokesGainedApproachService(putting, around), around);
        return new CourseSetupPage(service);
    }

    private static void ClickLie(CourseSetupPage page, ShotLie value) =>
        ((IButtonController)Descendants(page).OfType<Button>().Single(b => b.AutomationId == $"CoursePractice.StartLie.{value}")).SendClicked();

    private static async Task ClickAsync(CourseSetupPage page, Button button, bool expectError = false)
    {
        ((IButtonController)button).SendClicked();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Field<bool>(page, "busy") && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse(Field<bool>(page, "busy"), "Page action did not complete.");
        if (!expectError) Assert.IsTrue(string.IsNullOrWhiteSpace(Field<Label>(page, "error").Text), Field<Label>(page, "error").Text);
    }

    private static T Field<T>(CourseSetupPage page, string name) =>
        (T)typeof(CourseSetupPage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page)!;

    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private sealed class Store : ICoursePracticeRepository
    {
        public Task<CoursePracticeDocument> LoadAsync() => Task.FromResult(CoursePracticeDocument.Empty);
        public Task SaveAsync(CoursePracticeDocument document) => Task.CompletedTask;
    }
}
