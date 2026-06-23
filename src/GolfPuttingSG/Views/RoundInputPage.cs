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

        var holes = new CollectionView
        {
            ItemTemplate = HoleTemplate(),
            SelectionMode = SelectionMode.Single
        };
        holes.SetBinding(ItemsView.ItemsSourceProperty, nameof(RoundInputViewModel.Holes));
        holes.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not HoleInputViewModel hole)
            {
                return;
            }

            holes.SelectedItem = null;
            await Navigation.PushAsync(new HoleEntryPage(viewModel, hole));
        };

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
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                title.Row(0),
                HoleCountPanel().Row(1).Margin(new Thickness(0, 12, 0, 0)),
                holes.Row(2).Margin(new Thickness(0, 12, 0, 12)),
                SummaryPanel().Row(3),
                saveButton.Row(4).Margin(new Thickness(0, 10, 0, 0))
            }
        };
    }

    private static DataTemplate HoleTemplate()
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
            distance.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.DistanceText), stringFormat: "{0} m"));

            var putts = new Label
            {
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = MutedTextColor,
                HorizontalTextAlignment = TextAlignment.End
            };
            putts.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.Putts), stringFormat: "{0} putts"));

            return Card(new VerticalStackLayout
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

    private View SummaryPanel()
    {
        var totalSg = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            TextColor = TextColor
        };
        totalSg.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalSgText), stringFormat: "Total SG Putting: {0}"));

        var totalPutts = new Label { TextColor = TextColor };
        totalPutts.SetBinding(Label.TextProperty, new Binding(nameof(RoundInputViewModel.TotalPuttsText), stringFormat: "Total putts: {0}"));

        var breakdown = new Label { TextColor = TextColor };
        breakdown.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.CountBreakdownText));

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
                totalPutts,
                breakdown
            }
        });
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
