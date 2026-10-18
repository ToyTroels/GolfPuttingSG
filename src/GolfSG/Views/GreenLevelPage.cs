using GolfSG.Application.Services;
using Microsoft.Maui.Devices.Sensors;

namespace GolfSG.Views;

public sealed class GreenLevelPage : ContentPage
{
    private readonly Label status = new() { Text = "Læg telefonen på plads og tryk Start måling. Målingen starter et halvt sekund efter trykket.", FontSize = 16 };
    private readonly Label sideways = new() { Text = "Sidehældning: —", FontSize = 23, TextColor = GolfTheme.Colors.Text };
    private readonly Label forward = new() { Text = "Mod hullet: —", FontSize = 23, TextColor = GolfTheme.Colors.Text };
    private readonly Label calibrationStatus = new() { FontSize = 14 };
    private readonly Button start = AppViews.PrimaryButton("Start måling");
    private readonly Button calibrate = AppViews.SecondaryButton("Kalibrér på vandret flade");
    private readonly Button cancel = AppViews.SecondaryButton("Annuller måling");
    private readonly Button reset = AppViews.SecondaryButton("Nulstil kalibrering");
    private readonly ProgressBar progress = new()
    {
        ProgressColor = GolfTheme.Colors.PrimaryGreen, HeightRequest = 8,
        IsVisible = false, AutomationId = "green-level.progress"
    };
    private readonly GraphicsView level;
    private readonly LevelDrawing drawing = new();
    private GreenLevelCapture capture = new();
    private GreenLevelFilter filter => capture.Filter;
    private readonly System.Diagnostics.Stopwatch captureTime = new();
    private CancellationTokenSource? measurement;
    private int sensorGeneration;
#if ANDROID
    private Android.Content.PM.ScreenOrientation? previousOrientation;
#endif
    private bool listening;
    private bool visible;
    private double rightOffset;
    private double forwardOffset;

