using GolfSG.Application.Courses;
using GolfSG.Application.ViewModels;
using GolfSG.Core.Models;

namespace GolfSG.Views;

public sealed class CourseSetupPage : ContentPage
{
    private readonly CoursePracticeService service;
    private readonly CourseMapView map = new() { DragSavedShots = false, ShowSavedShots = false };
    private readonly Entry courseName = new() { Placeholder = "Banens navn", MaxLength = 100 };
    private readonly Entry shotName = new() { Placeholder = "Valgfrit navn", MaxLength = 100 };
    private readonly Label shotNameSuggestion = new() { Text = "Foreslået navn: Slag 1", TextColor = GolfTheme.Colors.MutedText, FontSize = 13 };
    private readonly Entry attempts = new() { Text = "1", Placeholder = "Fx 5", Keyboard = Keyboard.Numeric, MaxLength = 3 };
    private readonly Entry measured = new() { Placeholder = "Valgfri målt afstand (m)", Keyboard = Keyboard.Numeric };
    private ShotLie? startingLie;
    private readonly Dictionary<ShotLie, Button> lieButtons = [];
    private readonly View startLieSection;
    private readonly Picker category = new() { Title = "SG-kategori" };
    private readonly Label status = new() { TextColor = GolfTheme.Colors.Text, FontSize = 14 };
    private readonly Label scale = new() { TextColor = GolfTheme.Colors.MutedText, FontSize = 13 };
    private readonly Label error = new() { TextColor = GolfTheme.Colors.DangerText };
    private readonly VerticalStackLayout shotList = new() { Spacing = 6 };
    private readonly VerticalStackLayout layout = new() { Spacing = 12, Padding = 16 };
    private readonly ScrollView courseScroll = new();
    private readonly VerticalStackLayout footer = new()
    {
        Spacing = 6, Padding = new Thickness(16, 8, 16, 16), BackgroundColor = GolfTheme.Colors.PageBackground
    };
    private List<CoursePracticeShot> shots = [];
    private string courseId = Guid.NewGuid().ToString("N");
    private string imageFile = CoursePracticeImages.SampleFile;
    private double metresPerWidth = 941 * .1 * 110 / 136.8;
    private string scaleNote = "Omtrentlig målestok fra 110 m-reference. Brug en målt afstand for bedre præcision.";
    private bool calibrationMode, dirty, loading, busy, savedShotChangedDuringInteraction;
    private string? editingId;
    private CoursePoint? beforeFrom, beforeTarget;
    private string? beforeDragMeasured;
    private bool beforeDragDirty;
    private static readonly ShotLie[] Lies = [ShotLie.Tee, ShotLie.Fairway, ShotLie.Rough, ShotLie.Sand, ShotLie.FairwayCut, ShotLie.Green, ShotLie.Recovery];

