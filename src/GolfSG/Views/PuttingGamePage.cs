using GolfSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class PuttingGamePage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

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
        submitButton.Clicked += async (_, _) =>
        {
            submitButton.IsEnabled = false;
            try
            {
                completedRoundId = await viewModel.SubmitAsync();
            }
            catch (Exception)
            {
                await DisplayAlertAsync(
                    "Resultatet kunne ikke gemmes",
                    "Prøv igen, eller tjek lagring under Indstillinger.",
                    "OK");
            }
            finally
            {
                submitButton.IsEnabled = true;
            }
        };

        var setupPanel = SetupPanel();
        setupPanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsSetup));

        var activePanel = ActivePanel();
        activePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsActive));

        var completePanel = CompletePanel();
        completePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsComplete));

        var scrollView = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 16, 16, 8),
                Spacing = 10,
                Children =
                {
                    GameTitleLabel(),
                    setupPanel,
                    activePanel,
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
        var holeCount = NumericEntry(nameof(PuttingGameViewModel.HoleCountText));
        var minimumDistance = NumericEntry(nameof(PuttingGameViewModel.MinimumDistanceMetersText));
        var maximumDistance = NumericEntry(nameof(PuttingGameViewModel.MaximumDistanceMetersText));

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
        start.Clicked += (_, _) => viewModel.StartConfiguredGame();

        return Card(new VerticalStackLayout
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
                Field("Putts", holeCount),
                Field("Minimumsafstand (m)", minimumDistance),
                Field("Maksimumsafstand (m)", maximumDistance),
                error,
                start,
                new BoxView
                {
                    HeightRequest = 1,
                    BackgroundColor = CardStroke,
                    Margin = new Thickness(0, 4)
                },
                BenchmarkToggleButton(),
            }
        });
    }

    private Button BenchmarkToggleButton()
    {
        var button = new Button
        {
            Text = "Test",
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        button.Clicked += async (_, _) => await Navigation.PushAsync(new PuttingBenchmarkPage(viewModel));
        return button;
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

        var minus = StepperButton("-");
        minus.Clicked += (_, _) => viewModel.DecreasePutts();

        var plus = StepperButton("+");
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
                Card(new VerticalStackLayout
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

        return Card(new VerticalStackLayout
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

    private async Task ConfirmCloseActiveGameAsync()
    {
        var closeGame = await DisplayAlertAsync(
            "Luk spil?",
            "Du er midt i et putting-spil. Vil du lukke spillet og miste den igangværende score?",
            "Luk spil",
            "Bliv her");

        if (closeGame)
        {
            await Navigation.PopAsync();
        }
    }

    private async Task NavigateBackAsync()
    {
        if (viewModel.IsActive)
        {
            await ConfirmCloseActiveGameAsync();
            return;
        }

        await Navigation.PopAsync();
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
                await Navigation.PushAsync(page);
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
        finish.Clicked += async (_, _) => await Navigation.PopToRootAsync();

        return Card(new VerticalStackLayout
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

    private static Button StepperButton(string text)
    {
        return new Button
        {
            Text = text,
            WidthRequest = 48,
            HeightRequest = 48,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 20,
            Padding = 0
        };
    }

    private static Entry NumericEntry(string bindingPath)
    {
        var entry = new Entry
        {
            Keyboard = Keyboard.Numeric,
            TextColor = TextColor,
            BackgroundColor = Colors.White,
            HeightRequest = 48
        };
        entry.SetBinding(Entry.TextProperty, bindingPath, BindingMode.TwoWay);
        return entry;
    }

    private static View Field(string labelText, Entry entry)
    {
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = labelText,
                    TextColor = MutedTextColor,
                    FontAttributes = FontAttributes.Bold
                },
                entry
            }
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