    public GreenLevelPage()
    {
        Title = "Vaterpas til green";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        rightOffset = Preferences.Default.Get("green-level.right-offset", 0d);
        forwardOffset = Preferences.Default.Get("green-level.forward-offset", 0d);
        if (!double.IsFinite(rightOffset) || !double.IsFinite(forwardOffset) ||
            Math.Abs(rightOffset) > 0.2 || Math.Abs(forwardOffset) > 0.2)
            rightOffset = forwardOffset = 0;
        level = new GraphicsView { Drawable = drawing, HeightRequest = 220, AutomationId = "green-level.bubble" };
        SemanticProperties.SetDescription(level, "Vaterpas. Boblen viser den høje side i den færdige måling.");
        start.AutomationId = "green-level.start";
        calibrate.AutomationId = "green-level.calibrate";
        cancel.AutomationId = "green-level.cancel";
        cancel.IsVisible = false;
        start.Clicked += async (_, _) => await MeasureAsync(false);
        calibrate.Clicked += async (_, _) =>
        {
            if (measurement is not null) return;
            if (await DisplayAlertAsync("Kalibrér", "Læg telefonen på en kendt vandret, fast flade, ikke greenens skråning. Tryk Start og slip telefonen. Kalibreringen starter et halvt sekund efter trykket.", "Start", "Annuller"))
                await MeasureAsync(true);
        };
        cancel.Clicked += (_, _) => CancelMeasurement("Målingen er annulleret. Tryk Start måling for at prøve igen.");
        reset.Clicked += (_, _) =>
        {
            rightOffset = forwardOffset = 0;
            Preferences.Default.Remove("green-level.right-offset");
            Preferences.Default.Remove("green-level.forward-offset");
            UpdateCalibrationText();
            ClearResult();
            status.Text = "Kalibrering nulstillet · tryk Start måling.";
        };
        UpdateCalibrationText();
        var holeDirection = new Label
        {
            Text = "MOD\nHULLET\n→", FontSize = 24, FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center,
            AutomationId = "green-level.hole-direction"
        };
        SemanticProperties.SetDescription(holeDirection, "Peg telefonens højre langside mod hullet. Telefonens længde ligger på tværs af puttelinjen.");
        var levelPanel = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(90)) },
            Children = { level, holeDirection }
        };
        Grid.SetColumn(holeDirection, 1);
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 14,
            Children =
            {
                AppViews.PageTitle("Greenens hældning"),
                new Label { Text = "Læg telefonen med skærmen opad. Peg højre langside og pilen mod hullet, så telefonens længde ligger på tværs af puttelinjen. Tryk Start måling og slip telefonen. Venstre/højre ses fra bolden mod hullet." },
                levelPanel, sideways, forward, status, progress, start, cancel, calibrate, calibrationStatus, reset,
                new Label { Text = "Et halvt sekunds pause efter trykket. Målingen stopper, så snart der er nok stabile aflæsninger, dog senest efter 3 sekunder. Bjælken viser måletiden, ikke nøjagtigheden. Resultatet bliver stående, så du kan løfte telefonen." },
                new Label { Text = "Boblen viser den høje side. 1 % svarer til 1 cm pr. meter. Boblen stopper ved kanten ved 5 %." },
                new Label { Text = "Øvelsesværktøj: Måler kun hældningen under telefonen, ikke hele puttets break. Kamerabule, cover og græs kan påvirke resultatet. Decimaler er ikke en garanti for nøjagtighed." }
            }
        } };
    }

    private async Task MeasureAsync(bool calibration)
    {
        if (!visible || measurement is not null) return;
        if (!Accelerometer.Default.IsSupported)
        {
            status.Text = "Denne enhed understøtter ikke vaterpas.";
            return;
        }
        var run = new CancellationTokenSource();
        measurement = run;
        SetBusy(true);
        progress.Progress = 0;
        progress.IsVisible = true;
        SemanticProperties.SetDescription(progress, "Måletid, højst 3 sekunder. Stopper tidligere ved stabil aflæsning.");
        ClearResult();
        try
        {
            status.Text = "Slip telefonen · målingen starter straks…";
            await Task.Delay(500, run.Token);
            run.Token.ThrowIfCancellationRequested();
            if (Accelerometer.Default.IsMonitoring)
                throw new InvalidOperationException("Bevægelsessensoren er i brug. Prøv igen.");
            capture = new GreenLevelCapture();
            captureTime.Restart();
            sensorGeneration++;
            Accelerometer.Default.ReadingChanged += OnReading;
            listening = true;
            Accelerometer.Default.Start(SensorSpeed.Game);
            status.Text = "Måler · lad telefonen ligge stille…";
            var complete = false;
            while (captureTime.Elapsed < GreenLevelCapture.MaxDuration)
            {
                var remaining = GreenLevelCapture.MaxDuration - captureTime.Elapsed;
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50), run.Token);
                progress.Progress = Math.Clamp(captureTime.Elapsed.TotalSeconds / GreenLevelCapture.MaxDuration.TotalSeconds, 0, 1);
                status.Text = $"Måler · {Math.Min(captureTime.Elapsed.TotalSeconds, 3):0.0} / 3 sek. · hold telefonen stille…";
                if (capture.IsComplete(captureTime.Elapsed)) { complete = true; break; }
            }
            run.Token.ThrowIfCancellationRequested();
            // Stop before showing the result so picking up the phone cannot change it.
            StopSensor();
            progress.Progress = 1;
            if (!complete)
            {
                status.Text = "3 sekunder er gået uden en stabil måling. Sensor stoppet. Læg telefonen stille og prøv igen.";
                return;
            }
            if (calibration)
            {
                var right = Math.Atan2(filter.X, filter.Z);
                var top = Math.Atan2(filter.Y, filter.Z);
                if (Math.Abs(right) > 0.2 || Math.Abs(top) > 0.2)
                {
                    status.Text = "Fladen hælder for meget til kalibrering. Brug en kendt vandret flade.";
                    return;
                }
                rightOffset = right;
                forwardOffset = top;
                Preferences.Default.Set("green-level.right-offset", rightOffset);
                Preferences.Default.Set("green-level.forward-offset", forwardOffset);
                UpdateCalibrationText();
            }
            RenderResult();
            status.Text = calibration ? "Kalibrering gemt · sensor stoppet. Klar til en ny måling."
                : "Måling færdig · sensor stoppet. Du kan løfte telefonen.";
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { }
        catch (Exception)
        {
            progress.IsVisible = false;
            status.Text = "Målingen kunne ikke gennemføres. Kontrollér bevægelsessensoren og prøv igen.";
        }
        finally
        {
            StopSensor();
            if (measurement == run)
            {
                measurement = null;
                SetBusy(false);
            }
            run.Dispose();
        }
    }

    private void OnReading(object? sender, AccelerometerChangedEventArgs e)
    {
        var a = e.Reading.Acceleration;
        var generation = sensorGeneration;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!visible || !listening || generation != sensorGeneration) return;
            capture.Add(a.X, a.Y, a.Z, captureTime.Elapsed);
        });
    }

    private void SetBusy(bool busy)
    {
        start.IsEnabled = calibrate.IsEnabled = reset.IsEnabled = !busy;
        cancel.IsVisible = busy;
    }

    private void RenderResult()
    {
        var deviceSlope = GreenSlope.FromGravity(filter.X, filter.Y, filter.Z, rightOffset, forwardOffset);
        var slope = deviceSlope.WithHoleAtRightEdge();
        sideways.Text = Math.Abs(slope.RightPercent) < 0.05 ? "Sidehældning: 0,0 %" :
            $"{Math.Abs(slope.RightPercent):0.0} % · falder mod {(slope.RightPercent > 0 ? "venstre" : "højre")}";
        forward.Text = Math.Abs(slope.ForwardPercent) < 0.05 ? "Mod hullet: 0,0 %" :
            $"{Math.Abs(slope.ForwardPercent):0.0} % · {(slope.ForwardPercent > 0 ? "opad" : "nedad")} mod hullet";
        drawing.Right = deviceSlope.RightPercent;
        drawing.Forward = deviceSlope.ForwardPercent;
        level.Invalidate();
    }

    private void ClearResult()
    {
        filter.Reset();
        sideways.Text = "Sidehældning: —";
        forward.Text = "Mod hullet: —";
        drawing.Right = drawing.Forward = 0;
        level.Invalidate();
    }

    private void StopSensor()
    {
        sensorGeneration++;
        if (!listening) return;
        listening = false;
        Accelerometer.Default.ReadingChanged -= OnReading;
        Accelerometer.Default.Stop();
    }

    private void CancelMeasurement(string message)
    {
        if (measurement is null) return;
        measurement.Cancel();
        progress.IsVisible = false;
        StopSensor();
        status.Text = message;
    }

    private void UpdateCalibrationText() => calibrationStatus.Text =
        Preferences.Default.ContainsKey("green-level.right-offset") ? "Kalibrering gemt på denne telefon." : "Bruger telefonens standardnulpunkt. Kalibrér før brug.";

    protected override void OnAppearing()
    {
        base.OnAppearing();
        visible = true;
#if ANDROID
        if (Platform.CurrentActivity is { } activity)
        {
            previousOrientation = activity.RequestedOrientation;
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;
        }
#endif
        if (Window is { } window) window.Deactivated += OnDeactivated;
    }

    private void OnDeactivated(object? sender, EventArgs e) =>
        CancelMeasurement("Målingen blev afbrudt. Tryk Start måling for at prøve igen.");

    protected override void OnDisappearing()
    {
        visible = false;
        if (Window is { } window) window.Deactivated -= OnDeactivated;
        CancelMeasurement("Målingen blev afbrudt. Tryk Start måling for at prøve igen.");
        StopSensor();
#if ANDROID
        if (previousOrientation is { } orientation && Platform.CurrentActivity is { } activity)
            activity.RequestedOrientation = orientation;
        previousOrientation = null;
#endif
        base.OnDisappearing();
    }

    private sealed class LevelDrawing : IDrawable
    {
        public double Right { get; set; }
        public double Forward { get; set; }
        public void Draw(ICanvas canvas, RectF rect)
        {
            float cx = rect.Center.X, cy = rect.Center.Y;
            float radius = Math.Min(rect.Width, rect.Height) / 2 - 18;
            canvas.FillColor = GolfTheme.Colors.SoftGreen;
            canvas.FillCircle(cx, cy, radius);
            canvas.StrokeColor = GolfTheme.Colors.PrimaryGreen;
            canvas.StrokeSize = 2;
            canvas.DrawCircle(cx, cy, radius);
            canvas.DrawCircle(cx, cy, 14);
            canvas.DrawLine(cx - radius, cy, cx + radius, cy);
            canvas.DrawLine(cx, cy - radius, cx, cy + radius);
            var dx = Right / 5;
            var dy = -Forward / 5;
            var magnitude = Math.Sqrt(dx * dx + dy * dy);
            if (magnitude > 1) { dx /= magnitude; dy /= magnitude; }
            canvas.FillColor = GolfTheme.Colors.PrimaryGreen;
            canvas.FillCircle(cx + (float)dx * (radius - 12), cy + (float)dy * (radius - 12), 10);
        }
    }
}
