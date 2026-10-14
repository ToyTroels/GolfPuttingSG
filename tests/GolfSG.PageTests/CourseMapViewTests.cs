using System.Reflection;
using GolfSG.Core.Models;
using GolfSG.Views;

namespace GolfSG.Tests;

[TestClass]
public sealed class CourseMapViewTests
{
    [TestMethod]
    public void SwipeOnEmptyPictureDoesNotPlacePointOrStartEditing()
    {
        var map = new CourseMapView();
        DrawLabels(map);
        var touch = TouchSurface(map);
        var started = 0; var changed = 0;
        map.InteractionStarted += () => started++;
        map.PointsChanged += (_, _) => changed++;

        touch.StartInteraction([new(240, 400)]);
        Assert.AreEqual(0, started, "Touching empty picture space must leave page scrolling available.");
        touch.DragInteraction([new(240, 320)]);
        touch.EndInteraction([new(240, 320)], true);

        Assert.IsNull(map.From); Assert.IsNull(map.Target);
        Assert.AreEqual(0, started); Assert.AreEqual(0, changed);
    }

    [TestMethod]
    public void TapPlacesPointOnlyOnReleaseAndCanRepositionExistingPoint()
    {
        var map = new CourseMapView { ImageAspectRatio = 300d / 570 };
        DrawLabels(map);
        var touch = TouchSurface(map);
        touch.StartInteraction([new(240, 400)]);
        Assert.IsNull(map.From);
        touch.EndInteraction([new(240, 400)], true);
        Assert.AreEqual(.8, map.From!.X, .001);
        Assert.AreEqual(400d / 570, map.From.Y, .001);

        touch.StartInteraction([new(60, 100)]);
        touch.EndInteraction([new(60, 100)], true);
        Assert.AreEqual(.2, map.From.X, .001);
        Assert.AreEqual(100d / 570, map.From.Y, .001);
    }

    [TestMethod]
    public void ScrollingPastExistingShotKeepsPositionsAndSelection()
    {
        var map = MapWithTwoSavedShots();
        map.SelectedShotId = "second";
        DrawLabels(map);
        var from = map.From; var target = map.Target;
        var started = 0;
        map.InteractionStarted += () => started++;
        var touch = TouchSurface(map);
        touch.StartInteraction([new(270, 450)]);
        touch.DragInteraction([new(270, 350)]);
        touch.CancelInteraction();
        touch.EndInteraction([new(270, 350)], true);

        Assert.AreEqual(from, map.From); Assert.AreEqual(target, map.Target);
        Assert.AreEqual("second", map.SelectedShotId);
        Assert.AreEqual(0, started);
    }

    [TestMethod]
    public void MarkerDragMovesEndpointAndCancellationRestoresIt()
    {
        var map = new CourseMapView { ImageAspectRatio = 300d / 570, From = new(.2, .6), Target = new(.4, .3) };
        DrawLabels(map);
        var touch = TouchSurface(map);
        var original = map.From;
        touch.StartInteraction([new(60, 342)]);
        touch.DragInteraction([new(90, 342)]);
        Assert.AreEqual(.3, map.From!.X, .001);
        Assert.AreEqual(new CoursePoint(.4, .3), map.Target);
        touch.CancelInteraction();
        Assert.AreEqual(original, map.From);
    }

    [TestMethod]
    public void LineDragMovesBothEndpointsAndEndsNormally()
    {
        var map = new CourseMapView { ImageAspectRatio = 300d / 570, From = new(.2, .6), Target = new(.4, .3) };
        DrawLabels(map);
        var touch = TouchSurface(map);
        touch.StartInteraction([new(90, 256.5f)]);
        touch.DragInteraction([new(120, 256.5f)]);
        touch.EndInteraction([new(120, 256.5f)], true);
        Assert.AreEqual(.3, map.From!.X, .001);
        Assert.AreEqual(.5, map.Target!.X, .001);
    }

    [TestMethod]
    public void CancelledTapDoesNotPlacePointAndNextTapStillWorks()
    {
        var map = new CourseMapView { ImageAspectRatio = 300d / 570 };
        DrawLabels(map);
        var touch = TouchSurface(map);
        touch.StartInteraction([new(240, 400)]);
        touch.CancelInteraction();
        touch.EndInteraction([new(240, 400)], true);
        Assert.IsNull(map.From);
        touch.StartInteraction([new(60, 100)]);
        touch.EndInteraction([new(60, 100)], true);
        Assert.AreEqual(.2, map.From!.X, .001);
    }

    [TestMethod]
    public void SavedMarkerStillMovesAndCancellationRestoresSavedShot()
    {
        var map = MapWithTwoSavedShots();
        map.ImageAspectRatio = 300d / 570;
        map.From = map.Target = null;
        DrawLabels(map);
        var original = map.SavedShots[0];
        CoursePracticeShot? changed = null;
        map.SavedShotChanged += shot => changed = shot;
        var touch = TouchSurface(map);
        touch.StartInteraction([new(30, 513)]);
        touch.DragInteraction([new(60, 513)]);
        Assert.IsNotNull(changed);
        Assert.AreEqual(original.Id, changed.Id);
        Assert.AreEqual(.2, changed.From.X, .001);
        Assert.AreEqual(original.Target, changed.Target);
        touch.CancelInteraction();
        Assert.AreEqual(original, changed);
    }

