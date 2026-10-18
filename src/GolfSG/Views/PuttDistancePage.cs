using GolfSG.Services;
using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class PuttDistancePage : ContentPage
{
    private readonly Label status = new() { Text = "Stå ved bolden. Tryk Start og vent på Klar, før du går til hullet med telefonen i lommen.", FontSize = 18 };
    private readonly Entry stepLength = new() { Keyboard = Keyboard.Numeric, AutomationId = "putt-measure.step-length" };
    private readonly Button start = new() { Text = "Start" };
    private readonly Button done = new() { Text = "Færdig", IsEnabled = false };
    private readonly Button apply = new() { Text = "Brug afstand", IsEnabled = false };
    private readonly Action<double> selectDistance;
    private PuttStepCounter? counter;
    private int steps;
    private double lengthMeters;
    private bool active;
    private bool finishing;
    private bool ready;
    private readonly HorizontalStackLayout corrections = new() { Spacing = 12, IsVisible = false };

    public PuttDistancePage(Action<double> selectDistance)
    {
        this.selectDistance = selectDistance;
        Title = "Mål med skridt";
        stepLength.Text = Preferences.Default.Get("putt-step-length-cm", 75d).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture);
        start.Clicked += async (_, _) => await StartAsync();
        done.Clicked += async (_, _) => await FinishAsync();
        var fewer = new Button { Text = "− 1 skridt", AutomationId = "putt-measure.remove-step" };
        var more = new Button { Text = "+ 1 skridt", AutomationId = "putt-measure.add-step" };
        fewer.Clicked += (_, _) => { steps = Math.Max(0, steps - 1); UpdateResult(); };
        more.Clicked += (_, _) => { steps++; UpdateResult(); };
        corrections.Children.Add(fewer);
        corrections.Children.Add(more);
        apply.Clicked += async (_, _) =>
        {
            apply.IsEnabled = false;
            selectDistance(Math.Round(steps * lengthMeters, 1));
            await Navigation.PopAsync();
        };
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
        if (!DistanceInputParser.TryParse(stepLength.Text, out var cm) || cm < 20 || cm > 150)
        {
            await DisplayAlertAsync("Skridtlængde", "Angiv en skridtlængde mellem 20 og 150 cm.", "OK");
            return;
        }
        Preferences.Default.Set("putt-step-length-cm", cm);
        lengthMeters = cm / 100;
        steps = 0;
        active = true;
        ready = finishing = false;
        corrections.IsVisible = false;
        start.IsEnabled = stepLength.IsEnabled = apply.IsEnabled = false;
        var measurement = new PuttStepCounter();
        counter = measurement;
        measurement.StepsChanged += count => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!active || counter != measurement) return;
            if (count < 0) { Stop(); status.Text = "Motionstilladelse mangler, eller skridtmålingen kunne ikke fortsætte."; apply.IsEnabled = false; return; }
            steps = count;
            if (ready) status.Text = finishing
                ? $"Stå stille · modtager de sidste skridt ({steps})…"
                : $"{steps} skridt · ca. {steps * lengthMeters:0.0} m";
        });
        status.Text = "Klargør skridtmåler · bliv stående ved bolden…";
        try
        {
            await measurement.StartAsync();
            await Task.Delay(2500);
            if (active && counter == measurement)
            {
                ready = true;
                done.IsEnabled = true;
                status.Text = $"Klar · gå nu til hullet. {steps} skridt · ca. {steps * lengthMeters:0.0} m";
            }
        }
        catch (Exception)
        {
            if (!active || counter != measurement) return;
            Stop();
            status.Text = "Skridtmåling kunne ikke startes. Kontrollér telefonens understøttelse og tilladelse til fysisk aktivitet/bevægelse. Du kan stadig indtaste afstanden manuelt.";
            apply.IsEnabled = false;
        }
    }

    private async Task FinishAsync()
    {
        if (!active || finishing) return;
        finishing = true;
        done.IsEnabled = false;
        var measurement = counter;
        status.Text = "Stå stille · modtager de sidste skridt…";
        await Task.Delay(3000);
        if (!active || counter != measurement) return;
        Stop();
        corrections.IsVisible = true;
    }

    private void Stop()
    {
        active = false;
        counter?.Dispose();
        counter = null;
        done.IsEnabled = false;
        start.IsEnabled = stepLength.IsEnabled = true;
        UpdateResult();
    }

    private void UpdateResult()
    {
        var distance = Math.Round(steps * lengthMeters, 1);
        apply.IsEnabled = distance > 0 && distance <= 30;
        status.Text = distance > 30 ? "Afstanden overstiger 30 m. Prøv igen eller indtast manuelt."
            : $"{steps} skridt · ca. {distance:0.0} m. Kontrollér estimatet før du bruger det.";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (Window is { } window) window.Deactivated += OnWindowDeactivated;
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (!active || !ready) return;
        Stop();
        apply.IsEnabled = false;
        status.Text = "Målingen blev afbrudt. Tryk Start for at prøve igen.";
    }

    protected override void OnDisappearing()
    {
        if (Window is { } window) window.Deactivated -= OnWindowDeactivated;
        Stop();
        apply.IsEnabled = false;
        base.OnDisappearing();
    }
}
