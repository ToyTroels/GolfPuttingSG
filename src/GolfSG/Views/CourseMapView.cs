using GolfSG.Application.Courses;
using GolfSG.Core.Models;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

// Coordinates are fractions of the original image, independent of display size and zoom.
public sealed class CourseMapView : Grid, IDrawable
{
    private readonly Image image = new() { Aspect = Aspect.AspectFit };
    private readonly GraphicsView overlay;
    private readonly Grid picture = new() { HeightRequest = 570, IsClippedToBounds = true };
    private readonly Label distanceValue = new()
    {
        TextColor = GolfTheme.Colors.Text, FontSize = 24, FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center, InputTransparent = true
    };
    private readonly Label distanceCaption = new()
    {
        TextColor = GolfTheme.Colors.MutedText, FontSize = 14,
        VerticalOptions = LayoutOptions.Center, InputTransparent = true
    };
    private readonly Border distanceBadge;
    private bool movingFrom, interacting, hasGestureSnapshot, editable = true;
    private CoursePoint? originalFrom, originalTarget, dragOrigin, grabbedPoint;
    private CoursePracticeShot? originalShot;
    private RectF imageBounds;
    private PointF? selectionStart;
    public bool SelectSavedShots { get; set; }
    public IReadOnlySet<string> CompletedShotIds { get; set; } = new HashSet<string>();
#if IOS || MACCATALYST
    private UIKit.UIScrollView? capturedScroll;
    private bool scrollWasEnabled;
#endif
    public CoursePoint? From { get; set; }
    public CoursePoint? Target { get; set; }
    public IReadOnlyList<CoursePracticeShot> SavedShots { get; set; } = [];
    public string? SelectedShotId { get; set; }
    public string? EditingShotId { get; set; }
    public bool DragSavedShots { get; set; } = true;
    public bool MovingWholeShot { get; private set; }
    public double PictureHeight
    {
        get => picture.HeightRequest;
        set => picture.HeightRequest = value;
    }
    public double ImageAspectRatio { get; set; } = 941d / 1672;
    public bool PlaceFrom { get; set; } = true;
    public bool Editable
    {
        get => editable;
        set
        {
            if (!value && interacting) Cancel();
            if (!value) hasGestureSnapshot = false;
            editable = value;
        }
    }
    public float Zoom { get; private set; } = 1;
    public event Action<CoursePoint?, CoursePoint?>? PointsChanged;
    public event Action<CoursePracticeShot>? SavedShotChanged;
    public event Action<CoursePracticeShot?>? ShotSelected;
    public event Action? InteractionStarted;
    public event Action<bool>? InteractionFinished;

