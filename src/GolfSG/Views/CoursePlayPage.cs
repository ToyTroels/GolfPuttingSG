using GolfSG.Application.Courses;
using GolfSG.Application.ViewModels;
using GolfSG.Core.Models;

namespace GolfSG.Views;

public sealed class CoursePlayPage : ContentPage
{
    private readonly CoursePracticeService service;
    private CoursePracticeSession session;
    private readonly CourseMapView map = new() { Editable = false, PictureHeight = 400 };
    private readonly CourseMapView overviewMap = new("CoursePractice.OverviewMap") { Editable = false, SelectSavedShots = true, PictureHeight = 420 };
    private readonly HorizontalStackLayout shotSelector = new() { Spacing = 8 };
    private string? overviewShotId;
    private readonly Label heading = AppViews.PageTitle("");
    private readonly Label progress = new() { TextColor = GolfTheme.Colors.Text };
    private readonly Label instruction = new() { TextColor = GolfTheme.Colors.MutedText };
    private readonly Label attemptProgress = AppViews.PageTitle("", 20);
    private readonly Entry distance = new() { Keyboard = Keyboard.Numeric, Placeholder = "Afstand til målet efter slaget" };
    private readonly Picker lie = new() { Title = "Slut-lie" };
    private readonly Stepper penalties = new() { Minimum = 0, Maximum = 10, Increment = 1 };
    private readonly Label penaltyLabel = new() { TextColor = GolfTheme.Colors.Text };
    private readonly CheckBox holed = new();
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText };
    private readonly VerticalStackLayout results = new() { Spacing = 8 };
    private readonly VerticalStackLayout input = new() { Spacing = 12 };
    private readonly VerticalStackLayout play = new() { Spacing = 12 };
    private readonly VerticalStackLayout overview = new() { Spacing = 12 };
    private readonly VerticalStackLayout shotList = new() { Spacing = 12 };
    private readonly Label overviewInstruction = new() { TextColor = GolfTheme.Colors.MutedText };
    private readonly VerticalStackLayout layout = new() { Spacing = 12, Padding = 16 };
    private readonly VerticalStackLayout footer = new()
    {
        Spacing = 8, Padding = new Thickness(16, 8, 16, 16), BackgroundColor = GolfTheme.Colors.PageBackground
    };
    private readonly ScrollView roundScroll = new();
    private readonly Button showOverview = CoursePracticeViews.SecondaryButton("Slagoversigt");
    private readonly Button record = CoursePracticeViews.PrimaryButton("Gem og næste slag");
    private readonly Button finish = CoursePracticeViews.PrimaryButton("Gem rundens resultat");
    private readonly Button undo = CoursePracticeViews.SecondaryButton("Fortryd seneste resultat");
    private Task pendingInput = Task.CompletedTask;
    private bool refreshing, busy, showingOverview = true;
    private static readonly ShotLie[] Lies = [ShotLie.Green, ShotLie.Fairway, ShotLie.FairwayCut, ShotLie.Rough, ShotLie.Sand, ShotLie.Recovery, ShotLie.Tee];

    public CoursePlayPage(CoursePracticeService practiceService, CoursePracticeSession initialSession)
    {
        service = practiceService; session = initialSession;
        CoursePracticeViews.ConfigurePage(this);
        holed.Color = GolfTheme.Colors.PrimaryGreen;
        Title = "Spil banerunde · Beta"; BackgroundColor = GolfTheme.Colors.PageBackground;
        SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.All;
        HideSoftInputOnTapped = true;
        this.Accessible("CoursePractice.Play", "Spil træningsrunde, beta");
        NavigationPage.SetHasBackButton(this, false);
        ToolbarItems.Add(new ToolbarItem { Text = "Gem og luk", Command = new Command(async () => await RunAsync(async () => { await FlushInputAsync(); await Navigation.PopAsync(); })) });
        lie.ItemsSource = Lies.Select(ShotLieLabels.Format).ToArray();
        layout.Children.Add(heading); layout.Children.Add(progress);
        showOverview.Accessible("CoursePractice.ShowOverview", "Se alle slag og vælg et slag at spille");
        showOverview.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync(); showingOverview = true; Refresh(); await ScrollToTopAsync();
        });
        layout.Children.Add(showOverview);
        overview.Accessible("CoursePractice.ShotOverview", "Oversigt over banens planlagte og spillede slag");
        overview.Children.Add(AppViews.PageTitle("Slagoversigt", 20));
        SemanticProperties.SetDescription(overviewMap, "Banebillede med alle slag. Tryk på et nummer eller en linje for at vælge et slag. Grå slag er færdige.");
        overviewMap.ShotSelected += shot =>
        {
            if (busy || shot is null) return;
            overviewShotId = shot.Id; RefreshOverview(session.PlayedShots);
        };
        overview.Children.Add(overviewInstruction); overview.Children.Add(overviewMap);
        overview.Children.Add(new Label { Text = "Grøn: start · Rød: mål · Grå: færdig", TextColor = GolfTheme.Colors.MutedText, FontSize = 12 });
        overview.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = shotSelector });
        overview.Children.Add(shotList);
        layout.Children.Add(overview);
        play.Children.Add(map); play.Children.Add(attemptProgress); play.Children.Add(instruction);
        input.Children.Add(CoursePracticeViews.Field("Afstand til målet efter slaget (m)", distance));
        input.Children.Add(CoursePracticeViews.Field("Slut-lie", lie));
        input.Children.Add(new HorizontalStackLayout { Children = { holed, new Label { Text = "I hul", VerticalOptions = LayoutOptions.Center, TextColor = GolfTheme.Colors.Text } } });
        input.Children.Add(penaltyLabel); input.Children.Add(penalties);
        play.Children.Add(input);
        layout.Children.Add(play); layout.Children.Add(undo);
        var close = CoursePracticeViews.SecondaryButton("Gem og luk");
        close.Clicked += async (_, _) => await RunAsync(async () => { await FlushInputAsync(); await Navigation.PopAsync(); });
        layout.Children.Add(close); layout.Children.Add(AppViews.PageTitle("Seneste registrerede slag", 20)); layout.Children.Add(results);
        record.Accessible("CoursePractice.SaveResultAndNext", "Gem resultatet og gå til næste planlagte slag");
        finish.Accessible("CoursePractice.FinishRound", "Gem den færdige rundes resultat");
        footer.Children.Add(error); footer.Children.Add(record); footer.Children.Add(finish);
        error.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Label.Text)) RefreshActions(); };
        roundScroll.Content = layout;
        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { roundScroll.Row(0), footer.Row(1) }
        };
        distance.TextChanged += (_, _) => QueueInput(); lie.SelectedIndexChanged += (_, _) => QueueInput();
        penalties.ValueChanged += (_, _) => { penaltyLabel.Text = $"Strafslag: {penalties.Value:0}"; QueueInput(); };
        holed.CheckedChanged += (_, _) => { distance.IsEnabled = lie.IsEnabled = !holed.IsChecked; QueueInput(); };
        record.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync();
            // A resumed page can lag behind the persisted round (for example after
            // a save completed but navigation or rendering failed). Never submit
            // its result against a different attempt or position.
            var active = (await service.LoadAsync()).Active;
            if (active is null || active.Id != session.Id)
                throw new InvalidOperationException("Runden er ikke længere aktiv.");
            if (active.Results.Count != session.Results.Count || active.CurrentShot?.Id != session.CurrentShot?.Id)
            {
                session = active;
                showingOverview = active.IsComplete;
                Refresh(); await ScrollToTopAsync();
                error.Text = "Runden er opdateret. Kontrollér det viste slag, før du gemmer.";
                return;
            }
            if (!holed.IsChecked && !CourseSetupPage.TryDistance(distance.Text, out _)) throw new ArgumentException("Angiv slutafstanden i meter, eller vælg I hul.");
            if (!holed.IsChecked && lie.SelectedIndex < 0) throw new ArgumentException("Vælg slut-lie.");
            double endDistance = 0;
            if (!holed.IsChecked) CourseSetupPage.TryDistance(distance.Text, out endDistance);
            var recordedShotId = session.CurrentShot?.Id;
            session = await service.RecordAsync(session.Id, session.Results.Count, new(endDistance,
                holed.IsChecked ? ShotLie.Holed : Lies[lie.SelectedIndex], (int)penalties.Value, holed.IsChecked), recordedShotId);
            showingOverview = session.IsComplete; Refresh();
            // Stay at the form while entering the remaining results from this position.
            if (session.CurrentShot?.Id != recordedShotId) await ScrollToTopAsync();
        });
        finish.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync();
            var round = await service.FinishAsync(session.Id);
            await Navigation.PushAsync(new CoursePracticeResultPage(round));
            Navigation.RemovePage(this);
        });
        undo.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync();
            if (HasDraft() && !await DisplayAlertAsync("Fortryd seneste resultat?", "Den aktuelle, ikke registrerede indtastning ryddes, når det seneste spillede slag åbnes igen.", "Fortryd", "Annuller")) return;
            session = await service.UndoAsync(session.Id);
            showingOverview = false; Refresh(); await ScrollToTopAsync();
        });
        Refresh();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAsync(async () =>
        {
            var active = (await service.LoadAsync()).Active;
            if (active is null || active.Id != session.Id)
                throw new InvalidOperationException("Runden er ikke længere aktiv.");
            session = active; Refresh();
        });
        try { await CoursePracticeImages.EnsureSampleAsync(); var path = CoursePracticeImages.PathFor(session.Course.ImageFile); map.SetImage(path); overviewMap.SetImage(path); }
        catch { error.Text = "Banebilledet kunne ikke åbnes. Slagets navn, lie og afstand er stadig tilgængelige."; }
    }
    private void Refresh()
    {
        refreshing = true;
        var total = session.Results.Sum(r => r.StrokesGained);
        heading.Text = session.Course.Name;
        progress.Text = $"{session.Results.Count}/{session.TotalAttempts} slag registreret · SG {total:+0.00;-0.00;0.00}";
        map.ImageAspectRatio = session.Course.ImageAspectRatio;
        var shot = session.CurrentShot;
        map.From = shot?.From; map.Target = shot?.Target; map.Refresh();
        map.SetDistanceSummary(shot?.DistanceMeters(session.Course), shot?.MeasuredDistanceMeters is not null);
        var shotNumber = shot is null ? 0 : session.Course.Shots.ToList().FindIndex(s => s.Id == shot.Id) + 1;
        attemptProgress.Text = shot is null ? "" : $"Slag {session.CurrentAttempt} af {shot.Attempts}";
        attemptProgress.IsVisible = shot is not null;
        instruction.Text = shot is null ? "Alle slag er registreret. Gem resultatet, eller fortryd det seneste for at rette en fejl." :
            $"Position {shotNumber}: {shot.Name} · {ShotLieLabels.Format(shot.Lie)} · {CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(session.Course))}\nSpil slagene fra den viste startposition, og registrer resultaterne ét ad gangen. Gem hvert resultat med knappen nedenfor, eller vælg et andet slag via Slagoversigt.";
        overview.IsVisible = showingOverview; play.IsVisible = !showingOverview;
        showOverview.IsVisible = !showingOverview;
        input.IsVisible = !session.IsComplete; undo.IsEnabled = session.Results.Count > 0;
        RefreshActions();
        distance.Text = session.Input.Distance; lie.SelectedIndex = Array.IndexOf(Lies, session.Input.Lie);
        penalties.Value = session.Input.Penalties; penaltyLabel.Text = $"Strafslag: {penalties.Value:0}";
        holed.IsChecked = session.Input.Holed; distance.IsEnabled = lie.IsEnabled = !holed.IsChecked;
        var playedShots = session.PlayedShots;
        RefreshOverview(playedShots);
        results.Children.Clear();
        var attemptNumbers = new Dictionary<string, int>();
        var firstVisibleResult = Math.Max(0, session.Results.Count - 10);
        if (firstVisibleResult > 0)
            results.Children.Add(new Label { Text = "De seneste 10 slag vises her. Alle resultater gemmes i rundens resultat.", TextColor = GolfTheme.Colors.MutedText });
        foreach (var (r, index) in session.Results.Select((r, i) => (r, i)))
        {
            var playedShot = playedShots[index];
            var attemptNumber = attemptNumbers.GetValueOrDefault(playedShot.Id) + 1; attemptNumbers[playedShot.Id] = attemptNumber;
            if (index < firstVisibleResult) continue;
            var outcome = r.Outcome.Holed ? "I hul" : $"{r.Outcome.EndDistanceMeters:0.0} m · {ShotLieLabels.Format(r.Outcome.EndLie)}";
            results.Children.Add(new Label { Text = $"{index + 1}. {playedShot.Name} · slag {attemptNumber} af {playedShot.Attempts}: {outcome} · Straf {r.Outcome.PenaltyStrokes} · SG {r.StrokesGained:+0.00;-0.00;0.00}", TextColor = GolfTheme.Colors.Text });
        }
        refreshing = false;
    }
    private void RefreshActions()
    {
        var lastShot = session.Results.Count == session.TotalAttempts - 1;
        record.Text = lastShot ? "Gem sidste slag" : "Gem og næste slag";
        SemanticProperties.SetDescription(record, lastShot ? "Gem resultatet for det sidste slag" : "Gem resultatet og gå til næste planlagte slag");
        record.IsVisible = !showingOverview && !session.IsComplete;
        finish.IsVisible = session.IsComplete;
        error.IsVisible = !string.IsNullOrWhiteSpace(error.Text);
        footer.IsVisible = record.IsVisible || finish.IsVisible || error.IsVisible;
    }
    private void RefreshOverview(IReadOnlyList<CoursePracticeShot> playedShots)
    {
        overviewInstruction.Text = session.IsComplete
            ? "Alle slag er spillet. Gem rundens resultat, eller fortryd det seneste resultat for at rette det."
            : "Tryk på et slag på billedet eller vælg et nummer. Du bestemmer rækkefølgen.";
        var recorded = session.Results.Select((result, index) => (Id: playedShots[index].Id, Result: result))
            .GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.Select(item => item.Result).ToArray());
        overviewShotId ??= session.CurrentShot?.Id ?? session.Course.Shots.FirstOrDefault()?.Id;
        overviewMap.ImageAspectRatio = session.Course.ImageAspectRatio;
        overviewMap.SavedShots = session.Course.Shots;
        overviewMap.SelectedShotId = overviewShotId;
        overviewMap.CompletedShotIds = session.Course.Shots.Where(s => recorded.TryGetValue(s.Id, out var r) && r.Length == s.Attempts).Select(s => s.Id).ToHashSet();
        overviewMap.Refresh();
        shotList.Children.Clear(); shotSelector.Children.Clear();
        foreach (var (shot, index) in session.Course.Shots.Select((shot, index) => (shot, index)))
        {
            recorded.TryGetValue(shot.Id, out var shotResults);
            var completedCount = shotResults?.Length ?? 0;
            var completed = completedCount == shot.Attempts;
            var shotSg = shotResults?.Sum(result => result.StrokesGained) ?? 0;
            var current = session.CurrentShot?.Id == shot.Id;
            var selector = CoursePracticeViews.SecondaryButton($"{index + 1}{(completed ? " ✓" : "")}");
            selector.MinimumWidthRequest = 48;
            selector.BorderWidth = overviewShotId == shot.Id ? 3 : 1;
            selector.Accessible($"CoursePractice.PreviewShot-{shot.Id}", $"Vælg slag {index + 1}: {shot.Name}{(completed ? ", færdig" : "")}");
            selector.Clicked += (_, _) => { overviewShotId = shot.Id; RefreshOverview(session.PlayedShots); };
            shotSelector.Children.Add(selector);
            if (shot.Id != overviewShotId) continue;
            var category = shot.Category switch { PracticeShotCategory.Putting => "Putting", PracticeShotCategory.Approach => "Approach", _ => "Omkring green" };
            var card = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    AppViews.PageTitle($"{index + 1}. {shot.Name}", 20),
                    new Label { Text = $"{CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(session.Course))} · {ShotLieLabels.Format(shot.Lie)} · {category}", TextColor = GolfTheme.Colors.Text },
                    new Label { Text = $"{completedCount}/{shot.Attempts} slag spillet{(completedCount > 0 ? $" · SG {shotSg:+0.00;-0.00;0.00}" : "")}{(completed ? " · Færdig" : current ? " · Aktuel position" : "")}", TextColor = completed ? GolfTheme.Colors.PrimaryGreen : GolfTheme.Colors.MutedText }
                }
            };
            card.Children.Add(AttemptsControl(shot, completedCount));
            if (!completed)
            {
                var choose = CoursePracticeViews.PrimaryButton(completedCount > 0 || current && HasDraft() ? "Fortsæt slag" : "Spil slag")
                    .Accessible($"CoursePractice.ChooseShot-{shot.Id}", $"Spil slag {index + 1}: {shot.Name}");
                choose.Clicked += async (_, _) => await RunAsync(() => SelectShotAsync(shot.Id));
                card.Children.Add(choose);
            }
            shotList.Children.Add(AppViews.Card(card));
        }
    }
    private View AttemptsControl(CoursePracticeShot shot, int completedCount)
    {
        var fewer = CoursePracticeViews.SecondaryButton("−")
            .Accessible($"CoursePractice.DecreaseAttempts-{shot.Id}", $"Færre slag fra {shot.Name}");
        var more = CoursePracticeViews.SecondaryButton("+")
            .Accessible($"CoursePractice.IncreaseAttempts-{shot.Id}", $"Flere slag fra {shot.Name}");
        foreach (var button in new[] { fewer, more }) { button.WidthRequest = 56; button.Padding = 0; }
        fewer.IsEnabled = completedCount == 0 && shot.Attempts > 1;
        more.IsEnabled = completedCount == 0 && shot.Attempts < 100 && session.TotalAttempts < 1000;
        fewer.Clicked += async (_, _) => await RunAsync(() => ChangeAttemptsAsync(shot, shot.Attempts - 1));
        more.Clicked += async (_, _) => await RunAsync(() => ChangeAttemptsAsync(shot, shot.Attempts + 1));
        var value = new Label
        {
            Text = shot.Attempts.ToString(), FontSize = 24, FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.Text, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
        }.Accessible($"CoursePractice.AttemptCount-{shot.Id}", $"Antal slag fra {shot.Name}: {shot.Attempts}");
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                AppViews.FieldLabel("Antal slag"),
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    ColumnSpacing = 12, Children = { fewer.Column(0), value.Column(1), more.Column(2) }
                },
                new Label
                {
                    Text = completedCount == 0 ? "Gælder kun denne runde." : "Antallet er låst, fordi du har registreret slag her.",
                    TextColor = GolfTheme.Colors.MutedText, FontSize = 14
                }
            }
        };
    }
    private async Task ChangeAttemptsAsync(CoursePracticeShot shot, int attempts)
    {
        await FlushInputAsync();
        session = await service.UpdateAttemptsAsync(session.Id, shot.Id, attempts, session.Results.Count, session.CurrentShot?.Id, shot.Attempts);
        Refresh();
    }
    private bool HasDraft() => !string.IsNullOrWhiteSpace(session.Input.Distance) || session.Input.Lie != ShotLie.Green || session.Input.Penalties > 0 || session.Input.Holed;
    private async Task SelectShotAsync(string shotId)
    {
        await FlushInputAsync();
        var current = session.CurrentShot ?? throw new InvalidOperationException("Alle slag er allerede spillet.");
        if (current.Id != shotId && HasDraft() &&
            !await DisplayAlertAsync("Skift slag?", $"Den ikke registrerede indtastning for {current.Name} ryddes, hvis du vælger et andet slag.", "Skift slag", "Annuller")) return;
        session = await service.SelectShotAsync(session.Id, shotId, session.Results.Count, current.Id);
        showingOverview = false; Refresh(); await ScrollToTopAsync();
    }
    private async Task ScrollToTopAsync()
    {
        try
        {
            if (distance.IsSoftInputShowing()) await distance.HideSoftInputAsync(CancellationToken.None);
            distance.Unfocus(); lie.Unfocus();
            await Task.Yield(); await roundScroll.ScrollToAsync(0, 0, animated: true);
        }
        catch { error.Text = "Visningen er klar. Rul op for at se slaget eller oversigten."; }
    }
    private void QueueInput()
    {
        if (refreshing || busy || session.IsComplete) return;
        var snapshot = new CourseResultInput(distance.Text ?? "", lie.SelectedIndex < 0 ? ShotLie.Green : Lies[lie.SelectedIndex], (int)penalties.Value, holed.IsChecked);
        session = session with { Input = snapshot };
        pendingInput = SaveAfterAsync(pendingInput, snapshot, session.Results.Count, session.CurrentShot!.Id);
    }
    private async Task SaveAfterAsync(Task previous, CourseResultInput snapshot, int expectedIndex, string expectedShotId)
    {
        try { await previous; await service.SaveInputAsync(session.Id, snapshot, expectedIndex, expectedShotId); }
        catch { error.Text = "Indtastningen kunne ikke gemmes automatisk. Brug Gem og luk for at prøve igen."; }
    }
    private async Task FlushInputAsync() { await pendingInput; if (session.CurrentShot is { } shot) await service.SaveInputAsync(session.Id, session.Input, session.Results.Count, shot.Id); }
    protected override bool OnBackButtonPressed() { if (!busy) _ = RunAsync(async () => { await FlushInputAsync(); await Navigation.PopAsync(); }); return true; }
    protected override void OnDisappearing() { base.OnDisappearing(); if (!busy && session.CurrentShot is { } shot) pendingInput = SaveAfterAsync(pendingInput, session.Input, session.Results.Count, shot.Id); }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return; busy = true; layout.IsEnabled = footer.IsEnabled = false; error.Text = "";
        try { await action(); } catch (Exception ex) { error.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Kunne ikke gemme. Prøv igen; runden er bevaret."; }
        finally { busy = false; layout.IsEnabled = footer.IsEnabled = true; undo.IsEnabled = session.Results.Count > 0; }
    }
}
