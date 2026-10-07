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
    private const double MaxApproachDistanceMeters = 300;
    private const double MaxAroundGreenDistanceMeters = 50;
    private const double MaxFinishDistanceMeters = 300;


    private static readonly IReadOnlyList<DistanceQuickPick> ApproachQuickPicks =
        SgDistanceInputPresets.ToQuickPicks(SgDistanceInputPresets.ApproachMeters, MaxApproachDistanceMeters, 0);

    private static readonly IReadOnlyList<DistanceQuickPick> AroundGreenQuickPicks =
        SgDistanceInputPresets.ToQuickPicks(SgDistanceInputPresets.AroundGreenMeters, MaxAroundGreenDistanceMeters, 0);

    private static readonly IReadOnlyList<DistanceQuickPick> OffGreenFinishQuickPicks =
        SgDistanceInputPresets.ToQuickPicks(SgDistanceInputPresets.OffGreenFinishMeters, MaxFinishDistanceMeters, 0);

    private readonly RoundInputViewModel roundViewModel;
    private HoleInputViewModel viewModel;
    private Button? nextHoleButton;
    private Button? overviewButton;
    private ScrollView? holeScrollView;
    private Action? openFirstVisibleInputSection;
    private Func<HoleEntryStep, Task>? openNextInputPaneAsync;
    private (bool Putting, bool Approach, bool AroundGreen) layoutTracking;
    private bool useGuidedInput;
    private PuttingDistanceUnitPreference layoutDistanceUnit;
    private bool layoutRecordGir;
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
        UsageDiagnostics.BeginVisit(viewModel);
        BindingContext = viewModel;
        Title = viewModel.Title;
        BackgroundColor = PageBackground;
        HideSoftInputOnTapped = true;
        Shell.SetBackgroundColor(this, PageBackground);
        Shell.SetForegroundColor(this, PrimaryGreen);
        Shell.SetTitleColor(this, PrimaryGreen);
        this.Accessible(UiAutomationIds.HoleEntryPage, $"Input for hul {viewModel.HoleNumber}");
        BuildLayout();
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "⚙",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            Command = new Command(async () => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new RoundSettingsPage(roundViewModel, this.viewModel.HoleNumber))))
        });
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            IsVisible = true,
            IsEnabled = true,
            Command = new Command(async () => await NavigateBackFromHeaderAsync())
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
        if (layoutTracking != (viewModel.TrackPutting, viewModel.TrackApproach, viewModel.TrackAroundGreen) ||
            useGuidedInput != FeatureSettings.UseGuidedHoleEntry ||
            layoutDistanceUnit != viewModel.PuttingDistanceUnitPreference ||
            layoutRecordGir != FeatureSettings.RecordGreenInRegulation)
        {
            var previousStep = activeStep;
            useGuidedInput = FeatureSettings.UseGuidedHoleEntry;
            BuildLayout();
            activeStep = previousStep;
        }
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

        _ = GetPreviousHole() is { } previousHole
            ? GoToHoleAsync(previousHole)
            : GoToOverviewAsync();
        return true;
    }

    private void BuildLayout()
    {
        layoutTracking = (viewModel.TrackPutting, viewModel.TrackApproach, viewModel.TrackAroundGreen);
        layoutDistanceUnit = viewModel.PuttingDistanceUnitPreference;
        layoutRecordGir = FeatureSettings.RecordGreenInRegulation;
        var nextHole = GetNextHole();
        var puttingQuickPickDistances = SgDistanceInputPresets.GetPuttingQuickPickMeters(
            viewModel.PuttingDistanceUnitPreference);
        var puttingQuickPicks = SgDistanceInputPresets.ToQuickPicks(
            puttingQuickPickDistances,
            MaxFirstPuttDistanceMeters,
            1,
            viewModel.PuttingDistanceUnitPreference);

        var puttingSection = BuildPuttingSection(puttingQuickPicks);
        var approachSection = BuildApproachSection(puttingQuickPicks);
        var aroundGreenSection = BuildAroundGreenSection(puttingQuickPicks);

        approachSectionView = approachSection;
        aroundGreenSectionView = aroundGreenSection;
        puttingSectionView = puttingSection;

        var done = new Button
        {
            Text = "Til oversigt",
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold
        };
        done.Accessible(UiAutomationIds.CompleteHole, "Gem input for hullet og gå til oversigten");
        done.Clicked += async (_, _) => await GoToOverviewAsync();
        overviewButton = done;
        done.IsVisible = nextHole is not null;

        var next = new Button
        {
            Text = nextHole is null ? "Til oversigt" : "Næste hul",
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold
        };
        next.Accessible(UiAutomationIds.NextHole, "Gem input og gå til næste hul");
        nextHoleButton = next;
        next.Clicked += async (_, _) => await GoToNextHoleOrOverviewAsync();
        Grid.SetColumn(next, nextHole is null ? 0 : 1);
        Grid.SetColumnSpan(next, nextHole is null ? 2 : 1);

        if (useGuidedInput)
        {
            BuildGuidedContent(approachSection, aroundGreenSection, puttingSection);
            AttachUsageCounters(this);
            return;
        }

        Shell.SetTitleView(this, HoleHeader());
        Content = new Grid
        {
            Padding = new Thickness(20, 8, 20, 20),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                (holeScrollView = new ScrollView
                {
                    Content = CollapsibleInputSections(approachSection, aroundGreenSection, puttingSection)
                }).Row(0).Margin(new Thickness(0, 0, 0, 12)),
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
                        next
                    }
                }.Row(1)
            }
        };

        AttachUsageCounters(this);
    }

    private void BuildGuidedContent(
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
            VerticalOptions = LayoutOptions.Center
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
        guidedBackButton.Accessible(UiAutomationIds.GuidedBack, "Gå til forrige inputtrin");
        guidedBackButton.Clicked += async (_, _) => await GoBackInGuidedFlowAsync();

        guidedNextButton = new Button
        {
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold
        };
        guidedNextButton.Accessible(UiAutomationIds.GuidedNext, "Gå til næste inputtrin");
        guidedNextButton.Clicked += async (_, _) => await GoForwardInGuidedFlowAsync();

        UpdateGuidedStepVisibility();

        Shell.SetTitleView(this, HoleHeader());
        Content = new Grid
        {
            Padding = new Thickness(20, 8, 20, 20),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                (holeScrollView = new ScrollView
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
                }).Row(0).Margin(new Thickness(0, 0, 0, 12)),
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
                }.Row(1)
            }
        };
    }

    private View HoleHeader()
    {
        var closeRound = new Button
        {
            Text = "Luk runde",
            HeightRequest = 44,
            FontSize = 13,
            Padding = new Thickness(10, 0),
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0,
            TextColor = PrimaryGreen
        };
        closeRound.Accessible("hole-entry.close-round", "Luk runde", "Spørger om bekræftelse før runden lukkes");
        closeRound.Clicked += async (_, _) => await this.RunActionOnceAsync(ConfirmCloseRoundAsync);
        var title = new Button
        {
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = PrimaryGreen,
            BackgroundColor = Colors.Transparent,
            Padding = 0,
            HeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        };
        title.SetBinding(Button.TextProperty, new Binding(nameof(Title), source: this, stringFormat: "{0} ▾"));
        title.Accessible("hole-entry.choose-hole", "Vælg hul", "Åbn hulnumrene for at springe til et andet hul");
        title.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() =>
            Navigation.PushModalAsync(new HolePickerPage(roundViewModel.Holes, viewModel.HoleNumber, async hole =>
            {
                if (hole != viewModel)
                {
                    await GoToHoleAsync(hole);
                }
            })));
        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Children =
            {
                title.Column(0),
                closeRound.Column(1)
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
