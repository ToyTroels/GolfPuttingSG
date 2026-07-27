using GolfSG.Application.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace GolfSG.Views;

public sealed partial class HoleEntryPage
{
    private static View PuttingSection(
        Button minus,
        Label putts,
        Button plus,
        View quickActions,
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
            null,
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
        Slider? distance,
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
        Slider? distance,
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

        if (distance is not null)
        {
            children.Children.Add(distance);
        }

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
