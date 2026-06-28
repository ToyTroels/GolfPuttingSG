using GolfSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace GolfSG.Views;

public sealed class HoleEntryPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color InputBackground = Color.FromArgb("#FAFBFA");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color SoftGreen = Color.FromArgb("#EEF5EF");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly RoundInputViewModel roundViewModel;
    private readonly HoleInputViewModel viewModel;

    public HoleEntryPage(RoundInputViewModel roundViewModel, HoleInputViewModel viewModel)
    {
        this.roundViewModel = roundViewModel;
        this.viewModel = viewModel;
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

    protected override bool OnBackButtonPressed()
    {
        _ = ConfirmCloseRoundAsync();
        return true;
    }

    private void BuildLayout()
    {
        var previousHole = GetPreviousHole();
        var nextHole = GetNextHole();

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

        var puttingDistance = DistanceSlider(30);
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
            ("1 putt", 1, () => viewModel.SetPutts(1)),
            ("2 putts", 2, () => viewModel.SetPutts(2)),
            ("3 putts", 3, () => viewModel.SetPutts(3)));

        var puttingSg = SgLabel();
        puttingSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedPuttingText), stringFormat: "SG Putting {0}"));

        var approachDistance = DistanceSlider(250);
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

        var approachStartDistance = DistanceSlider(250);
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

        var approachEndDistance = ExpandingDistanceSlider(30, 250);
        approachEndDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.ApproachEndDistance), BindingMode.TwoWay);

        var approachEndDistanceValue = DistanceValueLabel();
        approachEndDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachEndDistanceDisplayText));

        var approachEndDistanceMinus = DistanceStepperButton("-");
        approachEndDistanceMinus.Clicked += (_, _) => viewModel.DecreaseApproachEndDistance();

        var approachEndDistancePlus = DistanceStepperButton("+");
        approachEndDistancePlus.Clicked += (_, _) => viewModel.IncreaseApproachEndDistance();

        var approachEndDistancePanel = DistancePanel("Slutafstand", approachEndDistance, approachEndDistanceValue, approachEndDistanceMinus, approachEndDistancePlus);
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

        var aroundGreenStartDistance = DistanceSlider(50);
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

        var aroundGreenEndDistance = ExpandingDistanceSlider(30, 250);
        aroundGreenEndDistance.SetBinding(Slider.ValueProperty, nameof(HoleInputViewModel.AroundGreenEndDistance), BindingMode.TwoWay);
        aroundGreenEndDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenFinishDistanceVisible));

        var aroundGreenEndDistanceValue = DistanceValueLabel();
        aroundGreenEndDistanceValue.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.AroundGreenEndDistanceDisplayText));

        var aroundGreenEndDistanceMinus = DistanceStepperButton("-");
        aroundGreenEndDistanceMinus.Clicked += (_, _) => viewModel.DecreaseAroundGreenEndDistance();

        var aroundGreenEndDistancePlus = DistanceStepperButton("+");
        aroundGreenEndDistancePlus.Clicked += (_, _) => viewModel.IncreaseAroundGreenEndDistance();

        var aroundGreenEndDistancePanel = DistancePanel("Slutafstand", aroundGreenEndDistance, aroundGreenEndDistanceValue, aroundGreenEndDistanceMinus, aroundGreenEndDistancePlus);
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

        var puttingSection = PuttingSection(
            puttMinus,
            putts,
            puttPlus,
            puttQuickActions,
            puttingDistance,
            puttingDistanceValue,
            puttingDistanceMinus,
            puttingDistancePlus,
            puttingSg);
        puttingSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsPuttingInputVisible));

        var approachSection = ApproachSection(
            approachStartLie,
            approachStartDistance,
            approachStartDistanceValue,
            approachStartDistanceMinus,
            approachStartDistancePlus,
            approachEndLie,
            approachEndDistancePanel,
            approachHoled,
            approachPenaltyMinus,
            approachPenaltyStrokes,
            approachPenaltyPlus,
            approachSg);
        approachSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsApproachInputVisible));

        var aroundGreenSection = AroundGreenSection(
            aroundGreenStartLie,
            aroundGreenStartDistance,
            aroundGreenStartDistanceValue,
            aroundGreenStartDistanceMinus,
            aroundGreenStartDistancePlus,
            aroundGreenEndLie,
            aroundGreenEndDistancePanel,
            aroundGreenHoled,
            aroundGreenPenaltyMinus,
            aroundGreenPenaltyStrokes,
            aroundGreenPenaltyPlus,
            addAroundGreenShot,
            aroundGreenSg);
        aroundGreenSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenInputVisible));

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

        var page = new HoleEntryPage(roundViewModel, hole);
        Navigation.InsertPageBefore(page, this);
        await Navigation.PopAsync(animated: false);
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
        Label sg)
    {
        return Card(new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                CounterPanel("Antal putts", minus, putts, plus),
                quickActions,
                DistancePanel("Første putt-afstand", distance, distanceValue, distanceMinus, distancePlus),
                sg
            }
        });
    }

    private static View ApproachSection(
        View startLie,
        Slider startDistance,
        Label startDistanceValue,
        Button startDistanceMinus,
        Button startDistancePlus,
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
                ShotPositionPanel("Start", "Til flaget", startLie, startDistance, startDistanceValue, startDistanceMinus, startDistancePlus),
                ShotPathDivider(),
                ShotPositionPanel("Finish", endLie, endDistancePanel),
                ToggleRow("I hul", holed),
                CounterPanel("Strafslag", penaltyMinus, penaltyStrokes, penaltyPlus),
                sg
            }
        });
    }

    private static View AroundGreenSection(
        View startLie,
        Slider startDistance,
        Label startDistanceValue,
        Button startDistanceMinus,
        Button startDistancePlus,
        View endLie,
        View endDistancePanel,
        Switch holed,
        Button penaltyMinus,
        Label penaltyStrokes,
        Button penaltyPlus,
        Button addAnotherShot,
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
                ShotPositionPanel("Start", "Til flaget", startLie, startDistance, startDistanceValue, startDistanceMinus, startDistancePlus),
                ShotPathDivider(),
                ShotPositionPanel("Finish", endLie, endDistancePanel),
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

    private static View DistancePanel(string title, Slider distance, Label distanceValue, Button minus, Button plus)
    {
        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Border
                {
                    BackgroundColor = InputBackground,
                    Stroke = CardStroke,
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(10, 8),
                    Content = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        ColumnSpacing = 8,
                        Children =
                        {
                            new Label
                            {
                                Text = title,
                                FontSize = 13,
                                TextColor = MutedTextColor,
                                VerticalTextAlignment = TextAlignment.Center
                            }.Column(0),
                            distanceValue.Column(1),
                            minus.Column(2),
                            plus.Column(3)
                        }
                    }
                },
                distance
            }
        };
    }

    private static View ShotPositionPanel(
        string title,
        string distanceTitle,
        View lie,
        Slider distance,
        Label distanceValue,
        Button minus,
        Button plus) =>
        ShotPositionPanel(title, lie, DistancePanel(distanceTitle, distance, distanceValue, minus, plus));

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

    private static Slider DistanceSlider(double maximum)
    {
        return new Slider
        {
            Minimum = 0,
            Maximum = maximum,
            MinimumTrackColor = PrimaryGreen,
            MaximumTrackColor = CardStroke,
            ThumbColor = PrimaryGreen
        };
    }

    private static Slider ExpandingDistanceSlider(double initialMaximum, double absoluteMaximum)
    {
        var slider = DistanceSlider(initialMaximum);
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
