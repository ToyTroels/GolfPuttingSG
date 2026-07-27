using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using System.ComponentModel;

namespace GolfSG.Views;

public sealed partial class HoleEntryPage : ContentPage
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
            Command = new Command(async () => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new RoundSettingsPage(roundViewModel, viewModel.HoleNumber))))
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
        var puttingQuickPickDistances = SgDistanceInputPresets.GetPuttingQuickPickMeters(
            viewModel.PuttingDistanceUnitPreference);
        var puttingQuickPicks = SgDistanceInputPresets.ToQuickPicks(
            puttingQuickPickDistances,
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
        done.Clicked += async (_, _) =>
        {
            await roundViewModel.FlushAutosaveAsync();
            await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        };

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
        var propertyName = args.PropertyName;
        var affectsVisibility = string.IsNullOrEmpty(propertyName) ||
            propertyName == nameof(HoleInputViewModel.IsApproachInputVisible) ||
            propertyName == nameof(HoleInputViewModel.IsAroundGreenInputVisible) ||
            propertyName == nameof(HoleInputViewModel.IsPuttingInputVisible);
        var affectsReadiness = string.IsNullOrEmpty(propertyName) ||
            propertyName == nameof(HoleInputViewModel.CanAdvanceApproachStep) ||
            propertyName == nameof(HoleInputViewModel.CanAdvanceAroundGreenStep) ||
            propertyName == nameof(HoleInputViewModel.CanAdvancePuttingStep);

        if (!affectsVisibility && !affectsReadiness)
        {
            return;
        }

        var previousStep = activeStep;
        if (affectsVisibility)
        {
            UpdateGuidedStepVisibility();
        }

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
}
