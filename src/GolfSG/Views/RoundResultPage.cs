using GolfSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class RoundResultPage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;

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
            try
            {
                var page = Handler!.MauiContext!.Services.GetRequiredService<RoundInputPage>();
                await page.LoadAsync(viewModel.RoundId);
                await Navigation.PushAsync(page);
            }
            catch (Exception)
            {
                await DisplayAlertAsync(
                    "Runden kunne ikke åbnes",
                    "Prøv igen, eller tjek lagring under Indstillinger.",
                    "OK");
            }
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
        var puttingSg = BoundLabel(nameof(RoundResultViewModel.TotalPuttingSgText), "SG Putning: {0}");
        puttingSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var averagePuttDistance = BoundLabel(nameof(RoundResultViewModel.AverageDistanceText), "Gns. første putt-afstand: {0}");
        averagePuttDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var totalPutts = BoundLabel(nameof(RoundResultViewModel.TotalPuttsText), "Putts i alt: {0}");
        totalPutts.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var targetPutts = BoundLabel(nameof(RoundResultViewModel.TargetPuttsText), "Mål-putts: {0}");
        targetPutts.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.IsPuttingGame));

        var threePuttRate = BoundLabel(nameof(RoundResultViewModel.ThreePuttRateText), "3-putt-andel: {0}");
        threePuttRate.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var bestHole = BoundLabel(nameof(RoundResultViewModel.BestHoleText), "Bedste putting-hul: {0}");
        bestHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var worstHole = BoundLabel(nameof(RoundResultViewModel.WorstHoleText), "Værste putting-hul: {0}");
        worstHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackPutting));

        var approachSg = BoundLabel(nameof(RoundResultViewModel.TotalApproachSgText), "SG Indspil: {0}");
        approachSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var averageApproachDistance = BoundLabel(nameof(RoundResultViewModel.AverageApproachDistanceText), "Gns. indspilsafstand: {0}");
        averageApproachDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var totalApproachShots = BoundLabel(nameof(RoundResultViewModel.TotalApproachShotsText), "Indspil: {0}");
        totalApproachShots.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var bestApproachHole = BoundLabel(nameof(RoundResultViewModel.BestApproachHoleText), "Bedste indspilshul: {0}");
        bestApproachHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var worstApproachHole = BoundLabel(nameof(RoundResultViewModel.WorstApproachHoleText), "Værste indspilshul: {0}");
        worstApproachHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackApproach));

        var aroundGreenSg = BoundLabel(nameof(RoundResultViewModel.TotalAroundGreenSgText), "SG Omkring green: {0}");
        aroundGreenSg.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackAroundGreen));

        var averageAroundGreenDistance = BoundLabel(nameof(RoundResultViewModel.AverageAroundGreenDistanceText), "Gns. afstand omkring green: {0}");
        averageAroundGreenDistance.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackAroundGreen));

        var totalAroundGreenShots = BoundLabel(nameof(RoundResultViewModel.TotalAroundGreenShotsText), "Slag omkring green: {0}");
        totalAroundGreenShots.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackAroundGreen));

        var bestAroundGreenHole = BoundLabel(nameof(RoundResultViewModel.BestAroundGreenHoleText), "Bedste hul omkring green: {0}");
        bestAroundGreenHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackAroundGreen));

        var worstAroundGreenHole = BoundLabel(nameof(RoundResultViewModel.WorstAroundGreenHoleText), "Værste hul omkring green: {0}");
        worstAroundGreenHole.SetBinding(VisualElement.IsVisibleProperty, nameof(RoundResultViewModel.TrackAroundGreen));

        return Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                BoundLabel(nameof(RoundResultViewModel.TotalSgText), "Samlet SG: {0}", true),
                BoundLabel(nameof(RoundResultViewModel.RoundProgressText), "Status: {0}"),
                BoundLabel(nameof(RoundResultViewModel.RoundCompletionText), "{0}"),
                puttingSg,
                approachSg,
                aroundGreenSg,
                averagePuttDistance,
                averageApproachDistance,
                averageAroundGreenDistance,
                totalPutts,
                targetPutts,
                totalApproachShots,
                totalAroundGreenShots,
                threePuttRate,
                bestHole,
                worstHole,
                bestApproachHole,
                worstApproachHole,
                bestAroundGreenHole,
                worstAroundGreenHole
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
