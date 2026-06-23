using GolfPuttingSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfPuttingSG.Views;

public sealed class RoundResultPage : ContentPage
{
    private readonly RoundResultViewModel viewModel;

    public RoundResultPage(RoundResultViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Resultat";
        BackgroundColor = Color.FromArgb("#F4F1E8");
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
            BackgroundColor = Color.FromArgb("#0F5132"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        editButton.Clicked += async (_, _) =>
        {
            var page = Handler!.MauiContext!.Services.GetRequiredService<RoundInputPage>();
            await page.LoadAsync(viewModel.RoundId);
            await Navigation.PushAsync(page);
        };

        var holes = new CollectionView
        {
            ItemTemplate = HoleResultTemplate()
        };
        holes.SetBinding(ItemsView.ItemsSourceProperty, nameof(RoundResultViewModel.HoleResults));

        Content = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
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
                        new Label
                        {
                            Text = "Resultat",
                            FontSize = 26,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#202421")
                        }.Column(0),
                        editButton.Column(1)
                    }
                }.Row(0),
                SummaryPanel().Row(1).Margin(new Thickness(0, 12, 0, 10)),
                AnalysisPanel().Row(2),
                holes.Row(3).Margin(new Thickness(0, 10, 0, 0))
            }
        };
    }

    private View SummaryPanel()
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#DCE4DD"),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    BoundLabel(nameof(RoundResultViewModel.TotalSgText), "Total SG Putting: {0}", true),
                    BoundLabel(nameof(RoundResultViewModel.AverageDistanceText), "Gns. første putt-afstand: {0}"),
                    BoundLabel(nameof(RoundResultViewModel.TotalPuttsText), "Total putts: {0}"),
                    BoundLabel(nameof(RoundResultViewModel.ThreePuttRateText), "3-putt rate: {0}"),
                    BoundLabel(nameof(RoundResultViewModel.BestHoleText), "Bedste hul: {0}"),
                    BoundLabel(nameof(RoundResultViewModel.WorstHoleText), "Værste hul: {0}")
                }
            }
        };
    }

    private View AnalysisPanel()
    {
        var notes = new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label { FontSize = 14 };
                label.SetBinding(Label.TextProperty, ".");
                return label;
            })
        };
        notes.SetBinding(ItemsView.ItemsSourceProperty, nameof(RoundResultViewModel.Analysis));

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#DCE4DD"),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = "Analyse", FontAttributes = FontAttributes.Bold },
                    notes
                }
            }
        };
    }

    private static Label BoundLabel(string path, string format, bool bold = false)
    {
        var label = new Label { FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None };
        label.SetBinding(Label.TextProperty, new Binding(path, stringFormat: format));
        return label;
    }

    private static DataTemplate HoleResultTemplate()
    {
        return new DataTemplate(() =>
        {
            var title = new Label { FontAttributes = FontAttributes.Bold };
            title.SetBinding(Label.TextProperty, nameof(HoleResultItemViewModel.Title));

            var detail = new Label { FontSize = 13, TextColor = Color.FromArgb("#4E5851") };
            detail.SetBinding(Label.TextProperty, nameof(HoleResultItemViewModel.Detail));

            var sg = new Label
            {
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0F5132"),
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(HoleResultItemViewModel.StrokesGainedText));

            return new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#DCE4DD"),
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
}
