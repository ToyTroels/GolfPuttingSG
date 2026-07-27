using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class BenchmarkHistoryPage : ContentPage
{
    private readonly BenchmarkHistoryViewModel viewModel;

    public BenchmarkHistoryPage(BenchmarkHistoryViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Benchmark historie beta";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        BuildLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
        if (viewModel.HasError)
        {
            await DisplayAlertAsync("Benchmark-historik kunne ikke indl\u00e6ses", viewModel.ErrorMessage, "OK");
        }
    }

    private void BuildLayout()
    {
        var errorLabel = new Label
        {
            FontSize = 14,
            TextColor = GolfTheme.Colors.DangerText
        };
        var error = AppViews.Card(errorLabel);
        error.BackgroundColor = GolfTheme.Colors.DangerBackground;
        error.Stroke = GolfTheme.Colors.DangerText;
        error.SetBinding(IsVisibleProperty, nameof(BenchmarkHistoryViewModel.HasError));
        errorLabel.SetBinding(Label.TextProperty, nameof(BenchmarkHistoryViewModel.ErrorMessage));

        var empty = new Label
        {
            FontSize = 14,
            TextColor = GolfTheme.Colors.MutedText,
            Margin = new Thickness(0, 8, 0, 0)
        };
        empty.SetBinding(Label.TextProperty, nameof(BenchmarkHistoryViewModel.EmptyStateText));
        empty.SetBinding(IsVisibleProperty, new Binding(nameof(BenchmarkHistoryViewModel.HasBenchmarks), converter: new InvertedBoolConverter()));

        var benchmarks = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = BenchmarkTemplate()
        };
        benchmarks.SetBinding(ItemsView.ItemsSourceProperty, nameof(BenchmarkHistoryViewModel.Benchmarks));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 10,
                Children =
                {
                    AppViews.PageTitle("Benchmark historie beta"),
                    error,
                    empty,
                    benchmarks
                }
            }
        };
    }

    private static DataTemplate BenchmarkTemplate()
    {
        return new DataTemplate(() =>
        {
            var title = new Label
            {
                FontSize = 17,
                FontAttributes = FontAttributes.Bold,
                TextColor = GolfTheme.Colors.Text
            };
            title.SetBinding(Label.TextProperty, nameof(BenchmarkHistoryItemViewModel.Title));

            var type = new Label
            {
                FontSize = 13,
                TextColor = GolfTheme.Colors.MutedText
            };
            type.SetBinding(Label.TextProperty, nameof(BenchmarkHistoryItemViewModel.TypeText));

            var detail = new Label
            {
                FontSize = 13,
                TextColor = GolfTheme.Colors.MutedText
            };
            detail.SetBinding(Label.TextProperty, nameof(BenchmarkHistoryItemViewModel.DetailText));

            return AppViews.Card(new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 3,
                        Children = { title, type, detail }
                    },
                    MetricGrid(),
                    BoundLabel(nameof(BenchmarkHistoryItemViewModel.AttemptsText)),
                    BoundLabel(nameof(BenchmarkHistoryItemViewModel.LatestDateText))
                }
            });
        });
    }

    private static View MetricGrid()
    {
        return new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                Metric("Seneste", nameof(BenchmarkHistoryItemViewModel.LatestScoreText)).Row(0).Column(0),
                Metric("Bedste", nameof(BenchmarkHistoryItemViewModel.BestScoreText)).Row(0).Column(1),
                Metric("Sidste 3", nameof(BenchmarkHistoryItemViewModel.AverageLastThreeText)).Row(1).Column(0),
                Metric("Udvikling", nameof(BenchmarkHistoryItemViewModel.ImprovementText)).Row(1).Column(1)
            }
        };
    }

    private static View Metric(string title, string bindingPath)
    {
        var value = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center
        };
        value.SetBinding(Label.TextProperty, bindingPath);

        return new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 12,
                    TextColor = GolfTheme.Colors.MutedText,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                value
            }
        };
    }

    private static Label BoundLabel(string bindingPath)
    {
        var label = new Label
        {
            FontSize = 13,
            TextColor = GolfTheme.Colors.MutedText
        };
        label.SetBinding(Label.TextProperty, bindingPath);
        return label;
    }
}
