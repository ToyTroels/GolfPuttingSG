using GolfSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class RoundResultPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly RoundResultViewModel viewModel;

    public RoundResultPage(RoundResultViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Resultat";
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    public async Task LoadAsync(string roundId)
    {
        await viewModel.LoadAsync(roundId);
    }

    private void BuildLayout()
    {
        var editButton = new Button
        {
            Text = "Rediger",
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            CornerRadius = 8
        };
        editButton.Clicked += async (_, _) =>
        {
            var page = Handler!.MauiContext!.Services.GetRequiredService<RoundInputPage>();
            await page.LoadAsync(viewModel.RoundId);
            await Navigation.PushAsync(page);
        };
        editButton.SetBinding(VisualElement.IsVisibleProperty, new Binding(nameof(RoundResultViewModel.IsPuttingGame), converter: new InvertedBoolConverter()));

        var title = new Label
        {
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };
        title.SetBinding(Label.TextProperty, nameof(RoundResultViewModel.ResultTitle));

        var finishButton = new Button
        {
            Text = "Afslut runde",
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold
        };
        finishButton.Clicked += async (_, _) => await GoBackToStartAsync();

        var holes = new VerticalStackLayout();
        BindableLayout.SetItemTemplate(holes, HoleResultTemplate());
        holes.SetBinding(BindableLayout.ItemsSourceProperty, nameof(RoundResultViewModel.HoleResults));

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
                        Spacing = 10,
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
                                    title.Column(0),
                                    editButton.Column(1)
                                }
                            },
                            SummaryPanel(),
                            PuttingDistancePanel(),
                            AnalysisPanel(),
                            holes
                        }
                    }
                }.Row(0),
                finishButton.Row(1).Margin(new Thickness(0, 10, 0, 0))
            }
        };
    }

    private async Task GoBackToStartAsync()
    {
        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopToRootAsync();
        }
    }

    private View SummaryPanel()
    {
        var puttingSg = BoundLabel(nameof(RoundResultViewModel.TotalPuttingSgText), "SG Putting: {0}");
        puttingSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var averagePuttDistance = BoundLabel(nameof(RoundResultViewModel.AverageDistanceText), "Gns. første putt-afstand: {0}");
        averagePuttDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var totalPutts = BoundLabel(nameof(RoundResultViewModel.TotalPuttsText), "Total putts: {0}");
        totalPutts.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var targetPutts = BoundLabel(nameof(RoundResultViewModel.TargetPuttsText), "Target putts: {0}");
        targetPutts.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.IsPuttingGame));

        var threePuttRate = BoundLabel(nameof(RoundResultViewModel.ThreePuttRateText), "3-putt rate: {0}");
        threePuttRate.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var bestHole = BoundLabel(nameof(RoundResultViewModel.BestHoleText), "Bedste putting-hul: {0}");
        bestHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var worstHole = BoundLabel(nameof(RoundResultViewModel.WorstHoleText), "Værste putting-hul: {0}");
        worstHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var approachSg = BoundLabel(nameof(RoundResultViewModel.TotalApproachSgText), "SG Approach: {0}");
        approachSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var averageApproachDistance = BoundLabel(nameof(RoundResultViewModel.AverageApproachDistanceText), "Gns. approach-afstand: {0}");
        averageApproachDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var totalApproachShots = BoundLabel(nameof(RoundResultViewModel.TotalApproachShotsText), "Approach-slag: {0}");
        totalApproachShots.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var bestApproachHole = BoundLabel(nameof(RoundResultViewModel.BestApproachHoleText), "Bedste approach-hul: {0}");
        bestApproachHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var worstApproachHole = BoundLabel(nameof(RoundResultViewModel.WorstApproachHoleText), "Værste approach-hul: {0}");
        worstApproachHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        return Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                BoundLabel(nameof(RoundResultViewModel.TotalSgText), "Total SG: {0}", true),
                puttingSg,
                approachSg,
                averagePuttDistance,
                averageApproachDistance,
                totalPutts,
                targetPutts,
                totalApproachShots,
                threePuttRate,
                bestHole,
                worstHole,
                bestApproachHole,
                worstApproachHole
            }
        });
    }

    private View AnalysisPanel()
    {
        var notes = new VerticalStackLayout { Spacing = 4 };
        BindableLayout.SetItemTemplate(notes, new DataTemplate(() =>
        {
            var label = new Label
            {
                FontSize = 14,
                TextColor = TextColor
            };
            label.SetBinding(Label.TextProperty, ".");
            return label;
        }));
        notes.SetBinding(BindableLayout.ItemsSourceProperty, nameof(RoundResultViewModel.Analysis));

        return Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "Analyse",
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                notes
            }
        });
    }

    private View PuttingDistancePanel()
    {
        var buckets = new VerticalStackLayout();
        BindableLayout.SetItemTemplate(buckets, PuttingDistanceBucketTemplate());
        buckets.SetBinding(BindableLayout.ItemsSourceProperty, nameof(RoundResultViewModel.PuttingDistanceBuckets));

        var panel = Card(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Putting-overblik",
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                buckets
            }
        });
        panel.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        return panel;
    }

    private static Label BoundLabel(string path, string format, bool bold = false)
    {
        var label = new Label
        {
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            TextColor = TextColor
        };
        label.SetBinding(Label.TextProperty, new Binding(path, stringFormat: format));
        return label;
    }

    private static DataTemplate HoleResultTemplate()
    {
        return new DataTemplate(() =>
        {
            var title = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = TextColor
            };
            title.SetBinding(Label.TextProperty, nameof(HoleResultItemViewModel.Title));

            var detail = new Label { FontSize = 13, TextColor = MutedTextColor };
            detail.SetBinding(Label.TextProperty, nameof(HoleResultItemViewModel.Detail));

            var sg = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = PrimaryGreen,
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(HoleResultItemViewModel.StrokesGainedText));

            return new Border
            {
                BackgroundColor = Colors.White,
                Stroke = CardStroke,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = 12,
                Margin = new Thickness(0, 0, 0, 10),
                Content = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Children =
                    {
                        new VerticalStackLayout { Children = { title, detail } }.Column(0),
                        sg.Column(1)
                    }
                }
            };
        });
    }

    private static DataTemplate PuttingDistanceBucketTemplate()
    {
        return new DataTemplate(() =>
        {
            var name = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = TextColor
            };
            name.SetBinding(Label.TextProperty, nameof(PuttingDistanceBucketItemViewModel.Name));

            var range = new Label
            {
                FontSize = 12,
                TextColor = MutedTextColor
            };
            range.SetBinding(Label.TextProperty, nameof(PuttingDistanceBucketItemViewModel.RangeText));

            var detail = new Label
            {
                FontSize = 12,
                TextColor = MutedTextColor
            };
            detail.SetBinding(Label.TextProperty, nameof(PuttingDistanceBucketItemViewModel.DetailText));

            var sg = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = PrimaryGreen,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center
            };
            sg.SetBinding(Label.TextProperty, nameof(PuttingDistanceBucketItemViewModel.StrokesGainedText));

            return new Grid
            {
                Padding = new Thickness(0, 4),
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 1,
                        Children = { name, range, detail }
                    }.Column(0),
                    sg.Column(1)
                }
            };
        });
    }

    private static Border Card(View content)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = content
        };
    }
}

public sealed class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }
}
