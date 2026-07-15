using GolfSG.Services;
using GolfSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using System.ComponentModel;

namespace GolfSG.Views;

public sealed class HoleEntryPage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color InputBackground = GolfTheme.Colors.InputBackground;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color SoftGreen = GolfTheme.Colors.SoftGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;
    private const double MaxFirstPuttDistanceMeters = 30;
    private const double MaxApproachDistanceMeters = 250;
    private const double MaxAroundGreenDistanceMeters = 50;
    private const double MaxFinishDistanceMeters = 250;


    private static readonly IReadOnlyList<DistanceQuickPick> ApproachQuickPicks =
        SgDistanceInputPresets.ToQuickPicks(SgDistanceInputPresets.ApproachMeters, MaxApproachDistanceMeters, 0);

    private static readonly IReadOnlyList<DistanceQuickPick> AroundGreenQuickPicks =
        SgDistanceInputPresets.ToQuickPicks(SgDistanceInputPresets.AroundGreenMeters, MaxAroundGreenDistanceMeters, 0);

    private static readonly IReadOnlyList<DistanceQuickPick> OffGreenFinishQuickPicks =
        SgDistanceInputPresets.ToQuickPicks(SgDistanceInputPresets.OffGreenFinishMeters, MaxFinishDistanceMeters, 0);

    private readonly RoundInputViewModel roundViewModel;
    private readonly HoleInputViewModel viewModel;
    private readonly bool useGuidedInput;
    private HoleEntryStep activeStep;
    private View? approachSectionView;
    private View? aroundGreenSectionView;
    private View? puttingSectionView;
    private Label? guidedStepLabel;
    private Button? guidedBackButton;
    private Button? guidedNextButton;
    private CancellationTokenSource? guidedAutoAdvanceCts;
    private bool isGuidedAutoAdvancing;
    private bool suppressGuidedAutoAdvanceAfterBack;
    private bool isViewModelSubscribed;

    public HoleEntryPage(RoundInputViewModel roundViewModel, HoleInputViewModel viewModel)
    {
        this.roundViewModel = roundViewModel;
        this.viewModel = viewModel;
        useGuidedInput = FeatureSettings.UseGuidedHoleEntry;
        BindingContext = viewModel;
        Title = viewModel.Title;
        BackgroundColor = PageBackground;
        BuildLayout();
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "⚙",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            Command = new Command(async () => await Navigation.PushAsync(new RoundSettingsPage(roundViewModel, viewModel.HoleNumber)))
        });
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(async () => await ConfirmCloseRoundAsync())
        });
    }

    private enum HoleEntryStep
    {
        Approach,
        AroundGreen,
        Putting
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (useGuidedInput && !isViewModelSubscribed)
        {
            viewModel.PropertyChanged += OnHoleInputPropertyChanged;
            isViewModelSubscribed = true;
            UpdateGuidedStepVisibility();
        }
    }

    protected override void OnDisappearing()
    {
        CancelGuidedAutoAdvance();
        if (isViewModelSubscribed)
        {
            viewModel.PropertyChanged -= OnHoleInputPropertyChanged;
            isViewModelSubscribed = false;
        }

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (useGuidedInput && HasPreviousGuidedStep())
        {
            _ = GoBackInGuidedFlowAsync();
            return true;
        }

        _ = ConfirmCloseRoundAsync();
        return true;
    }

    private void BuildLayout()
    {
        var previousHole = GetPreviousHole();
        var nextHole = GetNextHole();
        var puttingQuickPicks = SgDistanceInputPresets.ToQuickPicks(
            SgDistanceInputPresets.PuttingMeters,
            MaxFirstPuttDistanceMeters,
            1,
            viewModel.PuttingDistanceUnitPreference);

        var previous = new Button
        {
            Text = "Forrige hul",
            HeightRequest = 40,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold,
            IsVisible = previousHole is not null
        };
        previous.Clicked += async (_, _) => await GoToHoleAsync(GetPreviousHole());

        var title = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center
        };
        title.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Title));

        var subtitle = new Label
        {
            FontSize = 14,
            TextColor = MutedTextColor,
            HorizontalTextAlignment = TextAlignment.Center
        };
        subtitle.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Subtitle));

        var puttingDistance = DistanceSlider(MaxFirstPuttDistanceMeters, SgDistanceInputPresets.PuttingMeters);
        puttingDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.FirstPuttDistanceMeters), BindingMode.TwoWay);

        var puttingDistanceValue = DistanceValueLabel();
        puttingDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.FirstPuttDistanceDisplayText));

        var puttingDistanceMinus = DistanceStepperButton("-");
        puttingDistanceMinus.Clicked += (_, _) => viewModel.DecreaseFirstPuttDistance();

        var puttingDistancePlus = DistanceStepperButton("+");
        puttingDistancePlus.Clicked += (_, _) => viewModel.IncreaseFirstPuttDistance();

        var putts = CountLabel();
        putts.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Putts));

        var puttMinus = RoundStepperButton("-");
        puttMinus.Clicked += (_, _) => viewModel.DecreasePutts();

        var puttPlus = RoundStepperButton("+");
        puttPlus.Clicked += (_, _) => viewModel.IncreasePutts();

        var puttQuickActions = NumberQuickActions(
            nameof(HoleInputViewModel.Putts),
            ("1 putt", 1, () => SetPuttsFromQuickAction(1)),
            ("2 putts", 2, () => SetPuttsFromQuickAction(2)),
            ("3 putts", 3, () => SetPuttsFromQuickAction(3)));

        var puttingSg = SgLabel();
        puttingSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedPuttingText), stringFormat: "SG Putning {0}"));

        var carriedPuttingDistance = CarriedPuttingDistancePanel(() => viewModel.EditCarriedPuttingDistance());

        var approachDistance = DistanceSlider(MaxApproachDistanceMeters, SgDistanceInputPresets.ApproachMeters);
        approachDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.ApproachDistanceMeters), BindingMode.TwoWay);

        var approachDistanceValue = DistanceValueLabel();
        approachDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachDistanceDisplayText));

        var approachDistanceMinus = DistanceStepperButton("-");
        approachDistanceMinus.Clicked += (_, _) => viewModel.DecreaseApproachDistance();

        var approachDistancePlus = DistanceStepperButton("+");
        approachDistancePlus.Clicked += (_, _) => viewModel.IncreaseApproachDistance();

        var approachShots = CountLabel();
        approachShots.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachShots));

        var approachMinus = RoundStepperButton("-");
        approachMinus.Clicked += (_, _) => viewModel.DecreaseApproachShots();

        var approachPlus = RoundStepperButton("+");
        approachPlus.Clicked += (_, _) => viewModel.IncreaseApproachShots();

        var approachSg = SgLabel();
        approachSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedApproachText), stringFormat: "SG Indspil {0}"));

        var approachStartLie = QuickChoicePanel(
            "Startleje",
            nameof(HoleInputViewModel.ApproachStartLieText),
            ("Fairway", () => viewModel.SelectApproachStartLie("Fairway")),
            ("Rough", () => viewModel.SelectApproachStartLie("Rough")),
            ("Sand", () => viewModel.SelectApproachStartLie("Sand")),
            ("Problemlie", () => viewModel.SelectApproachStartLie("Problemlie")));

        var approachStartDistance = DistanceSlider(MaxApproachDistanceMeters, SgDistanceInputPresets.ApproachMeters);
        approachStartDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.ApproachStartDistanceYards), BindingMode.TwoWay);

        var approachStartDistanceValue = DistanceValueLabel();
        approachStartDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachStartDistanceDisplayText));

        var approachStartDistanceMinus = DistanceStepperButton("-");
        approachStartDistanceMinus.Clicked += (_, _) => viewModel.DecreaseApproachStartDistance();

        var approachStartDistancePlus = DistanceStepperButton("+");
        approachStartDistancePlus.Clicked += (_, _) => viewModel.IncreaseApproachStartDistance();

        var approachEndLie = QuickChoicePanel(
            "Slutposition",
            nameof(HoleInputViewModel.ApproachEndLieText),
            ("Green", () => viewModel.SelectApproachEndLie("Green")),
            ("Kortklippet", () => viewModel.SelectApproachEndLie("Kortklippet")),
            ("Rough", () => viewModel.SelectApproachEndLie("Rough")),
            ("Sand", () => viewModel.SelectApproachEndLie("Sand")),
            ("I hul", () => viewModel.SelectApproachEndLie("I hul")));

        var approachEndDistance = ExpandingDistanceSlider(MaxFirstPuttDistanceMeters, MaxFinishDistanceMeters, SgDistanceInputPresets.FinishMeters);
        approachEndDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.ApproachEndDistance), BindingMode.TwoWay);

        var approachEndDistanceValue = DistanceValueLabel();
        approachEndDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachEndDistanceDisplayText));

        var approachEndDistanceMinus = DistanceStepperButton("-");
        approachEndDistanceMinus.Clicked += (_, _) => viewModel.DecreaseApproachEndDistance();

        var approachEndDistancePlus = DistanceStepperButton("+");
        approachEndDistancePlus.Clicked += (_, _) => viewModel.IncreaseApproachEndDistance();

        var approachEndDistancePanel = DistancePanel(
            "Slutafstand",
            approachEndDistance,
            approachEndDistanceValue,
            approachEndDistanceMinus,
            approachEndDistancePlus,
            nameof(HoleInputViewModel.ApproachEndDistanceText),
            [
                new DistanceQuickPickGroup(puttingQuickPicks, nameof(HoleInputViewModel.IsApproachEndOnGreen)),
                new DistanceQuickPickGroup(OffGreenFinishQuickPicks, nameof(HoleInputViewModel.IsApproachEndOffGreen))
            ],
            value => viewModel.ApproachEndDistance = value,
            nameof(HoleInputViewModel.ApproachEndDistanceUnitText));
        approachEndDistancePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsApproachFinishDistanceVisible));

        var approachHoled = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        approachHoled.SetBinding(Switch.IsToggledProperty, nameof(HoleInputViewModel.ApproachHoled), BindingMode.TwoWay);

        var approachPenaltyStrokes = CountLabel();
        approachPenaltyStrokes.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachPenaltyStrokes));

        var approachPenaltyMinus = RoundStepperButton("-");
        approachPenaltyMinus.Clicked += (_, _) => viewModel.DecreaseApproachPenaltyStrokes();

        var approachPenaltyPlus = RoundStepperButton("+");
        approachPenaltyPlus.Clicked += (_, _) => viewModel.IncreaseApproachPenaltyStrokes();

        var aroundGreenStartDistance = DistanceSlider(MaxAroundGreenDistanceMeters, SgDistanceInputPresets.AroundGreenMeters);
        aroundGreenStartDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.AroundGreenStartDistanceYards), BindingMode.TwoWay);

        var aroundGreenStartDistanceValue = DistanceValueLabel();
        aroundGreenStartDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenStartDistanceDisplayText));

        var aroundGreenStartDistanceMinus = DistanceStepperButton("-");
        aroundGreenStartDistanceMinus.Clicked += (_, _) => viewModel.DecreaseAroundGreenStartDistance();

        var aroundGreenStartDistancePlus = DistanceStepperButton("+");
        aroundGreenStartDistancePlus.Clicked += (_, _) => viewModel.IncreaseAroundGreenStartDistance();

        var aroundGreenStartLie = QuickChoicePanel(
            "Startleje",
            nameof(HoleInputViewModel.AroundGreenStartLieText),
            ("Rough", () => viewModel.SelectAroundGreenStartLie("Rough")),
            ("Kortklippet", () => viewModel.SelectAroundGreenStartLie("Kortklippet")),
            ("Sand", () => viewModel.SelectAroundGreenStartLie("Sand")),
            ("Problemlie", () => viewModel.SelectAroundGreenStartLie("Problemlie")));

        var aroundGreenEndLie = QuickChoicePanel(
            "Slutposition",
            nameof(HoleInputViewModel.AroundGreenEndLieText),
            ("Green", () => viewModel.SelectAroundGreenEndLie("Green")),
            ("Kortklippet", () => viewModel.SelectAroundGreenEndLie("Kortklippet")),
            ("Rough", () => viewModel.SelectAroundGreenEndLie("Rough")),
            ("Sand", () => viewModel.SelectAroundGreenEndLie("Sand")),
            ("I hul", () => viewModel.SelectAroundGreenEndLie("I hul")));

        var aroundGreenEndDistance = ExpandingDistanceSlider(MaxFirstPuttDistanceMeters, MaxFinishDistanceMeters, SgDistanceInputPresets.FinishMeters);
        aroundGreenEndDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.AroundGreenEndDistance), BindingMode.TwoWay);
        aroundGreenEndDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenFinishDistanceVisible));

        var aroundGreenEndDistanceValue = DistanceValueLabel();
        aroundGreenEndDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenEndDistanceDisplayText));

        var aroundGreenEndDistanceMinus = DistanceStepperButton("-");
        aroundGreenEndDistanceMinus.Clicked += (_, _) => viewModel.DecreaseAroundGreenEndDistance();

        var aroundGreenEndDistancePlus = DistanceStepperButton("+");
        aroundGreenEndDistancePlus.Clicked += (_, _) => viewModel.IncreaseAroundGreenEndDistance();

        var aroundGreenEndDistancePanel = DistancePanel(
            "Slutafstand",
            aroundGreenEndDistance,
            aroundGreenEndDistanceValue,
            aroundGreenEndDistanceMinus,
            aroundGreenEndDistancePlus,
            nameof(HoleInputViewModel.AroundGreenEndDistanceText),
            [
                new DistanceQuickPickGroup(puttingQuickPicks, nameof(HoleInputViewModel.IsAroundGreenEndOnGreen)),
                new DistanceQuickPickGroup(AroundGreenQuickPicks, nameof(HoleInputViewModel.IsAroundGreenEndOffGreen))
            ],
            value => viewModel.AroundGreenEndDistance = value,
            nameof(HoleInputViewModel.AroundGreenEndDistanceUnitText));
        aroundGreenEndDistancePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenFinishDistanceVisible));

        var aroundGreenPenaltyStrokes = CountLabel();
        aroundGreenPenaltyStrokes.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenPenaltyStrokes));

        var aroundGreenPenaltyMinus = RoundStepperButton("-");
        aroundGreenPenaltyMinus.Clicked += (_, _) => viewModel.DecreaseAroundGreenPenaltyStrokes();

        var aroundGreenPenaltyPlus = RoundStepperButton("+");
        aroundGreenPenaltyPlus.Clicked += (_, _) => viewModel.IncreaseAroundGreenPenaltyStrokes();

        var aroundGreenHoled = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        aroundGreenHoled.SetBinding(Switch.IsToggledProperty, nameof(HoleInputViewModel.AroundGreenHoled), BindingMode.TwoWay);

        var aroundGreenSg = SgLabel();
        aroundGreenSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedAroundGreenText), stringFormat: "SG Omkring green {0}"));

        var completedAroundGreenShots = CompletedAroundGreenShotsPanel();

        var addAroundGreenShot = new Button
        {
            Text = "Tilføj nyt slag omkring green",
            HeightRequest = 50,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold
        };
        addAroundGreenShot.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.CanAddAnotherAroundGreenShot));
        addAroundGreenShot.Clicked += (_, _) => viewModel.AddAnotherAroundGreenShot();

        var undoAroundGreenShot = new Button
        {
            Text = "Fortryd seneste slag",
            HeightRequest = 44,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = GolfTheme.Colors.DangerText,
            BorderWidth = 1,
            TextColor = GolfTheme.Colors.DangerText,
            FontAttributes = FontAttributes.Bold
        };
        undoAroundGreenShot.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.CanUndoLastAroundGreenShot));
        undoAroundGreenShot.Clicked += (_, _) => viewModel.UndoLastAroundGreenShot();

        var puttingSection = PuttingSection(
            puttMinus,
            putts,
            puttPlus,
            puttQuickActions,
            puttingDistance,
            puttingDistanceValue,
            puttingDistanceMinus,
            puttingDistancePlus,
            nameof(HoleInputViewModel.DistanceText),
            puttingQuickPicks,
            nameof(HoleInputViewModel.PuttingDistanceUnitText),
            value => viewModel.FirstPuttDistanceMeters = value,
            carriedPuttingDistance,
            puttingSg);
        if (!useGuidedInput)
        {
            puttingSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsPuttingInputVisible));
        }

        var approachSection = ApproachSection(
            approachStartLie,
            approachStartDistance,
            approachStartDistanceValue,
            approachStartDistanceMinus,
            approachStartDistancePlus,
            nameof(HoleInputViewModel.ApproachStartDistanceText),
            ApproachQuickPicks,
            value => viewModel.ApproachStartDistanceYards = value,
            approachEndLie,
            approachEndDistancePanel,
            approachHoled,
            approachPenaltyMinus,
            approachPenaltyStrokes,
            approachPenaltyPlus,
            approachSg);
        if (!useGuidedInput)
        {
            approachSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsApproachInputVisible));
        }

        var aroundGreenSection = AroundGreenSection(
            completedAroundGreenShots,
            aroundGreenStartLie,
            aroundGreenStartDistance,
            aroundGreenStartDistanceValue,
            aroundGreenStartDistanceMinus,
            aroundGreenStartDistancePlus,
            nameof(HoleInputViewModel.AroundGreenStartDistanceText),
            AroundGreenQuickPicks,
            value => viewModel.AroundGreenStartDistanceYards = value,
            aroundGreenEndLie,
            aroundGreenEndDistancePanel,
            aroundGreenHoled,
            aroundGreenPenaltyMinus,
            aroundGreenPenaltyStrokes,
            aroundGreenPenaltyPlus,
            addAroundGreenShot,
            undoAroundGreenShot,
            aroundGreenSg);
        if (!useGuidedInput)
        {
            aroundGreenSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenInputVisible));
        }

        approachSectionView = approachSection;
        aroundGreenSectionView = aroundGreenSection;
        puttingSectionView = puttingSection;

        var done = new Button
        {
            Text = "Færdig",
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold
        };
        done.Clicked += async (_, _) => await Navigation.PopAsync();

        var next = new Button
        {
            Text = nextHole is null ? "Til oversigt" : "Næste hul",
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold
        };
        next.Clicked += async (_, _) => await GoToNextHoleOrOverviewAsync();

        if (useGuidedInput)
        {
            BuildGuidedContent(previous, title, subtitle, approachSection, aroundGreenSection, puttingSection);
            return;
        }

        Content = new Grid
        {
            Padding = 20,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                previous.Row(0),
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children = { title, subtitle }
                }.Row(1).Margin(new Thickness(0, 12, 0, 0)),
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 16,
                        Padding = new Thickness(0, 0, 0, 20),
                        Children = { approachSection, aroundGreenSection, puttingSection }
                    }
                }.Row(2).Margin(new Thickness(0, 22, 0, 12)),
                new Grid
                {
                    Padding = new Thickness(0, 8, 0, 0),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 10,
                    Children =
                    {
                        done.Column(0),
                        next.Column(1)
                    }
                }.Row(3)
            }
        };

    }

    private void BuildGuidedContent(
        Button previousHole,
        Label title,
        Label subtitle,
        View approachSection,
        View aroundGreenSection,
        View puttingSection)
    {
        var visibleSteps = GetVisibleSteps();
        activeStep = visibleSteps.Count > 0 ? visibleSteps[0] : HoleEntryStep.Putting;

        guidedStepLabel = new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };

        guidedBackButton = new Button
        {
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold
        };
        guidedBackButton.Clicked += async (_, _) => await GoBackInGuidedFlowAsync();

        guidedNextButton = new Button
        {
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold
        };
        guidedNextButton.Clicked += async (_, _) => await GoForwardInGuidedFlowAsync();

        UpdateGuidedStepVisibility();

        Content = new Grid
        {
            Padding = 20,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                previousHole.Row(0),
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children = { title, subtitle }
                }.Row(1).Margin(new Thickness(0, 12, 0, 0)),
                guidedStepLabel.Row(2),
                new ScrollView
                {
                    Content = new Grid
                    {
                        Children =
                        {
                            approachSection,
                            aroundGreenSection,
                            puttingSection
                        }
                    }
                }.Row(3).Margin(new Thickness(0, 16, 0, 12)),
                new Grid
                {
                    Padding = new Thickness(0, 8, 0, 0),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 10,
                    Children =
                    {
                        guidedBackButton.Column(0),
                        guidedNextButton.Column(1)
                    }
                }.Row(4)
            }
        };
    }

    private void OnHoleInputPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        var previousStep = activeStep;
        UpdateGuidedStepVisibility();
        var isReady = CanAdvanceCurrentGuidedStep();

        if (previousStep == activeStep && isReady && !suppressGuidedAutoAdvanceAfterBack)
        {
            ScheduleGuidedAutoAdvance(activeStep);
        }

    }

    private void SetPuttsFromQuickAction(int putts)
    {
        viewModel.SetPutts(putts);
        if (useGuidedInput)
        {
            _ = AdvanceGuidedPuttingAfterQuickActionAsync();
        }
    }

    private async Task AdvanceGuidedPuttingAfterQuickActionAsync()
    {
        CancelGuidedAutoAdvance();
        await Task.Yield();
        if (activeStep == HoleEntryStep.Putting &&
            viewModel.CanAdvancePuttingStep &&
            !suppressGuidedAutoAdvanceAfterBack)
        {
            await GoForwardInGuidedFlowAsync();
        }
    }

    private void UpdateGuidedStepVisibility()
    {
        if (!useGuidedInput)
        {
            return;
        }

        var visibleSteps = GetVisibleSteps();
        if (visibleSteps.Count == 0)
        {
            SetSectionVisibility(false, false, false);
            if (guidedStepLabel is not null)
            {
                guidedStepLabel.Text = string.Empty;
            }

            if (guidedBackButton is not null)
            {
                guidedBackButton.Text = "Til oversigt";
            }

            if (guidedNextButton is not null)
            {
                guidedNextButton.Text = GetNextHole() is null ? "Til oversigt" : "Næste hul";
            }

            return;
        }

        if (!visibleSteps.Contains(activeStep))
        {
            activeStep = visibleSteps[0];
        }

        var index = visibleSteps.IndexOf(activeStep);
        SetSectionVisibility(
            activeStep == HoleEntryStep.Approach && viewModel.IsApproachInputVisible,
            activeStep == HoleEntryStep.AroundGreen && viewModel.IsAroundGreenInputVisible,
            activeStep == HoleEntryStep.Putting && viewModel.IsPuttingInputVisible);

        if (guidedStepLabel is not null)
        {
            guidedStepLabel.Text = $"{index + 1} / {visibleSteps.Count} - {GetStepLabel(activeStep)}";
        }

        if (guidedBackButton is not null)
        {
            guidedBackButton.Text = index > 0 ? "Forrige" : "Til oversigt";
        }

        if (guidedNextButton is not null)
        {
            guidedNextButton.Text = index < visibleSteps.Count - 1
                ? $"Næste: {GetStepLabel(visibleSteps[index + 1])}"
                : GetNextHole() is null ? "Til oversigt" : "Næste hul";
        }
    }

    private void SetSectionVisibility(bool showApproach, bool showAroundGreen, bool showPutting)
    {
        if (approachSectionView is not null)
        {
            approachSectionView.IsVisible = showApproach;
        }

        if (aroundGreenSectionView is not null)
        {
            aroundGreenSectionView.IsVisible = showAroundGreen;
        }

        if (puttingSectionView is not null)
        {
            puttingSectionView.IsVisible = showPutting;
        }
    }

    private bool CanAdvanceCurrentGuidedStep()
    {
        return activeStep switch
        {
            HoleEntryStep.Approach => viewModel.CanAdvanceApproachStep,
            HoleEntryStep.AroundGreen => viewModel.CanAdvanceAroundGreenStep,
            HoleEntryStep.Putting => viewModel.CanAdvancePuttingStep,
            _ => false
        };
    }

    private bool HasPreviousGuidedStep()
    {
        var visibleSteps = GetVisibleSteps();
        return visibleSteps.IndexOf(activeStep) > 0;
    }

    private void ScheduleGuidedAutoAdvance(HoleEntryStep step)
    {
        CancelGuidedAutoAdvance();
        guidedAutoAdvanceCts = new CancellationTokenSource();
        var token = guidedAutoAdvanceCts.Token;
        _ = AutoAdvanceGuidedStepAsync(step, token);
    }

    private async Task AutoAdvanceGuidedStepAsync(HoleEntryStep step, CancellationToken token)
    {
        try
        {
            await Task.Delay(650, token);
            if (token.IsCancellationRequested ||
                isGuidedAutoAdvancing ||
                activeStep != step ||
                !CanAdvanceCurrentGuidedStep())
            {
                return;
            }

            isGuidedAutoAdvancing = true;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (activeStep == step && CanAdvanceCurrentGuidedStep())
                {
                    await GoForwardInGuidedFlowAsync();
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            isGuidedAutoAdvancing = false;
        }
    }

    private void CancelGuidedAutoAdvance()
    {
        guidedAutoAdvanceCts?.Cancel();
        guidedAutoAdvanceCts?.Dispose();
        guidedAutoAdvanceCts = null;
    }

    private async Task GoBackInGuidedFlowAsync()
    {
        CancelGuidedAutoAdvance();
        var visibleSteps = GetVisibleSteps();
        var index = visibleSteps.IndexOf(activeStep);
        if (index > 0)
        {
            activeStep = visibleSteps[index - 1];
            suppressGuidedAutoAdvanceAfterBack = true;
            UpdateGuidedStepVisibility();
            return;
        }

        await Navigation.PopAsync();
    }

    private async Task GoForwardInGuidedFlowAsync()
    {
        CancelGuidedAutoAdvance();
        var visibleSteps = GetVisibleSteps();
        var index = visibleSteps.IndexOf(activeStep);
        if (index >= 0 && index < visibleSteps.Count - 1)
        {
            activeStep = visibleSteps[index + 1];
            UpdateGuidedStepVisibility();
            return;
        }

        await GoToNextHoleOrOverviewAsync();
    }

    private List<HoleEntryStep> GetVisibleSteps()
    {
        var steps = new List<HoleEntryStep>();
        if (viewModel.IsApproachInputVisible)
        {
            steps.Add(HoleEntryStep.Approach);
        }

        if (viewModel.IsAroundGreenInputVisible)
        {
            steps.Add(HoleEntryStep.AroundGreen);
        }

        if (viewModel.IsPuttingInputVisible)
        {
            steps.Add(HoleEntryStep.Putting);
        }

        return steps;
    }

    private static string GetStepLabel(HoleEntryStep step)
    {
        return step switch
        {
            HoleEntryStep.Approach => "Approach",
            HoleEntryStep.AroundGreen => "Omkring green",
            HoleEntryStep.Putting => "Putting",
            _ => string.Empty
        };
    }

    private async Task GoToNextHoleOrOverviewAsync()
    {
        var nextHole = GetNextHole();
        if (nextHole is null)
        {
            await Navigation.PopAsync();
            return;
        }

        await GoToHoleAsync(nextHole);
    }

    private async Task ConfirmCloseRoundAsync()
    {
        var closeRound = await DisplayAlertAsync(
            "Luk runde?",
            "Du er midt i en runde. Vil du lukke runden og miste den igangværende score?",
            "Luk runde",
            "Bliv her");

        if (closeRound)
        {
            await Navigation.PopToRootAsync();
        }
    }

    private async Task GoToHoleAsync(HoleInputViewModel? hole)
    {
        if (hole is null)
        {
            return;
        }

        var page = new HoleEntryPage(roundViewModel, hole)
        {
            suppressGuidedAutoAdvanceAfterBack = useGuidedInput && IsPreviousHole(hole)
        };
        Navigation.InsertPageBefore(page, this);
        await Navigation.PopAsync(animated: false);
    }

    private bool IsPreviousHole(HoleInputViewModel hole)
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var targetIndex = roundViewModel.Holes.IndexOf(hole);
        return currentIndex >= 0 && targetIndex >= 0 && targetIndex < currentIndex;
    }

    private HoleInputViewModel? GetPreviousHole()
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var previousIndex = currentIndex - 1;
        return previousIndex >= 0 ? roundViewModel.Holes[previousIndex] : null;
    }

    private HoleInputViewModel? GetNextHole()
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var nextIndex = currentIndex + 1;
        return currentIndex >= 0 && nextIndex < roundViewModel.Holes.Count
            ? roundViewModel.Holes[nextIndex]
            : null;
    }

    private static View PuttingSection(
        Button minus,
        Label putts,
        Button plus,
        View quickActions,
        Slider distance,
        Label distanceValue,
        Button distanceMinus,
        Button distancePlus,
        string distanceTextBindingPath,
        IReadOnlyList<DistanceQuickPick> quickPicks,
        string unitTextBindingPath,
        Action<double> selectDistance,
        View carriedDistance,
        Label sg)
    {
        var distancePanel = DistancePanel(
            "F\u00f8rste putt-afstand",
            distance,
            distanceValue,
            distanceMinus,
            distancePlus,
            distanceTextBindingPath,
            quickPicks,
            selectDistance,
            unitTextBindingPath);
        distancePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsPuttingDistanceInputVisible));

        return Card(new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                CounterPanel("Antal putts", minus, putts, plus),
                quickActions,
                carriedDistance,
                distancePanel,
                sg
            }
        });
    }

    private static View CarriedPuttingDistancePanel(Action editDistance)
    {
        var text = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            VerticalTextAlignment = TextAlignment.Center
        };
        text.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.CarriedPuttingDistanceText));

        var edit = new Button
        {
            Text = "Ret afstand",
            HeightRequest = 40,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            Padding = new Thickness(10, 0)
        };
        edit.Clicked += (_, _) => editDistance();

        var panel = new Border
        {
            BackgroundColor = SoftGreen,
            Stroke = CardStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(12, 10),
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10,
                Children =
                {
                    text.Column(0),
                    edit.Column(1)
                }
            }
        };
        panel.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.HasCarriedPuttingDistance));
        return panel;
    }

    private static View ApproachSection(
        View startLie,
        Slider startDistance,
        Label startDistanceValue,
        Button startDistanceMinus,
        Button startDistancePlus,
        string startDistanceTextBindingPath,
        IReadOnlyList<DistanceQuickPick> startQuickPicks,
        Action<double> selectStartDistance,
        View endLie,
        View endDistancePanel,
        Switch holed,
        Button penaltyMinus,
        Label penaltyStrokes,
        Button penaltyPlus,
        Label sg)
    {
        return Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                ShotPositionPanel(
                    "Start",
                    "Til flaget",
                    startLie,
                    startDistance,
                    startDistanceValue,
                    startDistanceMinus,
                    startDistancePlus,
                    startDistanceTextBindingPath,
                    startQuickPicks,
                    selectStartDistance),
                ShotPathDivider(),
                ShotPositionPanel("Slut", endLie, endDistancePanel),
                ToggleRow("I hul", holed),
                CounterPanel("Strafslag", penaltyMinus, penaltyStrokes, penaltyPlus),
                sg
            }
        });
    }

    private static View CompletedAroundGreenShotsPanel()
    {
        var shots = new VerticalStackLayout
        {
            Spacing = 0
        };
        BindableLayout.SetItemTemplate(shots, new DataTemplate(() =>
            {
                var title = new Label
                {
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                };
                title.SetBinding(Label.TextProperty, nameof(AroundGreenShotSummaryViewModel.Title));

                var start = new Label
                {
                    FontSize = 13,
                    TextColor = TextColor
                };
                start.SetBinding(Label.TextProperty, nameof(AroundGreenShotSummaryViewModel.StartText));

                var end = new Label
                {
                    FontSize = 13,
                    TextColor = TextColor
                };
                end.SetBinding(Label.TextProperty, nameof(AroundGreenShotSummaryViewModel.EndText));

                var penalties = new Label
                {
                    FontSize = 13,
                    TextColor = MutedTextColor
                };
                penalties.SetBinding(Label.TextProperty, nameof(AroundGreenShotSummaryViewModel.PenaltyText));

                return new Border
                {
                    BackgroundColor = InputBackground,
                    Stroke = CardStroke,
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(10, 8),
                    Margin = new Thickness(0, 0, 0, 8),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 3,
                        Children = { title, start, end, penalties }
                    }
                };
            }));
        shots.SetBinding(BindableLayout.ItemsSourceProperty, nameof(HoleInputViewModel.CompletedAroundGreenShotSummaries));
        shots.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.HasCompletedAroundGreenShots));

        return shots;
    }

    private static View AroundGreenSection(
        View completedShots,
        View startLie,
        Slider startDistance,
        Label startDistanceValue,
        Button startDistanceMinus,
        Button startDistancePlus,
        string startDistanceTextBindingPath,
        IReadOnlyList<DistanceQuickPick> startQuickPicks,
        Action<double> selectStartDistance,
        View endLie,
        View endDistancePanel,
        Switch holed,
        Button penaltyMinus,
        Label penaltyStrokes,
        Button penaltyPlus,
        Button addAnotherShot,
        Button undoLastShot,
        Label sg)
    {
        var title = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };
        title.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenShotTitle));

        return Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                title,
                completedShots,
                undoLastShot,
                ShotPositionPanel(
                    "Start",
                    "Til flaget",
                    startLie,
                    startDistance,
                    startDistanceValue,
                    startDistanceMinus,
                    startDistancePlus,
                    startDistanceTextBindingPath,
                    startQuickPicks,
                    selectStartDistance),
                ShotPathDivider(),
                ShotPositionPanel("Slut", endLie, endDistancePanel),
                ToggleRow("I hul", holed),
                CounterPanel("Strafslag", penaltyMinus, penaltyStrokes, penaltyPlus),
                addAnotherShot,
                sg
            }
        });
    }

    private static View CounterPanel(string title, Button minus, Label count, Button plus)
    {
        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 15,
                    TextColor = MutedTextColor,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new HorizontalStackLayout
                {
                    Spacing = 18,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { minus, count, plus }
                }
            }
        };
    }

    private static View DistancePanel(
        string title,
        Slider distance,
        Label distanceValue,
        Button minus,
        Button plus,
        string distanceTextBindingPath,
        IReadOnlyList<DistanceQuickPick> quickPicks,
        Action<double> selectDistance,
        string? unitTextBindingPath = null) =>
        DistancePanel(
            title,
            distance,
            distanceValue,
            minus,
            plus,
            distanceTextBindingPath,
            quickPicks.Count == 0
                ? []
                : [new DistanceQuickPickGroup(quickPicks, null)],
            selectDistance,
            unitTextBindingPath);

    private static View DistancePanel(
        string title,
        Slider distance,
        Label distanceValue,
        Button minus,
        Button plus,
        string distanceTextBindingPath,
        IReadOnlyList<DistanceQuickPickGroup> quickPickGroups,
        Action<double> selectDistance,
        string? unitTextBindingPath = null)
    {
        var input = new Entry
        {
            Keyboard = Keyboard.Numeric,
            TextColor = TextColor,
            BackgroundColor = Colors.Transparent,
            HorizontalTextAlignment = TextAlignment.End,
            FontSize = 16,
            WidthRequest = 70,
            HeightRequest = 36,
            ReturnType = ReturnType.Done,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        input.SetBinding(Entry.TextProperty, distanceTextBindingPath, BindingMode.TwoWay);

        var unit = new Label
        {
            Text = "m",
            FontSize = 13,
            TextColor = MutedTextColor,
            VerticalTextAlignment = TextAlignment.Center
        };
        if (unitTextBindingPath is not null)
        {
            unit.SetBinding(Label.TextProperty, unitTextBindingPath);
        }

        var children = new VerticalStackLayout
        {
            Spacing = 8
        };

        children.Children.Add(new Border
        {
            BackgroundColor = InputBackground,
            Stroke = CardStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(10, 8),
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 13,
                        TextColor = MutedTextColor,
                        LineBreakMode = LineBreakMode.NoWrap
                    },
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        ColumnSpacing = 8,
                        Children =
                        {
                            distanceValue.Column(0),
                            input.Column(1),
                            unit.Column(2),
                            minus.Column(3),
                            plus.Column(4)
                        }
                    }
                }
            }
        });

        foreach (var group in quickPickGroups)
        {
            if (group.QuickPicks.Count == 0)
            {
                continue;
            }

            var actions = DistanceQuickActions(group.QuickPicks, selectDistance);
            if (!string.IsNullOrWhiteSpace(group.IsVisibleBindingPath))
            {
                actions.SetBinding(IsVisibleProperty, group.IsVisibleBindingPath);
            }

            children.Children.Add(actions);
        }

        children.Children.Add(distance);

        return children;
    }

    private sealed record DistanceQuickPickGroup(
        IReadOnlyList<DistanceQuickPick> QuickPicks,
        string? IsVisibleBindingPath);

    private static FlexLayout DistanceQuickActions(
        IReadOnlyList<DistanceQuickPick> quickPicks,
        Action<double> selectDistance)
    {
        var actions = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start
        };

        foreach (var quickPick in quickPicks)
        {
            var button = new Button
            {
                Text = quickPick.Text,
                HeightRequest = 32,
                MinimumWidthRequest = 54,
                CornerRadius = 8,
                BackgroundColor = InputBackground,
                BorderColor = CardStroke,
                BorderWidth = 1,
                TextColor = TextColor,
                FontAttributes = FontAttributes.Bold,
                FontSize = 11,
                Padding = new Thickness(8, 0),
                Margin = new Thickness(0, 0, 6, 6)
            };
            button.Clicked += (_, _) => selectDistance(quickPick.Meters);
            actions.Children.Add(button);
        }

        return actions;
    }

    private static View ShotPositionPanel(
        string title,
        string distanceTitle,
        View lie,
        Slider distance,
        Label distanceValue,
        Button minus,
        Button plus,
        string distanceTextBindingPath,
        IReadOnlyList<DistanceQuickPick> quickPicks,
        Action<double> selectDistance,
        string? unitTextBindingPath = null) =>
        ShotPositionPanel(
            title,
            lie,
            DistancePanel(
                distanceTitle,
                distance,
                distanceValue,
                minus,
                plus,
                distanceTextBindingPath,
                quickPicks,
                selectDistance,
                unitTextBindingPath));

    private static View ShotPositionPanel(string title, View lie, View? distancePanel)
    {
        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                lie
            }
        };

        if (distancePanel is not null)
        {
            content.Children.Add(distancePanel);
        }

        return new Border
        {
            BackgroundColor = SoftGreen,
            Stroke = CardStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(12),
            Content = content
        };
    }

    private static View ShotPathDivider()
    {
        return new Grid
        {
            HeightRequest = 24,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Children =
            {
                new BoxView
                {
                    HeightRequest = 1,
                    Color = CardStroke,
                    VerticalOptions = LayoutOptions.Center
                }.Column(0),
                new Label
                {
                    Text = "->",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = PrimaryGreen,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    WidthRequest = 34
                }.Column(1),
                new BoxView
                {
                    HeightRequest = 1,
                    Color = CardStroke,
                    VerticalOptions = LayoutOptions.Center
                }.Column(2)
            }
        };
    }

    private static View QuickChoicePanel(
        string title,
        string selectedTextProperty,
        params (string Text, Action Action)[] choices)
    {
        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 15,
                    TextColor = MutedTextColor
                },
                QuickActions(selectedTextProperty, choices)
            }
        };
    }

    private static FlexLayout QuickActions(params (string Text, Action Action)[] choices) =>
        QuickActions(null, choices);

    private static FlexLayout QuickActions(string? selectedTextProperty, params (string Text, Action Action)[] choices)
    {
        var actions = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start
        };

        foreach (var choice in choices)
        {
            var button = new Button
            {
                Text = choice.Text,
                HeightRequest = 36,
                MinimumWidthRequest = 72,
                CornerRadius = 8,
                BackgroundColor = InputBackground,
                BorderColor = CardStroke,
                BorderWidth = 1,
                TextColor = TextColor,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                Padding = new Thickness(10, 0),
                Margin = new Thickness(0, 0, 6, 6)
            };

            if (!string.IsNullOrWhiteSpace(selectedTextProperty))
            {
                button.Triggers.Add(new DataTrigger(typeof(Button))
                {
                    Binding = new Binding(selectedTextProperty),
                    Value = choice.Text,
                    Setters =
                    {
                        new Setter { Property = Button.BackgroundColorProperty, Value = PrimaryGreen },
                        new Setter { Property = Button.BorderColorProperty, Value = PrimaryGreen },
                        new Setter { Property = Button.TextColorProperty, Value = Colors.White }
                    }
                });
            }

            button.Clicked += (_, _) => choice.Action();
            actions.Children.Add(button);
        }

        return actions;
    }

    private static FlexLayout NumberQuickActions(
        string selectedNumberProperty,
        params (string Text, int Value, Action Action)[] choices)
    {
        var actions = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start
        };

        foreach (var choice in choices)
        {
            var button = QuickActionButton(choice.Text);
            button.Triggers.Add(new DataTrigger(typeof(Button))
            {
                Binding = new Binding(selectedNumberProperty),
                Value = choice.Value,
                Setters =
                {
                    new Setter { Property = Button.BackgroundColorProperty, Value = PrimaryGreen },
                    new Setter { Property = Button.BorderColorProperty, Value = PrimaryGreen },
                    new Setter { Property = Button.TextColorProperty, Value = Colors.White }
                }
            });
            button.Clicked += (_, _) => choice.Action();
            actions.Children.Add(button);
        }

        return actions;
    }

    private static Button QuickActionButton(string text)
    {
        return new Button
        {
            Text = text,
            HeightRequest = 36,
            MinimumWidthRequest = 72,
            CornerRadius = 8,
            BackgroundColor = InputBackground,
            BorderColor = CardStroke,
            BorderWidth = 1,
            TextColor = TextColor,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12,
            Padding = new Thickness(10, 0),
            Margin = new Thickness(0, 0, 6, 6)
        };
    }

    private static View PickerPanel(string title, string itemsSourceProperty, string selectedItemProperty)
    {
        var picker = new Picker
        {
            Title = title,
            TextColor = TextColor
        };
        picker.SetBinding(Picker.ItemsSourceProperty, itemsSourceProperty);
        picker.SetBinding(Picker.SelectedItemProperty, selectedItemProperty, BindingMode.TwoWay);

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 15,
                    TextColor = MutedTextColor
                },
                new Border
                {
                    BackgroundColor = InputBackground,
                    Stroke = CardStroke,
                    StrokeThickness = 2,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(12, 2),
                    Content = picker
                }
            }
        };
    }

    private static View ToggleRow(string title, Switch toggle)
    {
        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor,
                    VerticalTextAlignment = TextAlignment.Center
                }.Column(0),
                toggle.Column(1)
            }
        };
    }

    private static Slider DistanceSlider(double maximum, IReadOnlyList<double> snapIntervals)
    {
        _ = snapIntervals;
        return new Slider
        {
            Minimum = 0,
            Maximum = maximum,
            MinimumTrackColor = PrimaryGreen,
            MaximumTrackColor = CardStroke,
            ThumbColor = PrimaryGreen
        };
    }

    private static Slider ExpandingDistanceSlider(
        double initialMaximum,
        double absoluteMaximum,
        IReadOnlyList<double> snapIntervals)
    {
        var slider = DistanceSlider(initialMaximum, snapIntervals);
        var nextExpansionAllowedAt = DateTime.MinValue;

        slider.ValueChanged += (_, args) =>
        {
            if (args.NewValue < slider.Maximum || slider.Maximum >= absoluteMaximum ||
                DateTime.UtcNow < nextExpansionAllowedAt)
            {
                return;
            }

            slider.Maximum = Math.Min(absoluteMaximum, slider.Maximum + initialMaximum);
            nextExpansionAllowedAt = DateTime.UtcNow.AddMilliseconds(900);
        };

        return slider;
    }

    private static Label DistanceValueLabel()
    {
        return new Label
        {
            Text = "-",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 70,
            HeightRequest = 36
        };
    }

    private static Button DistanceStepperButton(string text)
    {
        return new Button
        {
            Text = text,
            WidthRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            Padding = 0
        };
    }

    private static Label CountLabel()
    {
        return new Label
        {
            FontSize = 44,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 96,
            HeightRequest = 64
        };
    }

    private static Label SgLabel()
    {
        return new Label
        {
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center
        };
    }

    private static Button RoundStepperButton(string text)
    {
        return new Button
        {
            Text = text,
            WidthRequest = 64,
            HeightRequest = 64,
            CornerRadius = 32,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 28,
            Padding = 0
        };
    }

    private static Border Card(View content)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 16,
            Content = content
        };
    }
}