    [TestMethod]
    public void OverviewSwipeDoesNotSelectButTapStillSelectsSavedShot()
    {
        var map = MapWithTwoSavedShots();
        map.ImageAspectRatio = 300d / 570;
        map.From = map.Target = null;
        map.Editable = false; map.SelectSavedShots = true;
        DrawLabels(map);
        var touch = TouchSurface(map);
        touch.StartInteraction([new(30, 513)]);
        touch.DragInteraction([new(30, 433)]);
        touch.EndInteraction([new(30, 433)], true);
        Assert.IsNull(map.SelectedShotId);
        touch.StartInteraction([new(30, 513)]);
        touch.EndInteraction([new(30, 513)], true);
        Assert.AreEqual("first", map.SelectedShotId);
    }

    private static IGraphicsView TouchSurface(CourseMapView map) =>
        (GraphicsView)typeof(CourseMapView).GetField("overlay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(map)!;

    [TestMethod]
    public void DraftUsesNextShotNumberOnBothEndpoints()
    {
        var map = MapWithTwoSavedShots();
        map.CurrentShotNumber = 3;
        CollectionAssert.AreEqual(new[] { "1", "1", "2", "2", "3", "3" }, DrawLabels(map));
    }

    [TestMethod]
    public void CurrentOnlyViewHidesSavedShotsWithoutChangingDraftNumber()
    {
        var map = MapWithTwoSavedShots();
        map.CurrentShotNumber = 3;
        var savedShots = map.SavedShots;
        map.ShowSavedShots = false;
        CollectionAssert.AreEqual(new[] { "3", "3" }, DrawLabels(map));
        Assert.AreSame(savedShots, map.SavedShots);

        map.ShowSavedShots = true;
        CollectionAssert.AreEqual(new[] { "1", "1", "2", "2", "3", "3" }, DrawLabels(map));
    }

    [TestMethod]
    public void VisibleShotFilterPreservesNumbersAndPreventsSelectionOfHiddenShots()
    {
        var map = MapWithTwoSavedShots();
        map.ImageAspectRatio = 300d / 570;
        map.From = map.Target = null;
        map.Editable = false; map.SelectSavedShots = true;
        map.VisibleSavedShotIds = new HashSet<string> { "second" };
        CollectionAssert.AreEqual(new[] { "2", "2" }, DrawLabels(map));
        var touch = TouchSurface(map);
        touch.StartInteraction([new(30, 513)]);
        touch.EndInteraction([new(30, 513)], true);
        Assert.IsNull(map.SelectedShotId);
        touch.StartInteraction([new(90, 456)]);
        touch.EndInteraction([new(90, 456)], true);
        Assert.AreEqual("second", map.SelectedShotId);
        map.VisibleSavedShotIds = new HashSet<string>();
        Assert.IsEmpty(DrawLabels(map));
        Assert.HasCount(2, map.SavedShots);
    }

    [TestMethod]
    public void CurrentOnlyViewKeepsEditedShotsOriginalNumber()
    {
        var map = MapWithTwoSavedShots();
        map.EditingShotId = "second";
        map.CurrentShotNumber = 2;
        map.ShowSavedShots = false;
        CollectionAssert.AreEqual(new[] { "2", "2" }, DrawLabels(map));
    }

    [TestMethod]
    public void HiddenSavedMarkerDoesNotCaptureSwipeOrChangeSavedShot()
    {
        var map = MapWithTwoSavedShots();
        map.ImageAspectRatio = 300d / 570;
        map.From = map.Target = null;
        map.SelectedShotId = "first";
        map.ShowSavedShots = false;
        DrawLabels(map);
        var started = 0; var changed = 0;
        map.InteractionStarted += () => started++;
        map.SavedShotChanged += _ => changed++;
        var touch = TouchSurface(map);
        touch.StartInteraction([new(30, 513)]);
        touch.DragInteraction([new(60, 513)]);
        touch.EndInteraction([new(60, 513)], true);
        Assert.AreEqual(0, started);
        Assert.AreEqual(0, changed);
        Assert.IsNull(map.SelectedShotId);
        Assert.IsNull(map.From);
    }

    [TestMethod]
    public void EditingKeepsOriginalNumberWithoutDrawingOldEndpoints()
    {
        var map = MapWithTwoSavedShots();
        map.EditingShotId = "second";
        map.CurrentShotNumber = 2;
        CollectionAssert.AreEqual(new[] { "1", "1", "2", "2" }, DrawLabels(map));
    }

    [TestMethod]
    public void SelectingShotChangesDrawOrderWithoutRenumberingShots()
    {
        var map = MapWithTwoSavedShots();
        map.From = map.Target = null;
        map.SelectedShotId = "first";
        CollectionAssert.AreEqual(new[] { "2", "2", "1", "1" }, DrawLabels(map));
    }

    [TestMethod]
    public void CalibrationLabelsReferencePointsWithLetters()
    {
        var map = new CourseMapView { From = new(.2, .6), Target = new(.4, .3), IsCalibration = true };
        CollectionAssert.AreEqual(new[] { "A", "B" }, DrawLabels(map));
    }

    private static CourseMapView MapWithTwoSavedShots() => new()
    {
        From = new(.2, .6), Target = new(.4, .3),
        SavedShots = [
            new("first", "First", new(.1, .9), new(.1, .5), ShotLie.Fairway, PracticeShotCategory.Approach),
            new("second", "Second", new(.3, .8), new(.3, .4), ShotLie.Rough, PracticeShotCategory.Approach)
        ]
    };

    private static string[] DrawLabels(CourseMapView map)
    {
        var canvas = DispatchProxy.Create<ICanvas, RecordingCanvas>();
        map.Draw(canvas, new RectF(0, 0, 300, 570));
        return ((RecordingCanvas)(object)canvas).Labels.ToArray();
    }

    public class RecordingCanvas : DispatchProxy
    {
        public List<string> Labels { get; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(ICanvas.DrawString)) Labels.Add((string)args![0]!);
            return null;
        }
    }
}
