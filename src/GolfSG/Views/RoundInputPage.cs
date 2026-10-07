using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed partial class RoundInputPage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;

    private readonly RoundInputViewModel viewModel;
    private readonly List<View> betaTrackingRows = [];

    public RoundInputPage(RoundInputViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Runde";
        BackgroundColor = PageBackground;
        this.Accessible(UiAutomationIds.RoundPage, "Rundeopsætning og rundeoversigt");
        BuildLayout();
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "⚙",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            Command = new Command(async () => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new RoundSettingsPage(viewModel))))
        });
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(async () => await NavigateBackAsync())
        });
    }

    public async Task LoadAsync(string? roundId)
    {
        await viewModel.LoadAsync(roundId);
        if (viewModel.HasError)
        {
            await DisplayAlertAsync("Runden kunne ikke indl\u00e6ses", viewModel.ErrorMessage, "OK");
        }
    }

    public async Task<bool> LoadActiveAsync()
    {
        var loaded = await viewModel.LoadActiveAsync();
        if (!loaded && viewModel.HasError)
        {
            await DisplayAlertAsync("Runden kunne ikke indl\u00e6ses", viewModel.ErrorMessage, "OK");
        }

        return loaded;
    }

    public async Task OpenResumeHoleAsync()
    {
        var hole = viewModel.Holes.FirstOrDefault(hole => hole.HoleNumber == viewModel.ResumeHoleNumber);
        if (hole is not null)
        {
            await OpenHoleAsync(hole);
        }
    }
    public Task FlushActiveRoundAsync() => viewModel.FlushAutosaveAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        foreach (var row in betaTrackingRows)
        {
            row.IsVisible = FeatureSettings.EnableBetaFeatures;
        }

        if (viewModel.IsSetupVisible && !FeatureSettings.EnableBetaFeatures)
        {
            viewModel.TrackPutting = true;
            viewModel.TrackApproach = false;
            viewModel.TrackAroundGreen = false;
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = NavigateBackAsync();
        return true;
    }

    private void BuildLayout()
    {
        var title = new Label
        {
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };
        title.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.ScreenTitle));

        var holes = new VerticalStackLayout
        {
            Spacing = 0
        }.Accessible(UiAutomationIds.HoleList, "Rundens huller");
        BindableLayout.SetItemTemplate(holes, HoleTemplate());
        holes.SetBinding(BindableLayout.ItemsSourceProperty, nameof(RoundInputViewModel.Holes));

        var startButton = new Button
        {
            Text = "Start runde",
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold
        };
        startButton.Accessible(UiAutomationIds.StartRound, "Start runden med de valgte kategorier");
        startButton.Clicked += async (_, _) => await StartRoundAsync();

        var saveButton = new Button
        {
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        saveButton.Accessible(UiAutomationIds.SaveRound, "Gem eller afslut runden");
        saveButton.SetBinding(Button.TextProperty, nameof(RoundInputViewModel.SaveButtonText));
        saveButton.SetBinding(VisualElement.IsEnabledProperty, nameof(RoundInputViewModel.CanSave));
        saveButton.Clicked += async (_, _) => await SaveRoundAsync(saveButton);
        saveButton.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.IsRoundVisible));

        var error = new Border
        {
            BackgroundColor = GolfTheme.Colors.DangerBackground,
            Stroke = GolfTheme.Colors.DangerText,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 12,
            Margin = new Thickness(0, 0, 0, 10),
            Content = new Label
            {
                FontSize = 14,
                TextColor = GolfTheme.Colors.DangerText
            }
        };
        error.SetBinding(IsVisibleProperty, nameof(RoundInputViewModel.HasError));
        ((Label)error.Content).SetBinding(Label.TextProperty, nameof(RoundInputViewModel.ErrorMessage));

        var roundError = new Border
        {
            BackgroundColor = GolfTheme.Colors.DangerBackground,
            Stroke = GolfTheme.Colors.DangerText,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 12,
            Margin = new Thickness(0, 0, 0, 10),
            Content = new Label
            {
                FontSize = 14,
                TextColor = GolfTheme.Colors.DangerText
            }
        };
        roundError.SetBinding(IsVisibleProperty, nameof(RoundInputViewModel.HasError));
        ((Label)roundError.Content).SetBinding(Label.TextProperty, nameof(RoundInputViewModel.ErrorMessage));

        var setupView = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    title,
                    error,
                    HoleCountPanel(showQuickSelection: true).Margin(new Thickness(0, 12, 0, 0)),
                    TrackingPanel(),
                    startButton.Margin(new Thickness(0, 8, 0, 0))
                }
            }
        };
        setupView.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.IsSetupVisible));

        var roundView = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    new Label
                    {
                        Text = "Huller",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 2, 0, 10)
                    },
                    roundError,
                    HoleCountPanel(),
                    RoundProgressPanel(),
                    holes,
                    SummaryPanel()
                }
            }
        };
        roundView.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.IsRoundVisible));

        Content = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                setupView.Row(0),
                roundView.Row(0),
                saveButton.Row(1).Margin(new Thickness(0, 10, 0, 0))
            }
        };
    }


    private DataTemplate HoleTemplate()
    {
        return new DataTemplate(() =>
        {
            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                FontSize = 20,
                TextColor = TextColor
            };
            title.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Title));

            var subtitle = new Label
            {
                FontSize = 13,
                TextColor = MutedTextColor
            };
            subtitle.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Subtitle));

            var sg = new Label
            {
                FontAttributes = FontAttributes.Bold,
                FontSize = 16,
                TextColor = PrimaryGreen,
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.StrokesGainedText));

            var distance = new Label
            {
                FontSize = 15,
                TextColor = MutedTextColor
            };
            distance.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.DetailText));

            var putts = new Label
            {
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = MutedTextColor,
                HorizontalTextAlignment = TextAlignment.End
            };
            putts.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.StrokesGainedText));

            var card = Card(new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Children =
                        {
                            new VerticalStackLayout
                            {
                                Spacing = 2,
                                Children = { title, subtitle }
                            }.Column(0),
                            sg.Column(1)
                        }
                    },
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Children =
                        {
                            distance.Column(0),
                            putts.Column(1)
                        }
                    },
                    new Label
                    {
                        Text = "Tryk for at indtaste",
                        FontSize = 12,
                        TextColor = MutedTextColor
                    }
                }
            });

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (card.BindingContext is HoleInputViewModel hole)
                {
                    await OpenHoleAsync(hole);
                }
            };
            card.GestureRecognizers.Add(tap);

            return card;
        });
    }

    private View HoleCountPanel(bool showQuickSelection = false)
    {
        var holeCount = new Label
        {
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 124,
            HeightRequest = 44
        };
        holeCount.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.HoleCountText));

        var minus = StepperButton("-");
        minus.Clicked += (_, _) => viewModel.DecreaseHoleCount();

        var plus = StepperButton("+");
        plus.Clicked += (_, _) => viewModel.IncreaseHoleCount();

        var panel = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = "Rundelængde",
                            FontAttributes = FontAttributes.Bold,
                            TextColor = TextColor
                        },
                        new Label
                        {
                            Text = "Vælg hvor mange huller du spiller i dag",
                            FontSize = 13,
                            TextColor = MutedTextColor
                        }
                    }
                },
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { minus, holeCount, plus }
                }
            }
        };
        if (showQuickSelection)
        {
            panel.Children.Add(new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                Children = { QuickHoleButton(9).Column(0), QuickHoleButton(18).Column(1) }
            });
        }

        return Card(panel);
    }

    private Button QuickHoleButton(int targetCount)
    {
        var button = new Button
        {
            Text = $"{targetCount} huller",
            HeightRequest = 48,
            CornerRadius = 8,
            BackgroundColor = GolfTheme.Colors.SoftGreen,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(8, 0)
        };
        button.Accessible($"round.setup.holes-{targetCount}", $"Vælg {targetCount} huller");
        var selected = new DataTrigger(typeof(Button))
        {
            Binding = new Binding(nameof(RoundInputViewModel.HoleCount)),
            Value = targetCount
        };
        selected.Setters.Add(new Setter { Property = Button.BackgroundColorProperty, Value = PrimaryGreen });
        selected.Setters.Add(new Setter { Property = Button.TextColorProperty, Value = Colors.White });
        button.Triggers.Add(selected);
        button.Clicked += (_, _) =>
        {
            while (viewModel.HoleCount < targetCount)
            {
                viewModel.IncreaseHoleCount();
            }

            while (viewModel.HoleCount > targetCount)
            {
                var before = viewModel.HoleCount;
                viewModel.DecreaseHoleCount();
                if (viewModel.HoleCount == before)
                {
                    break;
                }
            }
        };
        return button;
    }

    private View RoundProgressPanel()
    {
        var progress = new Label
        {
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };
        progress.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.RoundProgressText));

        Label CategoryScore(string title, string scoreProperty, string trackingProperty)
        {
            var score = new Label
            {
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = PrimaryGreen
            };
            score.SetBinding(Label.TextProperty, new Binding(scoreProperty, stringFormat: title + ": {0}"));
            score.SetBinding(IsVisibleProperty, trackingProperty);
            return score;
        }

        return Card(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = "Status",
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                progress,
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Margin = new Thickness(0, 4, 0, 4),
                    Children =
                    {
                        CategoryScore("SG Putting", nameof(RoundInputViewModel.TotalPuttingSgText), nameof(RoundInputViewModel.TrackPutting)),
                        CategoryScore("SG Approach", nameof(RoundInputViewModel.TotalApproachSgText), nameof(RoundInputViewModel.TrackApproach)),
                        CategoryScore("SG Omkring green", nameof(RoundInputViewModel.TotalAroundGreenSgText), nameof(RoundInputViewModel.TrackAroundGreen))
                    }
                },
                new Label
                {
                    Text = "Ikke-registrerede huller tæller ikke som nul-resultater.",
                    FontSize = 13,
                    TextColor = MutedTextColor
                }
            }
        });
    }

    private View TrackingPanel()
    {
        var putting = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        putting.Accessible(UiAutomationIds.TrackPutting, "Track putting");
        putting.SetBinding(Switch.IsToggledProperty, nameof(RoundInputViewModel.TrackPutting), BindingMode.TwoWay);
        putting.SetBinding(IsEnabledProperty, nameof(RoundInputViewModel.CanToggleTrackPutting));

        var approach = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        approach.Accessible(UiAutomationIds.TrackApproach, "Track approachslag");
        approach.SetBinding(Switch.IsToggledProperty, nameof(RoundInputViewModel.TrackApproach), BindingMode.TwoWay);
        approach.SetBinding(IsEnabledProperty, nameof(RoundInputViewModel.CanToggleTrackApproach));

        var aroundGreen = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        aroundGreen.Accessible(UiAutomationIds.TrackAroundGreen, "Track slag omkring green");
        aroundGreen.SetBinding(Switch.IsToggledProperty, nameof(RoundInputViewModel.TrackAroundGreen), BindingMode.TwoWay);
        aroundGreen.SetBinding(IsEnabledProperty, nameof(RoundInputViewModel.CanToggleTrackAroundGreen));

        return Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = "Registrering",
                            FontAttributes = FontAttributes.Bold,
                            TextColor = TextColor
                        },
                        new Label
                        {
                            Text = "Vælg hvilke dele af runden du vil tracke",
                            FontSize = 13,
                            TextColor = MutedTextColor
                        }
                    }
                },
                TrackingRow("Putting", "Første putt-afstand og antal putts", putting),
                BetaTrackingRow("Approach", "Start, slutposition og strafslag", approach),
                BetaTrackingRow("Omkring green", "Chip, pitch, bunker og problemlie ved green", aroundGreen)
            }
        });
    }

    private View SummaryPanel()
    {
        var totalSg = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            TextColor = TextColor
        };
        totalSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalSgText), stringFormat: "Samlet SG: {0}"));

        var puttingSg = new Label { TextColor = TextColor };
        puttingSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalPuttingSgText), stringFormat: "SG Putning: {0}"));
        puttingSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));

        var approachSg = new Label { TextColor = TextColor };
        approachSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalApproachSgText), stringFormat: "SG Approach: {0}"));
        approachSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackApproach));

        var aroundGreenSg = new Label { TextColor = TextColor };
        aroundGreenSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalAroundGreenSgText), stringFormat: "SG Omkring green: {0}"));
        aroundGreenSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackAroundGreen));

        var totalPutts = new Label { TextColor = TextColor };
        totalPutts.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalPuttsText), stringFormat: "Putts i alt: {0}"));
        totalPutts.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));

        var expectedBirdies = new Label { TextColor = TextColor, FontAttributes = FontAttributes.Bold };
        expectedBirdies.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.ExpectedBirdiesText));
        expectedBirdies.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));
        var girPuttsComparison = new Label { TextColor = MutedTextColor, FontSize = 13 };
        girPuttsComparison.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.GirPuttsComparisonText));
        girPuttsComparison.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));

        var totalApproachShots = new Label { TextColor = TextColor };
        totalApproachShots.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalApproachShotsText), stringFormat: "Approach: {0}"));
        totalApproachShots.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackApproach));

        var totalAroundGreenShots = new Label { TextColor = TextColor };
        totalAroundGreenShots.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalAroundGreenShotsText), stringFormat: "Slag omkring green: {0}"));
        totalAroundGreenShots.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackAroundGreen));

        var breakdown = new Label { TextColor = TextColor };
        breakdown.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.CountBreakdownText));
        breakdown.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));

        return Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "Rundeoversigt",
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                totalSg,
                puttingSg,
                approachSg,
                aroundGreenSg,
                totalPutts,
                expectedBirdies,
                girPuttsComparison,
                totalApproachShots,
                totalAroundGreenShots,
                breakdown
            }
        });
    }

    private View BetaTrackingRow(string title, string subtitle, Switch toggle)
    {
        var row = TrackingRow($"{title} · Beta", subtitle, toggle);
        row.IsVisible = FeatureSettings.EnableBetaFeatures;
        betaTrackingRows.Add(row);
        return row;
    }

    private static View TrackingRow(string title, string subtitle, Switch toggle)
    {
        // Keep the native switch fully measured and independent of theme switch states.
        toggle.Style = new Style(typeof(Switch));
        toggle.WidthRequest = 64;
        toggle.HeightRequest = 48;
        toggle.HorizontalOptions = LayoutOptions.Center;
        toggle.VerticalOptions = LayoutOptions.Center;
        toggle.OnColor = GolfTheme.Colors.CardStroke;
        toggle.ThumbColor = toggle.IsToggled ? PrimaryGreen : MutedTextColor;
        toggle.Toggled += (_, args) => toggle.ThumbColor = args.Value ? PrimaryGreen : MutedTextColor;

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
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = title,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = TextColor
                        },
                        new Label
                        {
                            Text = subtitle,
                            FontSize = 13,
                            TextColor = MutedTextColor
                        }
                    }
                }.Column(0),
                toggle.Column(1)
            }
        };
    }

    private static Button StepperButton(string text)
    {
        return new Button
        {
            Text = text,
            WidthRequest = 44,
            HeightRequest = 44,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            Padding = 0
        };
    }

    private static Border Card(View content)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Margin = new Thickness(0, 0, 0, 10),
            Content = content
        };
    }
}
