using GolfPuttingSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfPuttingSG.Views;

public sealed class RoundInputPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly RoundInputViewModel viewModel;

    public RoundInputPage(RoundInputViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Runde";
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    public async Task LoadAsync(string? roundId)
    {
        await viewModel.LoadAsync(roundId);
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
        };
        BindableLayout.SetItemTemplate(holes, HoleTemplate());
        holes.SetBinding(BindableLayout.ItemsSourceProperty, nameof(RoundInputViewModel.Holes));

        var saveButton = new Button
        {
            Text = "Gem runde",
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        saveButton.Clicked += async (_, _) =>
        {
            var roundId = await viewModel.SaveAsync();
            var page = Handler!.MauiContext!.Services.GetRequiredService<RoundResultPage>();
            await page.LoadAsync(roundId);
            await Navigation.PushAsync(page);
        };

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
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 0,
                        Children =
                        {
                            title,
                            HoleCountPanel().Margin(new Thickness(0, 12, 0, 0)),
                            TrackingPanel(),
                            SummaryPanel(),
                            new Label
                            {
                                Text = "Huller",
                                FontSize = 18,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = TextColor,
                                Margin = new Thickness(0, 2, 0, 10)
                            },
                            holes
                        }
                    }
                }.Row(0),
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
                    await Navigation.PushAsync(new HoleEntryPage(viewModel, hole));
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

    private View TrackingPanel()
    {
        var putting = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        putting.SetBinding(Switch.IsToggledProperty, nameof(RoundInputViewModel.TrackPutting), BindingMode.TwoWay);

        var approach = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        approach.SetBinding(Switch.IsToggledProperty, nameof(RoundInputViewModel.TrackApproach), BindingMode.TwoWay);

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
                            Text = "Tracking",
                            FontAttributes = FontAttributes.Bold,
                            TextColor = TextColor
                        },
                        new Label
                        {
                            Text = "Vælg om runden skal tracke putting, approach eller begge dele",
                            FontSize = 13,
                            TextColor = MutedTextColor
                        }
                    }
                },
                TrackingRow("Putting", "Første putt-afstand og antal putts", putting),
                TrackingRow("Approach", "Afstand og slag brugt til at komme i hul", approach)
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
        totalSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalSgText), stringFormat: "Total SG: {0}"));

        var puttingSg = new Label { TextColor = TextColor };
        puttingSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalPuttingSgText), stringFormat: "SG Putting: {0}"));
        puttingSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));

        var approachSg = new Label { TextColor = TextColor };
        approachSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalApproachSgText), stringFormat: "SG Approach: {0}"));
        approachSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackApproach));

        var totalPutts = new Label { TextColor = TextColor };
        totalPutts.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalPuttsText), stringFormat: "Total putts: {0}"));
        totalPutts.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackPutting));

        var totalApproachShots = new Label { TextColor = TextColor };
        totalApproachShots.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalApproachShotsText), stringFormat: "Approach-slag: {0}"));
        totalApproachShots.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundInputViewModel.TrackApproach));

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
                totalPutts,
                totalApproachShots,
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
