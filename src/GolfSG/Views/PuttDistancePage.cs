using GolfSG.Services;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class PuttDistancePage : ContentPage
{
    private static readonly TimeSpan WarmupDelay = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan FinishDelay = TimeSpan.FromSeconds(3);
    private const string StepLengthKey = "putt-step-length-cm";
    private readonly Label status = new() { Text = "Stå ved bolden. Tryk Start og vent på Klar, før du går til hullet med telefonen i lommen.", FontSize = 18 };
    private readonly Entry stepLength = new() { Keyboard = Keyboard.Numeric, AutomationId = "putt-measure.step-length" };
    private readonly Button start = AppViews.PrimaryButton("Start");
    private readonly Button done = AppViews.SecondaryButton("Færdig");
    private readonly Button apply = AppViews.PrimaryButton("Brug afstand");
    private readonly Action<double> selectDistance;
    private readonly Func<IPuttStepCounter> createCounter;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly IPreferences preferences;
    private readonly HorizontalStackLayout corrections = new() { Spacing = 12, IsVisible = false };
    private IPuttStepCounter? counter;
    private CancellationTokenSource? session;
    private int steps;
    private double lengthCentimeters;
    private bool ready;
    private bool finishing;
    private bool hasResult;
    private bool visible;
    private bool foreground;
    private CancellationTokenSource? permissionRequest;

    public PuttDistancePage(Action<double> selectDistance,
        Func<IPuttStepCounter>? createCounter = null, IPreferences? preferences = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.selectDistance = selectDistance;
        this.createCounter = createCounter ?? (() => new PuttStepCounter());
        this.preferences = preferences ?? Preferences.Default;
        this.delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        Title = "Mål med skridt";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        var savedLength = this.preferences.Get(StepLengthKey, PuttDistanceEstimate.DefaultStepLengthCentimeters);
        if (!PuttDistanceEstimate.IsValidStepLength(savedLength)) savedLength = PuttDistanceEstimate.DefaultStepLengthCentimeters;
        stepLength.Text = savedLength.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
        start.AutomationId = "putt-measure.start";
        done.AutomationId = "putt-measure.done";
        apply.AutomationId = "putt-measure.apply";
        done.IsEnabled = apply.IsEnabled = false;
        start.Clicked += async (_, _) => await StartAsync();
        done.Clicked += async (_, _) => await FinishAsync();
        var fewer = AppViews.SecondaryButton("− 1 skridt");
        var more = AppViews.SecondaryButton("+ 1 skridt");
        fewer.AutomationId = "putt-measure.remove-step";
        more.AutomationId = "putt-measure.add-step";
        fewer.Clicked += (_, _) => { if (hasResult) { steps = Math.Max(0, steps - 1); UpdateResult(); } };
        more.Clicked += (_, _) => { if (hasResult && steps < int.MaxValue) { steps++; UpdateResult(); } };
        corrections.Children.Add(fewer);
        corrections.Children.Add(more);
        apply.Clicked += async (_, _) => await this.RunNavigationOnceAsync(async () =>
        {
            if (!hasResult) return;
            var meters = PuttDistanceEstimate.Meters(steps, lengthCentimeters);
            if (!PuttDistanceEstimate.CanApply(meters)) return;
            selectDistance(meters);
            await Navigation.PopAsync();
        });
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 18,
            Children =
            {
                status,
                new Label { Text = "Skridtlængde (cm). Kalibrér ved at gå en kendt afstand og dividere den med antal skridt." },
                stepLength,
                new Label { Text = "Afstanden er et estimat. Korte og halve skridt kan give unøjagtige resultater. Hold skærmen åben under målingen." },
                start, done, corrections,
                new Label { Text = "Telefonen kan overse de første skridt på meget korte ture. Kontrollér antallet og ret med −/+ efter målingen." },
                apply
            }
        } };
    }

    private async Task StartAsync()
    {
        if (!visible || !foreground || session is not null) return;
        if (!DistanceInputParser.TryParse(stepLength.Text, out var cm) || !PuttDistanceEstimate.IsValidStepLength(cm))
        {
            await DisplayAlertAsync("Skridtlængde", "Angiv en skridtlængde mellem 20 og 150 cm.", "OK");
            return;
        }
        preferences.Set(StepLengthKey, cm);
        lengthCentimeters = cm;
        steps = 0;
        ready = finishing = hasResult = false;
        corrections.IsVisible = false;
        start.IsEnabled = stepLength.IsEnabled = apply.IsEnabled = done.IsEnabled = false;
        var run = new CancellationTokenSource();
        session = run;
        var cancellationToken = run.Token;
        try
        {
            var measurement = createCounter();
            counter = measurement;
            measurement.StepsChanged += count => Dispatcher.Dispatch(() =>
            {
                if (session != run || run.IsCancellationRequested) return;
                steps = Math.Max(0, count);
                if (ready) status.Text = finishing
                    ? $"Stå stille · modtager de sidste skridt ({steps})…"
                    : $"{steps} skridt · ca. {PuttDistanceEstimate.Meters(steps, lengthCentimeters):0.0} m";
            });
            measurement.Failed += message => Dispatcher.Dispatch(() =>
            {
                if (session == run) CancelSession(message);
            });
            status.Text = "Klargør skridtmåler · bliv stående ved bolden…";
            permissionRequest = run;
            try { await measurement.StartAsync(cancellationToken); }
            finally { if (permissionRequest == run) permissionRequest = null; }
            if (!visible || !foreground) CancelSession("Målingen blev afbrudt. Tryk Start for at prøve igen.");
            await delay(WarmupDelay, cancellationToken);
            if (session == run && !run.IsCancellationRequested)
            {
                ready = true;
                done.IsEnabled = true;
                status.Text = $"Klar · gå nu til hullet. {steps} skridt · ca. {PuttDistanceEstimate.Meters(steps, lengthCentimeters):0.0} m";
            }
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceError("Step measurement startup failed: {0}", error);
            if (session == run) CancelSession(error is InvalidOperationException ? error.Message
                : "Skridtmåling kunne ikke startes. Du kan stadig indtaste afstanden manuelt.");
        }
        finally
        {
            // The session stays alive while walking; failed/cancelled starts release it.
            if (session != run) run.Dispose();
        }
    }

    private async Task FinishAsync()
    {
        if (!ready || finishing || session is not { } run) return;
        finishing = true;
        done.IsEnabled = false;
        status.Text = "Stå stille · modtager de sidste skridt…";
        try
        {
            await delay(FinishDelay, run.Token);
            if (session != run || run.IsCancellationRequested) return;
            EndSession();
            hasResult = true;
            corrections.IsVisible = true;
            UpdateResult();
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested) { }
        finally { run.Dispose(); }
    }

    private void EndSession()
    {
        var previous = session;
        session = null;
        previous?.Cancel();
        previous?.Dispose();
        counter?.Dispose();
        counter = null;
        done.IsEnabled = false;
        start.IsEnabled = stepLength.IsEnabled = true;
    }

    private void CancelSession(string message)
    {
        EndSession();
        hasResult = false;
        corrections.IsVisible = false;
        apply.IsEnabled = false;
        status.Text = message;
    }

    private void UpdateResult()
    {
        var distance = PuttDistanceEstimate.Meters(steps, lengthCentimeters);
        apply.IsEnabled = hasResult && PuttDistanceEstimate.CanApply(distance);
        status.Text = distance > PuttDistanceEstimate.MaximumDistanceMeters ? "Afstanden overstiger 30 m. Prøv igen eller indtast manuelt."
            : $"{steps} skridt · ca. {distance:0.0} m. Kontrollér estimatet før du bruger det.";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        visible = foreground = true;
        if (Window is { } window)
        {
            window.Deactivated += OnWindowDeactivated;
            window.Activated += OnWindowActivated;
        }
    }

    private void OnWindowActivated(object? sender, EventArgs e) => foreground = true;
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        foreground = false;
        if (session is not null && session != permissionRequest)
            CancelSession("Målingen blev afbrudt. Tryk Start for at prøve igen.");
    }

    protected override void OnDisappearing()
    {
        visible = foreground = false;
        if (Window is { } window)
        {
            window.Deactivated -= OnWindowDeactivated;
            window.Activated -= OnWindowActivated;
        }
        if (session is not null) CancelSession("Målingen blev afbrudt. Tryk Start for at prøve igen.");
        base.OnDisappearing();
    }
}
