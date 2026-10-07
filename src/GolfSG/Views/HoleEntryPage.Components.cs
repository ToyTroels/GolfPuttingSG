using GolfSG.Application.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace GolfSG.Views;

public sealed partial class HoleEntryPage
{
    private View CollapsibleInputSections(View approach, View aroundGreen, View putting)
    {
        var sections = new List<(View Content, Button Header, string Title, string VisibilityProperty)>();
        var layout = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(0, 0, 0, 20)
        };

        void SetExpanded((View Content, Button Header, string Title, string VisibilityProperty) section, bool expanded)
        {
            section.Content.IsVisible = expanded;
            section.Header.Text = $"{section.Title}  {(expanded ? "▾" : "▸")}";
            SemanticProperties.SetDescription(section.Header,
                $"{section.Title}. {(expanded ? "Skjul indtastning" : "Vis indtastning")}");
        }

        void AddSection(string title, View content, bool tracked, string scoreProperty, string visibilityProperty)
        {
            if (!tracked)
            {
                return;
            }

            if (GolfSG.Application.Services.FeatureSettings.EnableBetaFeatures &&
                visibilityProperty != nameof(HoleInputViewModel.IsPuttingInputVisible))
            {
                title += " · Beta";
            }

            var header = new Button
            {
                BackgroundColor = GolfTheme.Colors.SoftGreen,
                TextColor = TextColor,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 52,
                CornerRadius = 8,
                Padding = new Thickness(12, 0)
            };
            var score = new Label
            {
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = PrimaryGreen,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalTextAlignment = TextAlignment.End
            };
            score.SetBinding(Label.TextProperty, scoreProperty);
            content.RemoveBinding(VisualElement.IsVisibleProperty);
            var section = (Content: content, Header: header, Title: title, VisibilityProperty: visibilityProperty);
            SetExpanded(section, sections.Count == 0);
            sections.Add(section);
            header.Clicked += async (_, _) =>
            {
                CancelGuidedAutoAdvance();
                var expand = !content.IsVisible;
                foreach (var other in sections)
                {
                    SetExpanded(other, expand && other.Content == content);
                }
                await Task.Yield();
                if (holeScrollView?.Handler is not null)
                {
                    await holeScrollView.ScrollToAsync(header, ScrollToPosition.Start, animated: false);
                }
            };
            var wrapper = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Grid
                    {
                        ColumnSpacing = 12,
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Children = { header.Column(0), score.Column(1) }
                    },
                    content
                }
            };
            wrapper.SetBinding(IsVisibleProperty, visibilityProperty);
            layout.Children.Add(wrapper);
        }

        AddSection("Approach", approach, viewModel.TrackApproach, nameof(HoleInputViewModel.StrokesGainedApproachText), nameof(HoleInputViewModel.IsApproachInputVisible));
        AddSection("Omkring green", aroundGreen, viewModel.TrackAroundGreen, nameof(HoleInputViewModel.StrokesGainedAroundGreenText), nameof(HoleInputViewModel.IsAroundGreenInputVisible));
        AddSection("Putting", putting, viewModel.TrackPutting, nameof(HoleInputViewModel.StrokesGainedPuttingText), nameof(HoleInputViewModel.IsPuttingInputVisible));
        openFirstVisibleInputSection = () =>
        {
            var opened = false;
            foreach (var section in sections)
            {
                var visible = section.VisibilityProperty switch
                {
                    nameof(HoleInputViewModel.IsApproachInputVisible) => viewModel.IsApproachInputVisible,
                    nameof(HoleInputViewModel.IsAroundGreenInputVisible) => viewModel.IsAroundGreenInputVisible,
                    nameof(HoleInputViewModel.IsPuttingInputVisible) => viewModel.IsPuttingInputVisible,
                    _ => false
                };
                var expand = !opened && visible;
                SetExpanded(section, expand);
                opened |= expand;
            }
        };
        openFirstVisibleInputSection();
        openNextInputPaneAsync = async currentStep =>
        {
            var nextProperty = currentStep == HoleEntryStep.Approach && viewModel.IsAroundGreenInputVisible
                ? nameof(HoleInputViewModel.IsAroundGreenInputVisible)
                : viewModel.IsPuttingInputVisible ? nameof(HoleInputViewModel.IsPuttingInputVisible) : null;
            if (nextProperty is null) return;
            var nextSection = sections.FirstOrDefault(section => section.VisibilityProperty == nextProperty);
            if (nextSection.Content is null) return;
            foreach (var section in sections)
            {
                SetExpanded(section, section.Content == nextSection.Content);
            }
            await Task.Yield();
            if (holeScrollView?.Handler is not null)
            {
                await holeScrollView.ScrollToAsync(nextSection.Header, ScrollToPosition.Start, animated: true);
            }
        };
        return layout;
    }

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

        var startEditor = ShotPositionPanel(
            "Start", "Til flaget", startLie, startDistance, startDistanceValue,
            startDistanceMinus, startDistancePlus, startDistanceTextBindingPath,
            startQuickPicks, selectStartDistance);
        var startToggle = new Button
        {
            BackgroundColor = GolfTheme.Colors.SoftGreen,
            TextColor = TextColor,
            FontSize = 15,
            CornerRadius = 8,
            MinimumHeightRequest = 44,
            HorizontalOptions = LayoutOptions.Fill
        };
        var startPanel = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { startToggle, startEditor }
        };
        HoleInputViewModel? startModel = null;
        var editingStart = false;

        void RefreshStart(bool reset)
        {
            if (reset) editingStart = startModel?.AroundGreenStartDistanceYards is not > 0;
            var configured = startModel?.AroundGreenStartDistanceYards > 0;
            startEditor.IsVisible = !configured || editingStart;
            startToggle.Text = configured
                ? $"Start: {startModel!.AroundGreenStartLieText} · {startModel.AroundGreenStartDistanceDisplayText}  {(startEditor.IsVisible ? "▾" : "▸ Ret") }"
                : "Startposition";
            SemanticProperties.SetDescription(startToggle,
                $"{startToggle.Text}. {(startEditor.IsVisible ? "Skjul startposition" : "Rediger startposition")}");
        }

        void OnStartChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            var reset = args.PropertyName == nameof(HoleInputViewModel.AroundGreenShotTitle) ||
                (args.PropertyName == nameof(HoleInputViewModel.IsAroundGreenStartCarriedFromApproach) &&
                 startModel?.IsAroundGreenStartCarriedFromApproach == true);
            if (reset || args.PropertyName is nameof(HoleInputViewModel.AroundGreenStartLieText)
                or nameof(HoleInputViewModel.AroundGreenStartDistanceDisplayText))
            {
                RefreshStart(reset);
            }
        }

        startPanel.BindingContextChanged += (_, _) =>
        {
            if (startModel is not null) startModel.PropertyChanged -= OnStartChanged;
            startModel = startPanel.BindingContext as HoleInputViewModel;
            if (startModel is not null) startModel.PropertyChanged += OnStartChanged;
            RefreshStart(reset: true);
        };
        startToggle.Clicked += (_, _) =>
        {
            editingStart = !editingStart;
            RefreshStart(reset: false);
        };

        return Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                title,
                completedShots,
                undoLastShot,
                startPanel,
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
        input.Completed += async (_, _) =>
        {
            input.Unfocus();
            if (distanceTextBindingPath is nameof(HoleInputViewModel.ApproachEndDistanceText)
                or nameof(HoleInputViewModel.AroundGreenEndDistanceText))
            {
                Element? ancestor = input;
                while (ancestor is not null && ancestor is not HoleEntryPage) ancestor = ancestor.Parent;
                if (ancestor is HoleEntryPage page)
                {
                    if (distanceTextBindingPath == nameof(HoleInputViewModel.ApproachEndDistanceText))
                        await page.AdvanceAfterApproachDistanceAsync();
                    else await page.AdvanceAfterAroundGreenDistanceAsync();
                }
            }
        };

        var isApproach = distanceTextBindingPath is nameof(HoleInputViewModel.ApproachStartDistanceText)
            or nameof(HoleInputViewModel.ApproachEndDistanceText)
            or nameof(HoleInputViewModel.ApproachEndDistanceToGreenEdgeText)
            or nameof(HoleInputViewModel.ApproachDistanceText)
            or nameof(HoleInputViewModel.AroundGreenStartDistanceText)
            or nameof(HoleInputViewModel.AroundGreenEndDistanceText);
        if (isApproach || distanceTextBindingPath == nameof(HoleInputViewModel.DistanceText))
        {
            distanceValue.TextColor = TextColor;
            distanceValue.MinimumHeightRequest = 44;
            SemanticProperties.SetDescription(distanceValue, $"{title}. Tryk for at indtaste afstand.");
            var editDistance = new TapGestureRecognizer();
            editDistance.Tapped += async (_, _) => await EditDistanceAsync(
                distanceValue, title, distanceTextBindingPath, selectDistance);
            distanceValue.GestureRecognizers.Add(editDistance);
        }

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
                    isApproach ? ApproachDistanceControls(distanceValue, input, distanceTextBindingPath, selectDistance) : new Grid
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

    private static View ApproachDistanceControls(
        Label value, Entry input, string bindingPath, Action<double> selectDistance)
    {
        value.ClearValue(WidthRequestProperty);
        value.HeightRequest = 48;
        value.FontSize = 18;
        value.HorizontalTextAlignment = TextAlignment.Center;
        value.LineBreakMode = LineBreakMode.NoWrap;
        input.ClearValue(WidthRequestProperty);
        input.HeightRequest = 48;
        input.HorizontalTextAlignment = TextAlignment.Center;
        input.IsVisible = false;
        if (bindingPath == nameof(HoleInputViewModel.ApproachEndDistanceText))
        {
            input.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsApproachEndOnGreen));
            value.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsApproachEndOffGreen));
        }
        else if (bindingPath == nameof(HoleInputViewModel.AroundGreenEndDistanceText))
        {
            input.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenEndOnGreen));
            value.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.IsAroundGreenEndOffGreen));
        }

        Button StepButton(int delta)
        {
            var button = new Button
            {
                Text = delta > 0 ? $"+{delta}" : $"−{Math.Abs(delta)}",
                WidthRequest = 40,
                HeightRequest = 48,
                MinimumWidthRequest = 40,
                CornerRadius = 8,
                Padding = 0,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = GolfTheme.Colors.Divider,
                BorderColor = CardStroke,
                BorderWidth = 1,
                TextColor = TextColor
            };
            SemanticProperties.SetDescription(button,
                $"{(delta > 0 ? "Øg" : "Reducer")} afstanden med {Math.Abs(delta)} meter");
            button.Clicked += (_, _) =>
            {
                if (value.BindingContext is not HoleInputViewModel model)
                {
                    return;
                }

                var current = bindingPath switch
                {
                    nameof(HoleInputViewModel.ApproachStartDistanceText) => model.ApproachStartDistanceYards,
                    nameof(HoleInputViewModel.ApproachEndDistanceText) => model.ApproachEndDistance,
                    nameof(HoleInputViewModel.ApproachEndDistanceToGreenEdgeText) => model.ApproachEndDistanceToGreenEdgeYards,
                    nameof(HoleInputViewModel.AroundGreenStartDistanceText) => model.AroundGreenStartDistanceYards,
                    nameof(HoleInputViewModel.AroundGreenEndDistanceText) => model.AroundGreenEndDistance,
                    _ => model.ApproachDistanceMeters
                };
                var maximum = bindingPath == nameof(HoleInputViewModel.AroundGreenStartDistanceText)
                    ? MaxAroundGreenDistanceMeters : MaxApproachDistanceMeters;
                selectDistance(Math.Clamp(current + delta, 0, maximum));
            };
            return button;
        }

        return new Grid
        {
            ColumnSpacing = 4,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            Children =
            {
                StepButton(-5).Column(0),
                StepButton(-1).Column(1),
                new Grid { Children = { value, input } }.Column(2),
                StepButton(1).Column(3),
                StepButton(5).Column(4)
            }
        };
    }

    private static async Task EditDistanceAsync(
        Label value, string title, string bindingPath, Action<double> selectDistance)
    {
        Element? ancestor = value;
        while (ancestor is not null && ancestor is not Page)
        {
            ancestor = ancestor.Parent;
        }

        if (ancestor is not Page page || value.BindingContext is not HoleInputViewModel model)
        {
            return;
        }

        await page.RunActionOnceAsync(async () =>
        {
            var text = bindingPath switch
            {
                nameof(HoleInputViewModel.DistanceText) => model.DistanceText,
                nameof(HoleInputViewModel.ApproachStartDistanceText) => model.ApproachStartDistanceText,
                nameof(HoleInputViewModel.ApproachEndDistanceText) => model.ApproachEndDistanceText,
                nameof(HoleInputViewModel.ApproachEndDistanceToGreenEdgeText) => model.ApproachEndDistanceToGreenEdgeText,
                nameof(HoleInputViewModel.AroundGreenStartDistanceText) => model.AroundGreenStartDistanceText,
                nameof(HoleInputViewModel.AroundGreenEndDistanceText) => model.AroundGreenEndDistanceText,
                _ => model.ApproachDistanceText
            };
            var unit = bindingPath switch
            {
                nameof(HoleInputViewModel.DistanceText) => model.PuttingDistanceUnitText,
                nameof(HoleInputViewModel.ApproachEndDistanceText) => model.ApproachEndDistanceUnitText,
                nameof(HoleInputViewModel.AroundGreenEndDistanceText) => model.AroundGreenEndDistanceUnitText,
                _ => "m"
            };
            var maximumMeters = bindingPath == nameof(HoleInputViewModel.DistanceText)
                ? MaxFirstPuttDistanceMeters
                : bindingPath == nameof(HoleInputViewModel.AroundGreenStartDistanceText)
                    ? MaxAroundGreenDistanceMeters : MaxApproachDistanceMeters;
            var maximum = unit == "ft"
                ? GolfSG.Core.DistanceConversions.MetersToFeet(maximumMeters)
                : maximumMeters;
            var message = $"Indtast afstand i {unit} (0–{Math.Floor(maximum):0} {unit}).";
            while (true)
            {
                var result = await ShowDistancePromptAsync(page, title, message, text);
                if (result is null)
                {
                    return;
                }

                if (DistanceInputParser.TryParse(result, out var entered) && entered >= 0 && entered <= maximum)
                {
                    selectDistance(unit == "ft" ? GolfSG.Core.DistanceConversions.FeetToMeters(entered) : entered);
                    if (bindingPath == nameof(HoleInputViewModel.ApproachEndDistanceText) && page is HoleEntryPage holePage)
                    {
                        await holePage.AdvanceAfterApproachDistanceAsync();
                    }
                    else if (bindingPath == nameof(HoleInputViewModel.AroundGreenEndDistanceText) && page is HoleEntryPage aroundGreenPage)
                    {
                        await aroundGreenPage.AdvanceAfterAroundGreenDistanceAsync();
                    }
                    return;
                }

                text = result;
                message = $"Indtast et tal mellem 0 og {Math.Floor(maximum):0} {unit}.";
            }
        });
    }

    private static async Task<string?> ShowDistancePromptAsync(Page page, string title, string message, string text)
    {
#if ANDROID
        var context = page.Handler?.MauiContext?.Context
            ?? throw new InvalidOperationException("Distance input requires an active page.");
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var input = new Android.Widget.EditText(context)
        {
            Text = text,
            TextSize = 24,
            InputType = Android.Text.InputTypes.ClassNumber | Android.Text.InputTypes.NumberFlagDecimal,
            ImeOptions = Android.Views.InputMethods.ImeAction.Done
        };
        input.SetSingleLine(true);
        input.SetSelectAllOnFocus(true);
        using var builder = new Android.App.AlertDialog.Builder(context);
        builder.SetTitle(title);
        builder.SetMessage(message);
        builder.SetView(input);
        builder.SetPositiveButton("Gem", (_, _) => completion.TrySetResult(input.Text));
        builder.SetNegativeButton("Annuller", (_, _) => completion.TrySetResult(null));
        using var dialog = builder.Create()
            ?? throw new InvalidOperationException("Distance dialog could not be created.");
        input.EditorAction += (_, args) =>
        {
            if (args.ActionId == Android.Views.InputMethods.ImeAction.Done)
            {
                args.Handled = true;
                completion.TrySetResult(input.Text);
                dialog.Dismiss();
            }
        };
        dialog.CancelEvent += (_, _) => completion.TrySetResult(null);
        dialog.DismissEvent += (_, _) => completion.TrySetResult(null);
        dialog.Window?.SetSoftInputMode(Android.Views.SoftInput.StateAlwaysVisible);
        dialog.Show();
        input.RequestFocus();
        input.SelectAll();
        return await completion.Task;
#else
        return await page.DisplayPromptAsync(title, message,
            accept: "Gem", cancel: "Annuller", keyboard: Keyboard.Numeric, initialValue: text);
#endif
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
