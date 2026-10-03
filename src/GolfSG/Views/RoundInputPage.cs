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
            await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new HoleEntryPage(viewModel, hole)));
        }
    }
    public Task FlushActiveRoundAsync() => viewModel.FlushAutosaveAsync();

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
                    HoleCountPanel().Margin(new Thickness(0, 12, 0, 0)),
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
                    await viewModel.SetCurrentHoleAsync(hole.HoleNumber);
                    await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new HoleEntryPage(viewModel, hole)));
                }
            };
            card.GestureRecognizers.Add(tap);

            return card;
        });
    }

    private View HoleCountPanel()
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
        });
    }

    private View RoundProgressPanel()
    {
        var progress = new Label
        {
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };
        progress.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.RoundProgressText));

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

        var approach = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        approach.Accessible(UiAutomationIds.TrackApproach, "Track approachslag");
        approach.SetBinding(Switch.IsToggledProperty, nameof(RoundInputViewModel.TrackApproach), BindingMode.TwoWay);

        var aroundGreen = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        aroundGreen.Accessible(UiAutomationIds.TrackAroundGreen, "Track slag omkring green");
        aroundGreen.SetBinding(Switch.IsToggledProperty, nameof(RoundInputViewModel.TrackAroundGreen), BindingMode.TwoWay);

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
                TrackingRow("Approach", "Start, slutposition og strafslag", approach),
                TrackingRow("Omkring green", "Chip, pitch, bunker og problemlie ved green", aroundGreen)
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
        approachSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalApproachSgText), stringFormat: "SG Indspil: {0}"));
        approachSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackApproach));

        var aroundGreenSg = new Label { TextColor = TextColor };
        aroundGreenSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalAroundGreenSgText), stringFormat: "SG Omkring green: {0}"));
        aroundGreenSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackAroundGreen));

        var totalPutts = new Label { TextColor = TextColor };
        totalPutts.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalPuttsText), stringFormat: "Putts i alt: {0}"));
        totalPutts.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));

        var totalApproachShots = new Label { TextColor = TextColor };
        totalApproachShots.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalApproachShotsText), stringFormat: "Indspil: {0}"));
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
                totalApproachShots,
                totalAroundGreenShots,
                breakdown
            }
        });
    }

    private static View TrackingRow(string title, string subtitle, Switch toggle)
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
