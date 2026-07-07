using GolfSG.ViewModels;

namespace GolfSG.Views;

public sealed class EvaluationPage : ContentPage
{
    private readonly EvaluationViewModel viewModel;

    public EvaluationPage(EvaluationViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "SG evaluering beta";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        BuildLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
        if (viewModel.HasError)
        {
            await DisplayAlertAsync("Evaluering kunne ikke indl\u00e6ses", viewModel.ErrorMessage, "OK");
        }
    }

    private void BuildLayout()
    {
        var period = new Picker
        {
            Title = "Periode",
            TextColor = GolfTheme.Colors.Text,
            BackgroundColor = Colors.White,
            HeightRequest = 48
        };
        period.SetBinding(Picker.ItemsSourceProperty, nameof(EvaluationViewModel.PeriodOptions));
        period.SetBinding(Picker.SelectedItemProperty, nameof(EvaluationViewModel.SelectedPeriod), BindingMode.TwoWay);

        var category = new Picker
        {
            Title = "Kategori",
            TextColor = GolfTheme.Colors.Text,
            BackgroundColor = Colors.White,
            HeightRequest = 48
        };
        category.SetBinding(Picker.ItemsSourceProperty, nameof(EvaluationViewModel.CategoryOptions));
        category.SetBinding(Picker.SelectedItemProperty, nameof(EvaluationViewModel.SelectedCategory), BindingMode.TwoWay);

        var errorLabel = new Label
        {
            TextColor = GolfTheme.Colors.DangerText,
            FontSize = 14
        };
        var error = AppViews.Card(errorLabel);
        error.BackgroundColor = GolfTheme.Colors.DangerBackground;
        error.Stroke = GolfTheme.Colors.DangerText;
        error.SetBinding(IsVisibleProperty, nameof(EvaluationViewModel.HasError));
        errorLabel.SetBinding(Label.TextProperty, nameof(EvaluationViewModel.ErrorMessage));

        var empty = new Label
        {
            FontSize = 14,
            TextColor = GolfTheme.Colors.MutedText
        };
        empty.SetBinding(Label.TextProperty, nameof(EvaluationViewModel.EmptyStateText));
        empty.SetBinding(IsVisibleProperty, new Binding(nameof(EvaluationViewModel.HasTrackedRounds), converter: new InvertedBoolConverter()));

        var buckets = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = BucketTemplate()
        };
        buckets.SetBinding(ItemsView.ItemsSourceProperty, nameof(EvaluationViewModel.PuttingBuckets));
        buckets.SetBinding(IsVisibleProperty, nameof(EvaluationViewModel.ShowPuttingBuckets));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 10,
                Children =
                {
                    AppViews.PageTitle("SG evaluering beta"),
                    AppViews.Card(new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            Field("Periode", period),
                            Field("Kategori", category)
                        }
                    }),
                    error,
                    AppViews.Card(new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            BoundLabel(nameof(EvaluationViewModel.IncludedRoundsText), bold: true),
                            BoundLabel(nameof(EvaluationViewModel.PeriodRoundsText)),
                            empty,
                            MetricGrid()
                        }
                    }),
                    new Label
                    {
                        Text = "Putting pr. afstand",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = GolfTheme.Colors.Text,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    buckets
                }
            }
        };
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
                Metric("Total SG", nameof(EvaluationViewModel.TotalSgText)).Row(0).Column(0),
                Metric("SG pr. runde", nameof(EvaluationViewModel.AverageSgText)).Row(0).Column(1),
                Metric("Bedste", nameof(EvaluationViewModel.BestRoundText)).Row(1).Column(0),
                Metric("V\u00e6rste", nameof(EvaluationViewModel.WorstRoundText)).Row(1).Column(1)
            }
        };
    }

    private static View Metric(string title, string bindingPath)
    {
        var value = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.Text,
            LineBreakMode = LineBreakMode.WordWrap
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
                    TextColor = GolfTheme.Colors.MutedText
                },
                value
            }
        };
    }

    private static Label BoundLabel(string bindingPath, bool bold = false)
    {
        var label = new Label
        {
            TextColor = GolfTheme.Colors.Text,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
        };
        label.SetBinding(Label.TextProperty, bindingPath);
        return label;
    }

    private static View Field(string labelText, View input)
    {
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = labelText,
                    TextColor = GolfTheme.Colors.MutedText,
                    FontAttributes = FontAttributes.Bold
                },
                input
            }
        };
    }

    private static DataTemplate BucketTemplate()
    {
        return new DataTemplate(() =>
        {
            var name = new Label
            {
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = GolfTheme.Colors.Text
            };
            name.SetBinding(Label.TextProperty, nameof(PuttingMadePercentageBucketItemViewModel.Name));

            var detail = new Label
            {
                FontSize = 13,
                TextColor = GolfTheme.Colors.MutedText
            };
            detail.SetBinding(Label.TextProperty, nameof(PuttingMadePercentageBucketItemViewModel.DetailText));

            var average = new Label
            {
                FontSize = 13,
                TextColor = GolfTheme.Colors.MutedText
            };
            average.SetBinding(Label.TextProperty, nameof(PuttingMadePercentageBucketItemViewModel.AveragePuttsText));

            var sg = new Label
            {
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = GolfTheme.Colors.PrimaryGreen,
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(PuttingMadePercentageBucketItemViewModel.StrokesGainedText));

            return AppViews.Card(new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 3,
                        Children = { name, detail, average }
                    }.Column(0),
                    sg.Column(1)
                }
            });
        });
    }
}