    public CourseSetupPage(CoursePracticeService service)
    {
        this.service = service;
        CoursePracticeViews.ConfigurePage(this);
        Title = "Opsæt bane · Beta"; BackgroundColor = GolfTheme.Colors.PageBackground;
        SafeAreaEdges = Microsoft.Maui.SafeAreaEdges.All;
        HideSoftInputOnTapped = true;
        this.Accessible("CoursePractice.Setup", "Opsæt en bane på et billede, beta");
        NavigationPage.SetHasBackButton(this, false);
        ToolbarItems.Add(new ToolbarItem { Text = "Tilbage", Command = new Command(async () => { if (!busy) await LeaveAsync(); }) });
        category.ItemsSource = new[] { "Putting", "Approach", "Omkring green" }; category.SelectedIndex = 1;
        map.InteractionStarted += () =>
        {
            ReleaseInputFocus();
            savedShotChangedDuringInteraction = false;
            beforeDragMeasured = measured.Text; beforeDragDirty = dirty;
        };
        map.PointsChanged += (_, _) =>
        {
            if (map.From is not null) map.PlaceFrom = false;
            if (!calibrationMode && !map.MovingWholeShot) measured.Text = "";
            UpdateDistance(); dirty = true;
        };
        map.SavedShotChanged += moved =>
        {
            var index = shots.FindIndex(s => s.Id == moved.Id);
            if (index < 0 || shots[index] == moved) return;
            shots[index] = moved; dirty = true; savedShotChangedDuringInteraction = true; UpdateMapShots(); UpdateDistance();
        };
        map.ShotSelected += _ => UpdateDistance();
        map.InteractionFinished += canceled =>
        {
            if (canceled) { measured.Text = beforeDragMeasured; dirty = beforeDragDirty; }
            if (savedShotChangedDuringInteraction) RefreshShots();
            UpdateDistance();
        };
        var currentShotFilter = MapButton("Aktuelt slag")
            .Accessible("CoursePractice.ShowCurrentShot", "Vis kun det aktuelle slag, valgt");
        var allShotsFilter = MapButton("Alle slag")
            .Accessible("CoursePractice.ShowAllShots", "Vis alle slag");
        void ShowShots(bool showAll)
        {
            if (busy) return;
            map.ShowSavedShots = showAll;
            foreach (var (button, selected, description) in new[]
            {
                (currentShotFilter, !showAll, "Vis kun det aktuelle slag"),
                (allShotsFilter, showAll, "Vis alle slag")
            })
            {
                button.BackgroundColor = selected ? GolfTheme.Colors.PrimaryGreen : GolfTheme.Colors.CardBackground;
                button.TextColor = selected ? Colors.White : GolfTheme.Colors.PrimaryGreen;
                SemanticProperties.SetDescription(button, selected ? $"{description}, valgt" : description);
            }
            UpdateDistance();
        }
        currentShotFilter.Clicked += (_, _) => ShowShots(false);
        allShotsFilter.Clicked += (_, _) => ShowShots(true);
        ShowShots(false);
        var from = MapButton("Start", () => { map.DragSavedShots = false; map.PlaceFrom = true; status.Text = "Tryk på start (grøn), eller træk punktet."; });
        var target = MapButton("Mål", () => { map.DragSavedShots = false; map.PlaceFrom = false; status.Text = "Tryk på mål (orange), eller træk punktet."; });
        var mapHelp = new VerticalStackLayout
        {
            IsVisible = false, Spacing = 6,
            Children =
            {
                new Label { Text = "Grøn = start · Orange = mål. Stryg på billedet uden for punkter og linjer for at rulle. Træk et punkt for at ændre slaget, eller træk linjen for at flytte hele slaget. Gem bane for at beholde ændringerne.", TextColor = GolfTheme.Colors.MutedText, FontSize = 14 },
                scale,
                AsyncButton("Kalibrer målestok", CalibrateAsync)
            }
        };
        var help = MapButton("Hjælp og målestok ▾");
        help.Accessible("CoursePractice.MapHelp", "Vis hjælp og målestok");
        help.HorizontalOptions = LayoutOptions.Start;
        help.BackgroundColor = Colors.Transparent;
        help.BorderWidth = 0;
        help.Clicked += (_, _) =>
        {
            if (busy) return;
            mapHelp.IsVisible = !mapHelp.IsVisible;
            help.Text = mapHelp.IsVisible ? "Skjul hjælp og målestok ▴" : "Hjælp og målestok ▾";
            SemanticProperties.SetDescription(help, mapHelp.IsVisible ? "Skjul hjælp og målestok" : "Vis hjælp og målestok");
        };
        layout.Children.Add(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 6,
                    Children = { currentShotFilter.Column(0), allShotsFilter.Column(1) }
                },
                map,
                new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 6,
                    Children = { from.Column(0), target.Column(1), MapButton("Zoom", map.ToggleZoom).Column(2) }
                },
                status
            }
        });
        startLieSection = StartLieChoices();
        layout.Children.Add(startLieSection);
        layout.Children.Add(help); layout.Children.Add(mapHelp);
        layout.Children.Add(CoursePracticeViews.Field("Navn (valgfrit)", shotName));
        layout.Children.Add(shotNameSuggestion);
        attempts.Accessible("CoursePractice.Attempts", "Antal slag fra denne startposition, fra 1 til 100");
        layout.Children.Add(CoursePracticeViews.Field("Antal slag", attempts));
        layout.Children.Add(new Label { Text = "Standard for fremtidige runder. Hvert slag spilles fra samme startposition mod samme mål og får sit eget resultat.", TextColor = GolfTheme.Colors.MutedText });
        layout.Children.Add(CoursePracticeViews.Field("SG-kategori (vælg omkring green for chips/bunkerslag)", category));
        layout.Children.Add(CoursePracticeViews.Field("Målt afstand, m (overstyr billedestimat)", measured));
        layout.Children.Add(new Label { Text = "En målt afstand ryddes, når du flytter ét af slagets punkter.", TextColor = GolfTheme.Colors.MutedText });
        measured.TextChanged += (_, _) => UpdateDistance();
        shotName.TextChanged += (_, e) => { if (!loading && !string.IsNullOrWhiteSpace(e.NewTextValue)) dirty = true; };
        measured.TextChanged += (_, e) => { if (!loading && !string.IsNullOrWhiteSpace(e.NewTextValue)) dirty = true; };
        attempts.TextChanged += (_, _) => { if (!loading) dirty = true; };
        footer.Children.Add(error);
        footer.Children.Add(AsyncButton("Gem og næste slag", SaveShotAndContinueAsync, true)
            .Accessible("CoursePractice.SaveShotAndContinue", "Gem eller opdater slaget og gå til næste startposition"));
        layout.Children.Add(AppViews.PageTitle("Planlagte slag", 20)); layout.Children.Add(shotList);
        layout.Children.Add(CoursePracticeViews.Field("Banens navn", courseName));
        layout.Children.Add(new Label { Text = "BETA · Gem start, mål og lie for hvert planlagt slag. Når banen er gemt, kan du vælge den under Spil bane. Afstande og SG er estimater.", TextColor = GolfTheme.Colors.WarningText });
        layout.Children.Add(AsyncButton("Gem bane", async () => { if (await SaveCourseAsync()) await Navigation.PopAsync(); }, true)
            .Accessible("CoursePractice.SaveCourse", "Gem banen og gå tilbage"));
        error.IsVisible = false;
        error.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Label.Text)) error.IsVisible = !string.IsNullOrWhiteSpace(error.Text); };
        courseScroll.Content = layout;
        Content = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { courseScroll.Row(0), footer.Row(1) }
        };
        courseName.TextChanged += (_, _) =>
        {
            var name = courseName.Text?.Trim();
            Title = string.IsNullOrEmpty(name) ? "Opsæt bane · Beta" : $"{name} · Beta";
            if (!loading) dirty = true;
        };
    }

    public static async Task OpenNewAsync(Page owner, CoursePracticeService service)
    {
        var initialName = "";
        while (true)
        {
            var name = await owner.DisplayPromptAsync("Ny bane", "Hvad skal banen hedde?", "Fortsæt", "Annuller",
                placeholder: "Fx min træningsbane", maxLength: 100, keyboard: Keyboard.Text, initialValue: initialName);
            if (name is null) return;
            initialName = name;
            name = name.Trim();
            if (name.Length is 0 or > 100)
            {
                await owner.DisplayAlertAsync("Banens navn", "Angiv et navn på 1–100 tegn.", "OK");
                continue;
            }
            var page = new CourseSetupPage(service);
            page.courseName.Text = name;
            await owner.Navigation.PushAsync(page);
            return;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAsync(async () => { await CoursePracticeImages.EnsureSampleAsync(); map.SetImage(CoursePracticeImages.PathFor(imageFile)); UpdateDistance(); });
    }
    private PracticeCourse Capture() => new(courseId, courseName.Text?.Trim() ?? "", imageFile, map.ImageAspectRatio, metresPerWidth, scaleNote, shots.ToArray());
    private async Task<bool> SaveCourseAsync()
    {
        if (calibrationMode) throw new ArgumentException("Afslut kalibreringen, før du gemmer banen.");
        if (editingId is not null || map.From is not null || map.Target is not null ||
            !string.IsNullOrWhiteSpace(shotName.Text) || !string.IsNullOrWhiteSpace(measured.Text) || startingLie is not null || attempts.Text?.Trim() != "1")
        {
            if (shots.Count == 0) throw new ArgumentException("Tryk Gem og næste slag, før du gemmer banen.");
            CoursePracticeService.ValidateCourse(Capture());
            if (!await DisplayAlertAsync("Ikke gemt slag", "Vil du kassere det aktuelle slag og gemme banen med de allerede gemte slag?", "Kassér slag og gem", "Tilbage")) return false;
            ResetShot(); RefreshShots();
        }
        await service.SaveCourseAsync(Capture()); dirty = false;
        return true;
    }
    public void EditCourse(PracticeCourse course)
    {
        CoursePracticeService.ValidateCourse(course);
        loading = true; courseId = course.Id; courseName.Text = course.Name; imageFile = course.ImageFile;
        map.ImageAspectRatio = course.ImageAspectRatio; metresPerWidth = course.MetresPerImageWidth; scaleNote = course.ScaleNote;
        shots = course.Shots.ToList(); map.SetImage(CoursePracticeImages.PathFor(imageFile)); ResetShot(); RefreshShots(); loading = false; dirty = false;
    }
    private void ResetShot()
    {
        editingId = null; map.From = map.Target = null; map.PlaceFrom = true; shotName.Text = ""; measured.Text = ""; SelectStartingLie(null);
        attempts.Text = "1";
        calibrationMode = false; map.SelectedShotId = null; map.DragSavedShots = true; UpdateMapShots(); UpdateDistance();
    }
    private async Task ReturnToPictureAsync()
    {
        foreach (var entry in new[] { courseName, shotName, measured, attempts })
        {
            if (entry.IsSoftInputShowing()) await entry.HideSoftInputAsync(CancellationToken.None);
        }
        ReleaseInputFocus();
        await Task.Yield();
        await courseScroll.ScrollToAsync(map, ScrollToPosition.Start, animated: true);
    }
    private void ReleaseInputFocus()
    {
        foreach (var entry in new[] { courseName, shotName, measured, attempts }) entry.Unfocus();
        category.Unfocus();
#if ANDROID
        // Give Android a focus target outside the inputs so a hint/layout update
        // cannot bring the previous EditText back into view while editing the map.
        if (courseScroll.Handler?.PlatformView is Android.Views.ViewGroup scroll)
        {
            scroll.FocusableInTouchMode = true;
            var previousFocusability = scroll.DescendantFocusability;
            scroll.DescendantFocusability = Android.Views.DescendantFocusability.BeforeDescendants;
            try { scroll.RequestFocus(); }
            finally { scroll.DescendantFocusability = previousFocusability; }
        }
#endif
    }
    private async Task SaveShotAndContinueAsync()
    {
        if (calibrationMode) throw new ArgumentException("Afslut kalibreringen først.");
        if (map.From is null || map.Target is null) throw new ArgumentException("Vælg start og mål på billedet.");
        if (startingLie is null)
        {
            await courseScroll.ScrollToAsync(startLieSection, ScrollToPosition.Start, animated: true);
            throw new ArgumentException("Vælg hvor bolden ligger ved start, fx Fairway eller Bunker.");
        }
        if (!TryAttempts(attempts.Text, out var attemptCount)) throw new ArgumentException("Angiv et helt antal slag mellem 1 og 100.");
        double? distance = null;
        if (!string.IsNullOrWhiteSpace(measured.Text))
        {
            if (!TryDistance(measured.Text, out var value)) throw new ArgumentException("Angiv en positiv målt afstand i meter.");
            distance = value;
        }
        var id = editingId ?? Guid.NewGuid().ToString("N");
        var shot = new CoursePracticeShot(id, string.IsNullOrWhiteSpace(shotName.Text) ? SuggestedShotName() : shotName.Text.Trim(),
            map.From, map.Target, startingLie.Value, (PracticeShotCategory)category.SelectedIndex, distance, attemptCount);
        var next = shots.Where(s => s.Id != id).ToList();
        var index = shots.FindIndex(s => s.Id == id); if (index < 0) next.Add(shot); else next.Insert(index, shot);
        CoursePracticeService.ValidateCourse(Capture() with { Name = string.IsNullOrWhiteSpace(courseName.Text) ? "Bane" : courseName.Text.Trim(), Shots = next });
        ReleaseInputFocus();
        shots = next; dirty = true; ResetShot(); map.DragSavedShots = false;
        RefreshShots(); UpdateDistance();
        try { await ReturnToPictureAsync(); }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Slaget er gemt i opsætningen. Rul op til billedet for at vælge næste startposition.", ex);
        }
    }
    private void RefreshShots()
    {
        UpdateMapShots();
        shotList.Children.Clear();
        foreach (var shot in shots)
        {
            var edit = ActionButton("Rediger slag", () =>
            {
                editingId = shot.Id; map.From = shot.From; map.Target = shot.Target; shotName.Text = shot.Name;
                attempts.Text = shot.Attempts.ToString(System.Globalization.CultureInfo.InvariantCulture);
                map.SelectedShotId = null;
                dirty = true;
                measured.Text = shot.MeasuredDistanceMeters?.ToString("0.###"); SelectStartingLie(shot.Lie); category.SelectedIndex = (int)shot.Category;
                UpdateMapShots(); UpdateDistance();
            });
            var remove = ActionButton("Slet", () => { shots.Remove(shot); if (editingId == shot.Id) ResetShot(); if (map.SelectedShotId == shot.Id) map.SelectedShotId = null; dirty = true; RefreshShots(); UpdateDistance(); });
            shotList.Children.Add(AppViews.Card(new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = $"{shots.IndexOf(shot) + 1}. {shot.Name} · {ShotLieLabels.Format(shot.Lie)} · {CoursePracticeViews.FormatDistanceMeters(shot.DistanceMeters(Capture()))} · {shot.Attempts} slag", TextColor = GolfTheme.Colors.Text, FontSize = 16 },
                    new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                        ColumnSpacing = 8,
                        Children = { edit.Column(0), remove.Column(1) }
                    }
                }
            }));
        }
    }
    private void UpdateMapShots()
    {
        map.SavedShots = calibrationMode ? [] : shots.ToArray();
        map.EditingShotId = editingId;
        map.CurrentShotNumber = CurrentShotNumber;
        map.IsCalibration = calibrationMode;
        map.Refresh();
    }
    private async Task CalibrateAsync()
    {
        if (!calibrationMode)
        {
            beforeFrom = map.From; beforeTarget = map.Target; map.From = map.Target = null; map.PlaceFrom = true;
            calibrationMode = true; UpdateMapShots(); UpdateDistance(); return;
        }
        if (map.From is null || map.Target is null) throw new ArgumentException("Placér begge referencepunkter først.");
        var text = await DisplayPromptAsync("Kendt afstand", "Afstanden mellem de to referencepunkter i meter", "Anvend", "Annuller", keyboard: Keyboard.Numeric);
        if (text is null) { EndCalibration(); return; }
        if (!TryDistance(text, out var metres)) throw new ArgumentException("Angiv en positiv afstand i meter.");
        var length = new CoursePracticeShot("ref", "ref", map.From, map.Target, ShotLie.Fairway, PracticeShotCategory.Approach).DistanceMeters(Capture() with { MetresPerImageWidth = 1 });
        if (length < .01) throw new ArgumentException("Vælg referencepunkter længere fra hinanden.");
        if (metres / length > 10000) throw new ArgumentException("Målestokken er for stor. Kontrollér referenceafstanden.");
        metresPerWidth = metres / length; scaleNote = $"Kalibreret med {CoursePracticeViews.FormatDistanceMeters(metres)}-reference. Billedafstande er estimater.";
        dirty = true; EndCalibration(); RefreshShots();
    }
    private void EndCalibration() { map.From = beforeFrom; map.Target = beforeTarget; calibrationMode = false; UpdateMapShots(); UpdateDistance(); }
    private void UpdateDistance()
    {
        shotNameSuggestion.Text = $"Foreslået navn: {SuggestedShotName()}";
        scale.Text = scaleNote;
        if (calibrationMode)
        {
            map.SetDistanceSummary(null);
            status.Text = "Placér to referencepunkter, og tryk Kalibrer målestok igen."; return;
        }
        var selected = shots.FirstOrDefault(s => s.Id == map.SelectedShotId);
        if (map.From is null && map.Target is null && selected is not null)
        {
            var savedDistance = selected.DistanceMeters(Capture());
            map.SetDistanceSummary(savedDistance, selected.MeasuredDistanceMeters is not null);
            status.Text = $"{selected.Name} · {ShotLieLabels.Format(selected.Lie)} · Træk punkterne eller linjen.";
            return;
        }
        if (map.From is null || map.Target is null)
        {
            map.SetDistanceSummary(null);
            status.Text = map.From is null ? (map.DragSavedShots && map.ShowSavedShots ? "Tryk for at vælge start, eller træk et gemt slag." : "Tryk på billedet for at vælge start (grøn).") : "Vælg mål (orange). Punkterne kan trækkes.";
            return;
        }
        var isMeasured = TryDistance(measured.Text, out var distance);
        if (!isMeasured) distance = new CoursePracticeShot("preview", "preview", map.From, map.Target, ShotLie.Fairway, PracticeShotCategory.Approach).DistanceMeters(Capture());
        map.SetDistanceSummary(distance, isMeasured);
        status.Text = "Træk et punkt eller linjen for at justere slaget.";
    }
    internal static bool TryDistance(string? text, out double value) =>
        double.TryParse(text?.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value is > 0 and <= 1000;
    internal static bool TryAttempts(string? text, out int value) =>
        int.TryParse(text?.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) && value is >= 1 and <= 100;
    private async Task<bool> MayReplaceAsync() => !dirty || await DisplayAlertAsync("Ikke gemte ændringer", "Vil du kassere ændringerne til banen?", "Kassér", "Annuller");
    protected override bool OnBackButtonPressed() { if (busy) return true; if (!dirty) return base.OnBackButtonPressed(); _ = LeaveAsync(); return true; }
    private async Task LeaveAsync()
    {
        if (busy) return;
        await RunAsync(async () => { if (await MayReplaceAsync()) await Navigation.PopAsync(); });
    }
    private Button ActionButton(string text, Action action) { var b = CoursePracticeViews.SecondaryButton(text); b.Clicked += (_, _) => { if (!busy) action(); }; return b; }
    private View StartLieChoices()
    {
        var choices = new Grid
        {
            ColumnSpacing = 6, RowSpacing = 6,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        for (var i = 0; i < Lies.Length; i++)
        {
            var value = Lies[i];
            var button = MapButton(StartLieLabel(value), () => SelectStartingLie(value))
                .Accessible($"CoursePractice.StartLie.{value}", $"Start: {StartLieLabel(value)}");
            lieButtons.Add(value, button);
            choices.Children.Add(button.Row(i / 3).Column(i % 3));
            if (i == Lies.Length - 1) Grid.SetColumnSpan(button, 3);
        }
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children = { new Label { Text = "Hvor ligger bolden ved start?", FontSize = 14 }, choices }
        };
    }
    private static string StartLieLabel(ShotLie value) => value == ShotLie.Sand ? "Bunker" : ShotLieLabels.Format(value);
    private void SelectStartingLie(ShotLie? value)
    {
        startingLie = value;
        foreach (var (lieValue, button) in lieButtons)
        {
            var selected = lieValue == value;
            button.BackgroundColor = selected ? GolfTheme.Colors.PrimaryGreen : GolfTheme.Colors.CardBackground;
            button.TextColor = selected ? Colors.White : GolfTheme.Colors.PrimaryGreen;
            SemanticProperties.SetDescription(button, $"Start: {StartLieLabel(lieValue)}{(selected ? ", valgt" : "")}");
        }
        if (value == ShotLie.Green) category.SelectedIndex = 0;
        else if (category.SelectedIndex == 0 || value == ShotLie.Tee) category.SelectedIndex = 1;
        if (!loading) dirty = true;
        UpdateDistance();
    }
    private string SuggestedShotName()
    {
        var name = $"Slag {CurrentShotNumber}";
        if (startingLie is { } value) name += $" · {StartLieLabel(value)}";
        if (map.From is not null && map.Target is not null && !calibrationMode)
        {
            if (!TryDistance(measured.Text, out var distance))
                distance = new CoursePracticeShot("preview", "preview", map.From, map.Target, ShotLie.Fairway, PracticeShotCategory.Approach).DistanceMeters(Capture());
            name += $" · {CoursePracticeViews.FormatDistanceMeters(distance)}";
        }
        return name;
    }
    private int CurrentShotNumber
    {
        get
        {
            var index = editingId is null ? -1 : shots.FindIndex(s => s.Id == editingId);
            return index < 0 ? shots.Count + 1 : index + 1;
        }
    }
    private Button MapButton(string text, Action? action = null)
    {
        var button = action is null ? CoursePracticeViews.SecondaryButton(text) : ActionButton(text, action);
        button.FontSize = 14;
        button.MinimumHeightRequest = 44;
        button.Padding = new Thickness(8, 6);
        button.BorderWidth = 1;
        return button;
    }
    private Button AsyncButton(string text, Func<Task> action, bool primary = false) { var b = primary ? CoursePracticeViews.PrimaryButton(text) : CoursePracticeViews.SecondaryButton(text); b.Clicked += async (_, _) => await RunAsync(action); return b; }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return; busy = true; layout.IsEnabled = footer.IsEnabled = false; map.Editable = false; error.Text = "";
        try { await action(); } catch (Exception ex) { error.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Kunne ikke gemme eller åbne. Prøv igen; dine data er bevaret."; }
        finally { busy = false; layout.IsEnabled = footer.IsEnabled = true; map.Editable = true; }
    }
}