    public CourseMapView(string automationId = "CoursePractice.Map")
    {
        IsClippedToBounds = true;
        RowSpacing = 0;
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        overlay = new GraphicsView { Drawable = this };
        // Keep the image and its touch/drawing surface in the same clipped area.
        // The distance belongs outside it, including when the picture is zoomed.
        picture.Children.Add(image); picture.Children.Add(overlay);
        Children.Add(picture.Row(0));
        distanceValue.AutomationId = automationId == "CoursePractice.Map" ? "CoursePractice.MapDistance" : $"{automationId}.Distance";
        distanceBadge = new Border
        {
            BackgroundColor = GolfTheme.Colors.CardBackground,
            Stroke = GolfTheme.Colors.PrimaryGreen,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(12, 8), Margin = new Thickness(0, 8, 0, 0),
            InputTransparent = true, IsVisible = false,
            Content = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                ColumnSpacing = 12, InputTransparent = true,
                Children = { distanceCaption.Column(0), distanceValue.Column(1) }
            }
        };
        Children.Add(distanceBadge.Row(1));
        overlay.StartInteraction += (_, e) =>
        {
            if (!interacting) hasGestureSnapshot = false;
            if (e.Touches.Length != 1) { if (interacting) Cancel(); return; }
            if (!Editable && SelectSavedShots)
            {
                selectionStart = imageBounds.Contains(e.Touches[0]) ? e.Touches[0] : null;
                return;
            }
            if (!Editable || interacting || !imageBounds.Contains(e.Touches[0]) || imageBounds.Width <= 0 || imageBounds.Height <= 0) return;
            var p = e.Touches[0];
            originalFrom = From; originalTarget = Target;
            originalShot = null; MovingWholeShot = false;
            var fromHit = Hit(From, p); var targetHit = Hit(Target, p);
            if (From is null && Target is null && DragSavedShots)
            {
                var hit = FindSavedShot(p);
                if (hit is not null)
                {
                    originalShot = hit.Value.Shot;
                    movingFrom = hit.Value.From;
                    MovingWholeShot = hit.Value.Whole;
                }
            }
            if (originalShot is null)
            {
                movingFrom = fromHit && (!targetHit || PlaceFrom) || !targetHit && !fromHit && PlaceFrom;
                MovingWholeShot = !fromHit && !targetHit && From is not null && Target is not null && LineDistance(From, Target, p) <= 12;
            }
            dragOrigin = ToPoint(p);
            var selected = originalShot is not null ? (movingFrom ? originalShot.From : originalShot.Target) : (movingFrom ? From : Target);
            var markerHit = originalShot is not null || fromHit || targetHit;
            grabbedPoint = markerHit && selected is not null ? selected : dragOrigin;
            interacting = hasGestureSnapshot = true;
            BlockParentScroll(true);
            InteractionStarted?.Invoke();
            SelectedShotId = originalShot?.Id;
            ShotSelected?.Invoke(originalShot);
            Move(p);
        };
        overlay.DragInteraction += (_, e) =>
        {
            if (selectionStart is { } start && (e.Touches.Length != 1 ||
                Math.Abs(e.Touches[0].X - start.X) > 10 || Math.Abs(e.Touches[0].Y - start.Y) > 10)) selectionStart = null;
            if (!interacting) return;
            if (e.Touches.Length != 1) { Cancel(); return; }
            Move(e.Touches[0]);
        };
        overlay.EndInteraction += (_, e) =>
        {
            if (!Editable && SelectSavedShots && selectionStart is { } start)
            {
                selectionStart = null;
                if (e.Touches.Length == 1 && Math.Abs(e.Touches[0].X - start.X) <= 10 && Math.Abs(e.Touches[0].Y - start.Y) <= 10)
                {
                    var selected = FindSavedShot(e.Touches[0]);
                    if (selected is not null)
                    {
                        SelectedShotId = selected.Value.Shot.Id;
                        Refresh(); ShotSelected?.Invoke(selected.Value.Shot);
                    }
                }
                return;
            }
            if (!interacting) return;
            if (e.Touches.Length == 1) Move(e.Touches[0]);
            interacting = false;
            BlockParentScroll(false);
            InteractionFinished?.Invoke(false);
            // Windows raises EndInteraction immediately before CancelInteraction on cancellation.
            // Keep the snapshot until the next gesture so cancellation can still restore it.
        };
        overlay.CancelInteraction += (_, _) => { selectionStart = null; Cancel(); };
        Unloaded += (_, _) => { if (interacting) Cancel(); BlockParentScroll(false); };
        this.Accessible(automationId, "Banebillede. Tryk for at placere start og mål. Træk punkterne eller linjen for at flytte et slag.");
    }

    public void SetImage(string path) { if (interacting) Cancel(); image.Source = ImageSource.FromFile(path); Refresh(); }
    public void ToggleZoom() { if (interacting) Cancel(); Zoom = Zoom == 1 ? 2 : 1; image.Scale = Zoom; Refresh(); }
    public void Refresh() => overlay.Invalidate();

    public void SetDistanceSummary(double? metres, bool measured = false)
    {
        distanceBadge.IsVisible = metres is not null;
        if (metres is null) return;
        distanceCaption.Text = measured ? "Målt afstand" : "Estimeret afstand";
        distanceValue.Text = CoursePracticeViews.FormatDistanceMeters(metres.Value);
        SemanticProperties.SetDescription(distanceValue, $"{distanceCaption.Text}: {distanceValue.Text}");
    }

    private void BlockParentScroll(bool block)
    {
#if ANDROID
        // Keep an accepted drag on the map instead of letting the enclosing page steal it.
        (overlay.Handler?.PlatformView as Android.Views.View)?.Parent?.RequestDisallowInterceptTouchEvent(block);
#elif IOS || MACCATALYST
        if (block && capturedScroll is null)
        {
            for (var parent = (overlay.Handler?.PlatformView as UIKit.UIView)?.Superview; parent is not null; parent = parent.Superview)
            {
                if (parent is not UIKit.UIScrollView scroll) continue;
                capturedScroll = scroll; scrollWasEnabled = scroll.ScrollEnabled; scroll.ScrollEnabled = false;
                break;
            }
        }
        else if (!block && capturedScroll is not null)
        {
            capturedScroll.ScrollEnabled = scrollWasEnabled; capturedScroll = null;
        }
#endif
    }
    private void Cancel()
    {
        BlockParentScroll(false);
        if (!hasGestureSnapshot) return;
        interacting = hasGestureSnapshot = false;
        if (originalShot is not null) SavedShotChanged?.Invoke(originalShot);
        else { From = originalFrom; Target = originalTarget; PointsChanged?.Invoke(From, Target); }
        Refresh(); InteractionFinished?.Invoke(true);
    }
    private PointF Screen(CoursePoint p) => new(imageBounds.Left + (float)p.X * imageBounds.Width, imageBounds.Top + (float)p.Y * imageBounds.Height);
    private double PointDistance(CoursePoint p, PointF touch)
    {
        var screen = Screen(p);
        return Math.Sqrt(Math.Pow(screen.X - touch.X, 2) + Math.Pow(screen.Y - touch.Y, 2));
    }
    private bool Hit(CoursePoint? p, PointF touch) => p is not null && PointDistance(p, touch) <= 22;
    private double LineDistance(CoursePoint from, CoursePoint target, PointF touch)
    {
        var a = Screen(from); var b = Screen(target);
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return double.MaxValue;
        var t = Math.Clamp(((touch.X - a.X) * dx + (touch.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return Math.Sqrt(Math.Pow(touch.X - (a.X + t * dx), 2) + Math.Pow(touch.Y - (a.Y + t * dy), 2));
    }
    private (CoursePracticeShot Shot, bool From, bool Whole)? FindSavedShot(PointF touch)
    {
        (CoursePracticeShot Shot, bool From, bool Whole)? best = null;
        double distance = 22;
        foreach (var shot in SavedShots.OrderBy(s => s.Id == SelectedShotId ? 1 : 0))
        {
            var from = PointDistance(shot.From, touch); var target = PointDistance(shot.Target, touch);
            if (Math.Min(from, target) <= distance)
            { best = (shot, from <= target, false); distance = Math.Min(from, target); }
        }
        if (best is not null) return best;
        distance = 12;
        foreach (var shot in SavedShots.OrderBy(s => s.Id == SelectedShotId ? 1 : 0))
        {
            var line = LineDistance(shot.From, shot.Target, touch);
            if (line <= distance) { best = (shot, false, true); distance = line; }
        }
        return best;
    }
    private CoursePoint ToPoint(PointF p) => new((p.X - imageBounds.Left) / imageBounds.Width, (p.Y - imageBounds.Top) / imageBounds.Height);
    private void Move(PointF p)
    {
        var position = ToPoint(p);
        if (originalShot is not null)
        {
            var moved = MovingWholeShot
                ? CourseShotPositioning.Translate(originalShot, position.X - dragOrigin!.X, position.Y - dragOrigin.Y)
                : CourseShotPositioning.MoveEndpoint(originalShot, movingFrom,
                    new(grabbedPoint!.X + (position.X - dragOrigin!.X), grabbedPoint.Y + (position.Y - dragOrigin.Y)));
            var current = SavedShots.FirstOrDefault(s => s.Id == originalShot.Id);
            if (current != moved) SavedShotChanged?.Invoke(moved);
        }
        else
        {
            var from = From; var target = Target;
            if (MovingWholeShot)
            {
                var moved = CourseShotPositioning.Translate(new("preview", "preview", originalFrom!, originalTarget!, ShotLie.Fairway, PracticeShotCategory.Approach),
                    position.X - dragOrigin!.X, position.Y - dragOrigin.Y);
                From = moved.From; Target = moved.Target;
            }
            else
            {
                var point = new CoursePoint(Math.Clamp(grabbedPoint!.X + (position.X - dragOrigin!.X), 0, 1),
                    Math.Clamp(grabbedPoint.Y + (position.Y - dragOrigin.Y), 0, 1));
                if (movingFrom) From = point; else Target = point;
            }
            if (from != From || target != Target) PointsChanged?.Invoke(From, Target);
        }
        Refresh();
    }
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var width = Math.Min(dirtyRect.Width, dirtyRect.Height * (float)ImageAspectRatio) * Zoom;
        var height = width / (float)ImageAspectRatio;
        imageBounds = new((dirtyRect.Width - width) / 2, (dirtyRect.Height - height) / 2, width, height);
        void Marker(CoursePoint? p, string label, Color color)
        {
            if (p is null) return;
            var screen = Screen(p);
            canvas.FillColor = color; canvas.FillCircle(screen.X, screen.Y, 12);
            canvas.DrawCircle(screen.X, screen.Y, 12);
            canvas.FontColor = Colors.White; canvas.FontSize = 14;
            canvas.DrawString(label, screen.X - 12, screen.Y - 12, 24, 24, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
        void Shot(CoursePoint? from, CoursePoint? target, string fromLabel, string targetLabel, bool completed = false)
        {
            canvas.StrokeColor = Colors.White; canvas.StrokeSize = 2;
            if (from is not null && target is not null) canvas.DrawLine(Screen(from), Screen(target));
            Marker(from, fromLabel, completed ? Colors.DimGray : GolfTheme.Colors.PrimaryGreen); Marker(target, targetLabel, completed ? Colors.DimGray : Colors.OrangeRed);
        }
        foreach (var item in SavedShots.Select((shot, index) => (Shot: shot, Number: index + 1)).Where(s => s.Shot.Id != EditingShotId).OrderBy(s => s.Shot.Id == SelectedShotId ? 1 : 0))
        {
            canvas.Alpha = item.Shot.Id == SelectedShotId ? 1 : .65f;
            Shot(item.Shot.From, item.Shot.Target, item.Number.ToString(), item.Number.ToString(), CompletedShotIds.Contains(item.Shot.Id));
        }
        canvas.Alpha = 1;
        Shot(From, Target, "1", "2");
    }
}
