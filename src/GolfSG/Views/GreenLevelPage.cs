using GolfSG.Application.Services;
using Microsoft.Maui.Devices.Sensors;

namespace GolfSG.Views;

public sealed partial class GreenLevelPage : ContentPage
{
    private readonly Label status = new() { Text = "Læg telefonen på plads og tryk Start måling. Målingen starter et halvt sekund efter trykket.", FontSize = 16 };
    private readonly Label sideways = new() { Text = "Sidehældning: —", FontSize = 23, TextColor = GolfTheme.Colors.Text };
    private readonly Label forward = new() { Text = "Mod hullet: —", FontSize = 23, TextColor = GolfTheme.Colors.Text };
    private readonly Label calibrationStatus = new() { FontSize = 14 };
    private readonly Button start = AppViews.PrimaryButton("Start måling");
    private readonly Button calibrate = AppViews.SecondaryButton("Kalibrér på vandret flade");
    private readonly Button cancel = AppViews.SecondaryButton("Annuller måling");
    private readonly Button reset = AppViews.SecondaryButton("Nulstil kalibrering");
    private readonly Picker directionPicker = new()
    {
        Title = "Kant mod hullet", AutomationId = "green-level.orientation",
        ItemsSource = new[] { "Øverste kant ↑", "Højre langside →", "Nederste kant ↓", "Venstre langside ←" }
    };
    private GreenLevelHoleDirection holeDirection = GreenLevelHoleDirection.Right;
    private readonly ProgressBar progress = new()
    {
        ProgressColor = GolfTheme.Colors.PrimaryGreen, HeightRequest = 8,
        IsVisible = false, AutomationId = "green-level.progress"
    };
    private readonly GraphicsView level;
    private readonly LevelDrawing drawing = new();
    private EventHandler<AccelerometerChangedEventArgs>? readingHandler;
    private CancellationTokenSource? measurement;
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
        if (!Preferences.Default.ContainsKey("green-level.right-offset") ||
            !Preferences.Default.ContainsKey("green-level.forward-offset") ||
            !new GreenLevelCalibration(rightOffset, forwardOffset).IsValid)
        {
            rightOffset = forwardOffset = 0;
            Preferences.Default.Remove("green-level.right-offset");
            Preferences.Default.Remove("green-level.forward-offset");
        }
        level = new GraphicsView { Drawable = drawing, HeightRequest = 220, AutomationId = "green-level.bubble" };
        BuildLayout();
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
            var capture = new GreenLevelCapture();
            var captureTime = System.Diagnostics.Stopwatch.StartNew();
            readingHandler = (_, e) =>
            {
                var a = e.Reading.Acceleration;
                // Use callback time, not time spent waiting in the UI dispatch queue.
                capture.Add(a.X, a.Y, a.Z, captureTime.Elapsed);
            };
            Accelerometer.Default.ReadingChanged += readingHandler;
            listening = true;
            Accelerometer.Default.Start(SensorSpeed.Game);
            status.Text = "Måler · lad telefonen ligge stille…";
            GreenLevelReading reading;
            while (captureTime.Elapsed < GreenLevelCapture.MaxDuration)
            {
                var remaining = GreenLevelCapture.MaxDuration - captureTime.Elapsed;
                if (remaining <= TimeSpan.Zero) break;
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50), run.Token);
                progress.Progress = Math.Clamp(captureTime.Elapsed.TotalSeconds / GreenLevelCapture.MaxDuration.TotalSeconds, 0, 1);
                status.Text = $"Måler · {Math.Min(captureTime.Elapsed.TotalSeconds, 3):0.0} / 3 sek. · hold telefonen stille…";
                if (capture.TryGetResult(out _)) break;
            }
            run.Token.ThrowIfCancellationRequested();
            // Stop before showing the result so picking up the phone cannot change it.
            StopSensor();
            progress.Progress = 1;
            if (!capture.TryGetResult(out reading))
            {
                status.Text = "3 sekunder er gået uden en stabil måling. Sensor stoppet. Læg telefonen stille og prøv igen.";
                return;
            }
            if (calibration)
            {
                GreenLevelCalibration offsets;
                try { offsets = GreenLevelCalibration.FromReading(reading); }
                catch (ArgumentOutOfRangeException)
                {
                    status.Text = "Fladen hælder for meget til kalibrering. Brug en kendt vandret flade.";
                    return;
                }
                rightOffset = offsets.RightRadians;
                forwardOffset = offsets.ForwardRadians;
                Preferences.Default.Set("green-level.right-offset", rightOffset);
                Preferences.Default.Set("green-level.forward-offset", forwardOffset);
                UpdateCalibrationText();
            }
            RenderResult(reading);
            status.Text = calibration ? "Kalibrering gemt · sensor stoppet. Klar til en ny måling."
                : "Måling færdig · sensor stoppet. Du kan løfte telefonen.";
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError("Green level measurement failed: {0}", error);
            progress.IsVisible = false;
            status.Text = error is InvalidOperationException ? error.Message
                : "Målingen kunne ikke gennemføres. Kontrollér bevægelsessensoren og prøv igen.";
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

    private void SetBusy(bool busy)
    {
        start.IsEnabled = calibrate.IsEnabled = reset.IsEnabled = directionPicker.IsEnabled = !busy;
        cancel.IsVisible = busy;
    }

    private void RenderResult(GreenLevelReading reading)
    {
        var deviceSlope = reading.ToSlope(rightOffset, forwardOffset);
        var slope = deviceSlope.WithHoleDirection(holeDirection);
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
        sideways.Text = "Sidehældning: —";
        forward.Text = "Mod hullet: —";
        drawing.Right = drawing.Forward = 0;
        level.Invalidate();
    }

    private void StopSensor()
    {
        if (!listening) return;
        listening = false;
        Accelerometer.Default.ReadingChanged -= readingHandler;
        readingHandler = null;
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

}
