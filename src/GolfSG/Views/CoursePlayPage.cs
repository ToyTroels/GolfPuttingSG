using GolfSG.Application.Courses;
using GolfSG.Application.ViewModels;
using GolfSG.Core.Models;
using GolfSG.Infrastructure.Courses;
using Microsoft.Maui.Layouts;

namespace GolfSG.Views;

public sealed class CoursePlayPage : ContentPage
{
    private readonly CoursePracticeService service;
    private readonly CourseCommunityWorkflow? community;
    private CoursePracticeSession session;
    private readonly CourseMapView map = new() { Editable = false, PictureHeight = 400 };
    private readonly CourseMapView overviewMap = new("CoursePractice.OverviewMap") { Editable = false, SelectSavedShots = true, PictureHeight = 420 };
    private readonly FlexLayout shotSelector = new()
    {
        Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap,
        JustifyContent = FlexJustify.Start, AlignItems = FlexAlignItems.Start,
        AlignContent = FlexAlignContent.Start
    };
    private readonly Button currentShotFilter = CoursePracticeViews.SecondaryButton("Aktuelt slag");
    private readonly Button allShotsFilter = CoursePracticeViews.SecondaryButton("Alle slag");
    private readonly CheckBox hideCompleted = new() { Color = GolfTheme.Colors.PrimaryGreen, MinimumHeightRequest = 44, MinimumWidthRequest = 44 };
    private bool showAllShots;
    private string? overviewShotId;
    private readonly Label progress = new() { TextColor = GolfTheme.Colors.MutedText, FontSize = 13 };
    private readonly Label instruction = new() { TextColor = GolfTheme.Colors.MutedText };
    private readonly Label attemptProgress = AppViews.PageTitle("", 20);
    private readonly Entry distance = new() { Keyboard = Keyboard.Numeric, Placeholder = "Afstand til målet efter slaget" };
    private readonly Picker lie = new() { Title = "Slut-lie" };
    private readonly Stepper penalties = new() { Minimum = 0, Maximum = 10, Increment = 1 };
    private readonly Label penaltyLabel = new() { TextColor = GolfTheme.Colors.Text };
    private readonly CheckBox holed = new();
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText };
    private readonly VerticalStackLayout input = new() { Spacing = 12 };
    private readonly VerticalStackLayout play = new() { Spacing = 12 };
    private readonly VerticalStackLayout overview = new() { Spacing = 6 };
    private readonly VerticalStackLayout shotList = new() { Spacing = 12 };
    private readonly Label overviewInstruction = new() { TextColor = GolfTheme.Colors.MutedText, FontSize = 13 };
    private readonly VerticalStackLayout layout = new() { Spacing = 10, Padding = new Thickness(16, 8, 16, 16) };
    private readonly VerticalStackLayout footer = new()
    {
        Spacing = 8, Padding = new Thickness(16, 8, 16, 16), BackgroundColor = GolfTheme.Colors.PageBackground
    };
    private readonly ScrollView roundScroll = new();
    private readonly Button showOverview = CoursePracticeViews.SecondaryButton("Slagoversigt");
    private readonly Button record = CoursePracticeViews.PrimaryButton("Gem og næste slag");
    private readonly Button finish = CoursePracticeViews.PrimaryButton("Gem rundens resultat");
    private readonly Button undo = CompactActionButton("Fortryd seneste");
    private readonly Button finishEarly = CompactActionButton("Afslut og gem");
    private Task pendingInput = Task.CompletedTask;
    private bool refreshing, busy, showingOverview = true;
    private static readonly ShotLie[] Lies = [ShotLie.Green, ShotLie.Fairway, ShotLie.FairwayCut, ShotLie.Rough, ShotLie.Sand, ShotLie.Recovery, ShotLie.Tee];

    public CoursePlayPage(CoursePracticeService practiceService, CoursePracticeSession initialSession, CourseCommunityWorkflow? community = null)
    {
        service = practiceService; session = initialSession;
        this.community = community;
        CoursePracticeViews.ConfigurePage(this);
        holed.Color = GolfTheme.Colors.PrimaryGreen;
        Title = session.Course.Name; BackgroundColor = GolfTheme.Colors.PageBackground;
        SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.All;
        HideSoftInputOnTapped = true;
        this.Accessible("CoursePractice.Play", "Spil træningsrunde, beta");
        NavigationPage.SetHasBackButton(this, false);
        ToolbarItems.Add(new ToolbarItem { Text = "Gem og luk", Command = new Command(async () => await RunAsync(async () => { await FlushInputAsync(); await Navigation.PopAsync(); })) });
        lie.ItemsSource = Lies.Select(ShotLieLabels.Format).ToArray();
        layout.Children.Add(progress);
        showOverview.Accessible("CoursePractice.ShowOverview", "Se alle slag og vælg et slag at spille");
        showOverview.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync(); showingOverview = true; Refresh(); await ScrollToTopAsync();
        });
        layout.Children.Add(showOverview);
        overview.Accessible("CoursePractice.ShotOverview", "Oversigt over banens planlagte og spillede slag");
        SemanticProperties.SetDescription(overviewMap, "Banebillede. Tryk på et synligt slag for at vælge det. Grå slag er færdige.");
        overviewMap.ShotSelected += shot =>
        {
            if (busy || shot is null) return;
            overviewShotId = shot.Id; RefreshOverview(session.PlayedShots);
        };
        currentShotFilter.Accessible("CoursePractice.Overview.ShowCurrent", "Vis kun det aktuelle slag");
        allShotsFilter.Accessible("CoursePractice.Overview.ShowAll", "Vis alle slag");
        foreach (var button in new[] { currentShotFilter, allShotsFilter })
        {
            button.FontSize = 14; button.MinimumHeightRequest = 44;
            button.Padding = new Thickness(8, 6); button.BorderWidth = 1;
        }
        currentShotFilter.Clicked += (_, _) => SetOverviewFilter(false);
        allShotsFilter.Clicked += (_, _) => SetOverviewFilter(true);
        hideCompleted.Accessible("CoursePractice.Overview.HideCompleted", "Skjul positioner, hvor alle slag er spillet");
        hideCompleted.CheckedChanged += (_, _) => { if (!busy) RefreshOverview(session.PlayedShots); };
        var hideCompletedLabel = new Label { Text = "Skjul færdige slag", FontSize = 14, VerticalOptions = LayoutOptions.Center };
        hideCompletedLabel.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => { if (!busy) hideCompleted.IsChecked = !hideCompleted.IsChecked; })
        });
        overview.Children.Add(new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 6,
            Children = { currentShotFilter.Column(0), allShotsFilter.Column(1) }
        });
        overview.Children.Add(new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 6,
            Children = { hideCompleted.Column(0), hideCompletedLabel.Column(1) }
        });
        overview.Children.Add(overviewMap); overview.Children.Add(overviewInstruction);
        overview.Children.Add(new Label { Text = "Grøn: start · Rød: mål · Grå: færdig", TextColor = GolfTheme.Colors.MutedText, FontSize = 12 });
        overview.Children.Add(shotSelector);
        overview.Children.Add(shotList);
        layout.Children.Add(overview);
        play.Children.Add(map); play.Children.Add(attemptProgress); play.Children.Add(instruction);
        input.Children.Add(CoursePracticeViews.Field("Afstand til målet efter slaget (m)", distance));
        input.Children.Add(CoursePracticeViews.Field("Slut-lie", lie));
        input.Children.Add(new HorizontalStackLayout { Children = { holed, new Label { Text = "I hul", VerticalOptions = LayoutOptions.Center, TextColor = GolfTheme.Colors.Text } } });
        input.Children.Add(penaltyLabel); input.Children.Add(penalties);
        play.Children.Add(input);
        layout.Children.Add(play);
        undo.Accessible("CoursePractice.UndoResult", "Fortryd det senest registrerede resultat");
        var close = CompactActionButton("Gem og luk").Accessible("CoursePractice.SaveAndClose", "Gem den igangværende runde og luk den");
        close.Clicked += async (_, _) => await RunAsync(async () => { await FlushInputAsync(); await Navigation.PopAsync(); });
        layout.Children.Add(new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8,
            Children = { undo.Column(0), close.Column(1) }
        });
        finishEarly.Accessible("CoursePractice.FinishEarly", "Afslut runden og gem de registrerede slag");
        finishEarly.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync();
            var active = (await service.LoadAsync()).Active;
            if (active is null || active.Id != session.Id)
                throw new InvalidOperationException("Runden er ikke længere aktiv.");
            if (!active.IsComplete && !await DisplayAlertAsync("Afslut runden?",
                $"Gem de {active.Results.Count} registrerede slag og spring resten over. En ikke registreret indtastning gemmes ikke som et resultat.",
                "Afslut og gem", "Annuller")) return;
            var round = await service.FinishEarlyAsync(session.Id);
            await Navigation.PushAsync(new CoursePracticeResultPage(round, community));
            Navigation.RemovePage(this);
        });
        layout.Children.Add(finishEarly);
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
            if (session.CurrentShot?.Id != recordedShotId) overviewShotId = session.CurrentShot?.Id;
            showingOverview = session.IsComplete; Refresh();
            // Stay at the form while entering the remaining results from this position.
            if (session.CurrentShot?.Id != recordedShotId) await ScrollToTopAsync();
        });
        finish.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync();
            var round = await service.FinishAsync(session.Id);
            await Navigation.PushAsync(new CoursePracticeResultPage(round, community));
            Navigation.RemovePage(this);
        });
        undo.Clicked += async (_, _) => await RunAsync(async () =>
        {
            await FlushInputAsync();
            if (HasDraft() && !await DisplayAlertAsync("Fortryd seneste resultat?", "Den aktuelle, ikke registrerede indtastning ryddes, når det seneste spillede slag åbnes igen.", "Fortryd", "Annuller")) return;
            session = await service.UndoAsync(session.Id);
            overviewShotId = session.CurrentShot?.Id;
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
        try { if (session.Course.ImageFile == CoursePracticeImages.SampleFile) await CoursePracticeImages.EnsureSampleAsync(); var path = CoursePracticeImages.PathFor(session.Course.ImageFile); map.SetImage(path); overviewMap.SetImage(path); }
        catch { error.Text = "Banebilledet kunne ikke åbnes. Slagets navn, lie og afstand er stadig tilgængelige."; }
    }
    private void Refresh()
    {
        refreshing = true;
        var total = session.Results.Sum(r => r.StrokesGained);
        Title = session.Course.Name;
        progress.Text = $"{session.Results.Count}/{session.TotalAttempts} slag · SG {total:+0.00;-0.00;0.00}";
        SemanticProperties.SetDescription(progress, $"{session.Results.Count} af {session.TotalAttempts} slag registreret. SG {total:+0.00;-0.00;0.00}");
        map.ImageAspectRatio = session.Course.ImageAspectRatio;
        var shot = session.CurrentShot;
        map.From = shot?.From; map.Target = shot?.Target;
        map.SetDistanceSummary(shot?.DistanceMeters(session.Course), shot?.MeasuredDistanceMeters is not null);
        var shotNumber = shot is null ? 0 : session.Course.Shots.ToList().FindIndex(s => s.Id == shot.Id) + 1;
        map.CurrentShotNumber = shotNumber;
        map.Refresh();
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
        RefreshOverview(session.PlayedShots);
        refreshing = false;
    }
    private void RefreshActions()
    {
        var lastShot = session.Results.Count == session.TotalAttempts - 1;
        record.Text = lastShot ? "Gem sidste slag" : "Gem og næste slag";
        SemanticProperties.SetDescription(record, lastShot ? "Gem resultatet for det sidste slag" : "Gem resultatet og gå til næste planlagte slag");
        record.IsVisible = !showingOverview && !session.IsComplete;
        finish.IsVisible = session.IsComplete;
        finishEarly.IsVisible = session.Game is null && !session.IsComplete;
        finishEarly.IsEnabled = session.Results.Count > 0;
        error.IsVisible = !string.IsNullOrWhiteSpace(error.Text);
        footer.IsVisible = record.IsVisible || finish.IsVisible || error.IsVisible;
    }
    private void RefreshOverview(IReadOnlyList<CoursePracticeShot> playedShots)
    {
        foreach (var (button, selected, description) in new[]
        {
            (currentShotFilter, !showAllShots, "Vis kun det aktuelle slag"),
            (allShotsFilter, showAllShots, "Vis alle slag")
        })
        {
            button.BackgroundColor = selected ? GolfTheme.Colors.PrimaryGreen : GolfTheme.Colors.CardBackground;
            button.TextColor = selected ? Colors.White : GolfTheme.Colors.PrimaryGreen;
            SemanticProperties.SetDescription(button, selected ? $"{description}, valgt" : description);
        }
        overviewInstruction.Text = session.IsComplete
            ? "Alle slag er spillet. Gem resultatet, eller fortryd for at rette."
            : "Vælg et slag på billedet eller et nummer nedenfor.";
        var recorded = session.Results.Select((result, index) => (Id: playedShots[index].Id, Result: result))
            .GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.Select(item => item.Result).ToArray());
        var completedIds = session.Course.Shots.Where(s => recorded.TryGetValue(s.Id, out var r) && r.Length == s.Attempts).Select(s => s.Id).ToHashSet();
        var availableShots = session.Course.Shots.Where(shot => !hideCompleted.IsChecked || !completedIds.Contains(shot.Id)).ToArray();
        if (!availableShots.Any(shot => shot.Id == overviewShotId))
            overviewShotId = availableShots.FirstOrDefault(shot => shot.Id == session.CurrentShot?.Id)?.Id ?? availableShots.FirstOrDefault()?.Id;
        overviewMap.ImageAspectRatio = session.Course.ImageAspectRatio;
        overviewMap.SavedShots = session.Course.Shots;
        overviewMap.VisibleSavedShotIds = availableShots.Where(shot => showAllShots || shot.Id == overviewShotId).Select(shot => shot.Id).ToHashSet();
        overviewMap.SelectedShotId = overviewShotId;
        overviewMap.CompletedShotIds = completedIds;
        overviewMap.Refresh();
        shotList.Children.Clear(); shotSelector.Children.Clear();
        if (availableShots.Length == 0)
            shotList.Children.Add(new Label { Text = "Alle slag er færdige. Slå Skjul færdige slag fra for at se dem.", FontSize = 14, TextColor = GolfTheme.Colors.MutedText });
        foreach (var (shot, index) in session.Course.Shots.Select((shot, index) => (shot, index)))
        {
            recorded.TryGetValue(shot.Id, out var shotResults);
            var completedCount = shotResults?.Length ?? 0;
            var completed = completedCount == shot.Attempts;
            if (hideCompleted.IsChecked && completed) continue;
            var shotSg = shotResults?.Sum(result => result.StrokesGained) ?? 0;
            var current = session.CurrentShot?.Id == shot.Id;
            var selector = CoursePracticeViews.SecondaryButton($"{index + 1}{(completed ? " ✓" : "")}");
            selector.MinimumWidthRequest = 48;
            selector.MinimumHeightRequest = 48;
            selector.FontSize = 14;
            selector.Padding = new Thickness(8, 6);
            selector.Margin = new Thickness(0, 0, 6, 6);
            FlexLayout.SetShrink(selector, 0);
            selector.BorderWidth = overviewShotId == shot.Id ? 3 : 1;
            selector.Accessible($"CoursePractice.PreviewShot-{shot.Id}", $"Vælg slag {index + 1}: {shot.Name}{(completed ? ", færdig" : "")}");
            selector.Clicked += (_, _) => { overviewShotId = shot.Id; RefreshOverview(session.PlayedShots); };
            shotSelector.Children.Add(selector);
            if (shot.Id != overviewShotId) continue;
            var category = shot.Category switch { PracticeShotCategory.Putting => "Putting", PracticeShotCategory.Approach => "Approach", _ => "Omkring green" };
            var card = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    AppViews.PageTitle($"{index + 1}. {shot.Name}", 18),
                    new Label { Text = $"{CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(session.Course))} · {ShotLieLabels.Format(shot.Lie)} · {category}", TextColor = GolfTheme.Colors.Text, FontSize = 14 },
                    new Label { Text = $"{completedCount}/{shot.Attempts} slag spillet{(completedCount > 0 ? $" · SG {shotSg:+0.00;-0.00;0.00}" : "")}{(completed ? " · Færdig" : current ? " · Aktuel position" : "")}", TextColor = completed ? GolfTheme.Colors.PrimaryGreen : GolfTheme.Colors.MutedText, FontSize = 13 }
                }
            };
            card.Children.Add(AttemptsControl(shot, completedCount));
            if (!completed)
            {
                var choose = CompactActionButton(completedCount > 0 || current && HasDraft() ? "Fortsæt slag" : "Spil slag", primary: true)
                    .Accessible($"CoursePractice.ChooseShot-{shot.Id}", $"Spil slag {index + 1}: {shot.Name}");
                choose.Clicked += async (_, _) => await RunAsync(() => SelectShotAsync(shot.Id));
                card.Children.Add(choose);
            }
            shotList.Children.Add(AppViews.Card(card, padding: GolfTheme.Spacing.CompactCardPadding));
        }
    }
    private void SetOverviewFilter(bool showAll)
    {
        if (busy) return;
        showAllShots = showAll;
        RefreshOverview(session.PlayedShots);
    }
    private View AttemptsControl(CoursePracticeShot shot, int completedCount)
    {
        var fewer = CompactActionButton("−")
            .Accessible($"CoursePractice.DecreaseAttempts-{shot.Id}", $"Færre slag fra {shot.Name}");
        var more = CompactActionButton("+")
            .Accessible($"CoursePractice.IncreaseAttempts-{shot.Id}", $"Flere slag fra {shot.Name}");
        foreach (var button in new[] { fewer, more }) { button.WidthRequest = 44; button.Padding = 0; }
        fewer.IsEnabled = session.Game is null && completedCount == 0 && shot.Attempts > 1;
        more.IsEnabled = session.Game is null && completedCount == 0 && shot.Attempts < 100 && session.TotalAttempts < 1000;
        fewer.Clicked += async (_, _) => await RunAsync(() => ChangeAttemptsAsync(shot, shot.Attempts - 1));
        more.Clicked += async (_, _) => await RunAsync(() => ChangeAttemptsAsync(shot, shot.Attempts + 1));
        var value = new Label
        {
            Text = shot.Attempts.ToString(), FontSize = 18, FontAttributes = FontAttributes.Bold, MinimumWidthRequest = 32,
            TextColor = GolfTheme.Colors.Text, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
        }.Accessible($"CoursePractice.AttemptCount-{shot.Id}", $"Antal slag fra {shot.Name}: {shot.Attempts}");
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) },
                    ColumnSpacing = 6,
                    Children =
                    {
                        new Label { Text = "Antal slag", FontSize = 14, VerticalOptions = LayoutOptions.Center }.Column(0),
                        fewer.Column(1), value.Column(2), more.Column(3)
                    }
                },
                new Label
                {
                    Text = session.Game is not null ? "Samme antal for alle spillere." : completedCount == 0 ? "Kun denne runde." : "Låst efter første resultat.",
                    TextColor = GolfTheme.Colors.MutedText, FontSize = 13
                }
            }
        };
    }
    private static Button CompactActionButton(string text, bool primary = false)
    {
        var button = primary ? CoursePracticeViews.PrimaryButton(text) : CoursePracticeViews.SecondaryButton(text);
        button.FontSize = 14;
        button.MinimumHeightRequest = 44;
        button.Padding = new Thickness(8, 6);
        button.BorderWidth = primary ? 0 : 1;
        return button;
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
