using GolfSG.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed partial class PuttingGamePage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;

    private readonly PuttingGameViewModel viewModel;
    private string? completedRoundId;

    public PuttingGamePage(PuttingGameViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Putting-spil";
        BackgroundColor = PageBackground;
        BuildLayout();
        Shell.SetBackButtonBehavior(this, new BackButtonBehavior
        {
            Command = new Command(async () => await NavigateBackAsync())
        });
    }

    public void Start(string mode)
    {
        viewModel.Start(mode);
        Title = viewModel.GameTitle;
    }

    public void Resume(GolfSG.Application.Putting.ActivePuttingGameSession session)
    {
        viewModel.Resume(session);
        Title = viewModel.GameTitle;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await viewModel.FlushAutosaveAsync(); }
        catch { await DisplayAlertAsync("Spillet kunne ikke gemmes", viewModel.ErrorMessage, "OK"); }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = NavigateBackAsync();
        return true;
    }

    private void BuildLayout()
    {
        var submitButton = new Button
        {
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(16, 8, 16, 16)
        };
        submitButton.SetBinding(Button.TextProperty, nameof(PuttingGameViewModel.PrimaryActionText));
        submitButton.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsActive));
        submitButton.SetBinding(VisualElement.IsEnabledProperty, nameof(PuttingGameViewModel.CanSubmit));
        submitButton.Clicked += async (_, _) => await SubmitCurrentPuttAsync();

        var setupPanel = SetupPanel();
        setupPanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsSetup));

        var activePanel = ActivePanel();
        activePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsActive));

        var completePanel = CompletePanel();
        completePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsComplete));

        var undo = AppViews.SecondaryButton("Fortryd seneste putt");
        undo.SetBinding(IsVisibleProperty, nameof(PuttingGameViewModel.IsActive));
        undo.SetBinding(IsEnabledProperty, nameof(PuttingGameViewModel.CanUndo));
        undo.Accessible("practice-undo", "Fortryd det senest registrerede putt");
        undo.Clicked += async (_, _) => await this.RunActionOnceAsync(async () =>
        {
            await viewModel.UndoLastAsync();
            if (viewModel.HasError) await DisplayAlertAsync("Resultatet kunne ikke fortrydes", viewModel.ErrorMessage, "OK");
        });
        var review = AppViews.SecondaryButton("Se og ret registrerede putts");
        review.SetBinding(IsVisibleProperty, nameof(PuttingGameViewModel.HasRecordedPutts));
        review.SetBinding(IsEnabledProperty, nameof(PuttingGameViewModel.CanReview));
        review.Accessible("practice-review", "Se og ret tidligere putt-resultater");
        review.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new PuttingGameReviewPage(viewModel)));

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
        error.SetBinding(IsVisibleProperty, nameof(PuttingGameViewModel.HasError));
        ((Label)error.Content).SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.ErrorMessage));

        var scrollView = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 16, 16, 8),
                Spacing = 10,
                Children =
                {
                    GameTitleLabel(),
                    error,
                    setupPanel,
                    activePanel,
                    undo,
                    review,
                    completePanel
                }
            }
        };

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                scrollView,
                submitButton.Row(1)
            }
        };
    }

    private static Label GameTitleLabel()
    {
        var title = new Label
        {
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };
        title.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.GameTitle));
        return title;
    }

    private View SetupPanel()
    {
        var holeCount = AppViews.NumericEntry(nameof(PuttingGameViewModel.HoleCountText));
        var minimumDistance = AppViews.NumericEntry(nameof(PuttingGameViewModel.MinimumDistanceMetersText));
        var maximumDistance = AppViews.NumericEntry(nameof(PuttingGameViewModel.MaximumDistanceMetersText));
        var distanceDistribution = new Picker
        {
            Title = "Fordeling",
            TextColor = TextColor,
            BackgroundColor = Colors.White,
            HeightRequest = 48
        };
        distanceDistribution.SetBinding(Picker.ItemsSourceProperty, nameof(PuttingGameViewModel.TrainingDistanceDistributionOptions));
        distanceDistribution.SetBinding(Picker.SelectedItemProperty, nameof(PuttingGameViewModel.SelectedTrainingDistanceDistribution), BindingMode.TwoWay);

        var error = new Label
        {
            TextColor = Colors.DarkRed,
            FontAttributes = FontAttributes.Bold
        };
        error.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.SetupErrorText));
        error.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.HasSetupError));

        var start = new Button
        {
            Text = "Start spil",
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold
        };
        start.Clicked += async (_, _) => await this.RunActionOnceAsync(async () =>
        {
            if (!viewModel.StartConfiguredGame()) return;
            try { await viewModel.FlushAutosaveAsync(); }
            catch { await DisplayAlertAsync("Spillet kunne ikke gemmes", viewModel.ErrorMessage, "OK"); }
        });

        return AppViews.FormCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "Spilopsætning",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                AppViews.Field("Putts", holeCount),
                AppViews.BoundField(nameof(PuttingGameViewModel.MinimumDistanceLabel), minimumDistance),
                AppViews.BoundField(nameof(PuttingGameViewModel.MaximumDistanceLabel), maximumDistance),
                AppViews.Field("Afstandsfordeling", distanceDistribution),
                error,
                start
            }
        });
    }

    private View ActivePanel()
    {
        var progress = new Label
        {
            FontSize = 14,
            TextColor = MutedTextColor
        };
        progress.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.ProgressText));

        var progressCount = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.End
        };
        progressCount.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.ProgressCountText));

        var progressBar = new ProgressBar
        {
            HeightRequest = 6,
            ProgressColor = PrimaryGreen,
            BackgroundColor = CardStroke
        };
        progressBar.SetBinding(ProgressBar.ProgressProperty, nameof(PuttingGameViewModel.ProgressFraction));

        var distance = new Label
        {
            FontSize = 42,
            FontAttributes = FontAttributes.Bold,
            TextColor = PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center
        };
        distance.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.CurrentDistanceText));

        var minus = AppViews.StepperButton("-");
        minus.SetBinding(IsEnabledProperty, nameof(PuttingGameViewModel.CanSubmit));
        minus.Clicked += (_, _) => viewModel.DecreasePutts();

        var plus = AppViews.StepperButton("+");
        plus.SetBinding(IsEnabledProperty, nameof(PuttingGameViewModel.CanSubmit));
        plus.Clicked += (_, _) => viewModel.IncreasePutts();

        var putts = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 82,
            HeightRequest = 48
        };
        putts.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.PuttsUsedText));

        var currentResult = new Label
        {
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center
        };
        currentResult.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.CurrentResultText));

        var remainingPreview = new Label
        {
            FontSize = 13,
            TextColor = MutedTextColor,
            HorizontalTextAlignment = TextAlignment.Center
        };
        remainingPreview.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.RemainingDistancesPreviewText));

        var progressHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Children = { progress, progressCount.Column(1) }
        };

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                AppViews.FormCard(new VerticalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        progressHeader,
                        progressBar,
                        remainingPreview,
                        distance,
                        new Label
                        {
                            Text = "Brugte putts",
                            TextColor = MutedTextColor,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new HorizontalStackLayout
                        {
                            Spacing = 10,
                            HorizontalOptions = LayoutOptions.Center,
                            Children = { minus, putts, plus }
                        },
                        currentResult
                    }
                }),
                RunningTotalPanel()
            }
        };
    }

    private View RunningTotalPanel()
    {
        var remainingPreview = new Label
        {
            FontSize = 13,
            TextColor = MutedTextColor
        };
        remainingPreview.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.RemainingCountText));

        var remaining = new Label
        {
            FontSize = 13,
            TextColor = TextColor
        };
        remaining.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.RemainingDistancesText));
        remaining.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.ShowRemainingDistanceDetails));

        var detailsButton = new Button
        {
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 13,
            Padding = new Thickness(10, 6)
        };
        detailsButton.SetBinding(Button.TextProperty, nameof(PuttingGameViewModel.RemainingDistanceDetailsButtonText));
        detailsButton.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.HasRemainingDistanceDetails));
        detailsButton.Clicked += (_, _) => viewModel.ShowRemainingDistanceDetails = !viewModel.ShowRemainingDistanceDetails;

        var stats = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            Children =
            {
                StatBlock("Resultat", nameof(PuttingGameViewModel.CurrentResultValueText)),
                StatBlock("Putts", nameof(PuttingGameViewModel.TotalPuttsText)).Column(1),
                StatBlock("SG total", nameof(PuttingGameViewModel.RunningSgText)).Column(2)
            }
        };

        return AppViews.FormCard(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Løbende total",
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                stats,
                remainingPreview,
                detailsButton,
                remaining
            }
        });
    }


    private View CompletePanel()
    {
        var viewResult = new Button
        {
            Text = "Se gemt resultat",
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        viewResult.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(completedRoundId))
            {
                return;
            }

            try
            {
                var page = Handler!.MauiContext!.Services.GetRequiredService<RoundResultPage>();
                await page.LoadAsync(completedRoundId);
                await this.RunNavigationOnceAsync(() => Navigation.PushAsync(page));
            }
            catch (Exception)
            {
                await DisplayAlertAsync(
                    "Resultatet kunne ikke åbnes",
                    "Prøv igen, eller tjek lagring under Indstillinger.",
                    "OK");
            }
        };

        var finish = new Button
        {
            Text = "Færdig",
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            CornerRadius = 8,
            HeightRequest = 50
        };
        finish.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PopToRootAsync());

        return AppViews.FormCard(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Opsummering",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                BoundLabel(nameof(PuttingGameViewModel.TotalPuttsText), "Putts i alt: {0}"),
                BoundLabel(nameof(PuttingGameViewModel.TargetPuttsText), "Mål-putts: {0}"),
                BoundLabel(nameof(PuttingGameViewModel.FinalSgText), "Endelig putting SG: {0}", true),
                BoundLabel(nameof(PuttingGameViewModel.BestResultText), "Bedste resultat: {0}"),
                BoundLabel(nameof(PuttingGameViewModel.WorstResultText), "Værste resultat: {0}"),
                viewResult.Margin(new Thickness(0, 8, 0, 0)),
                finish
            }
        });
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

    private static View StatBlock(string title, string bindingPath)
    {
        var value = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
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
                    TextColor = MutedTextColor,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                value
            }
        };
    }

}
