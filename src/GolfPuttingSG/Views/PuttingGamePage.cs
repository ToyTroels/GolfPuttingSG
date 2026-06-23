using GolfPuttingSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfPuttingSG.Views;

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
        Title = "Putting Game";
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    public void Start(string mode)
    {
        viewModel.Start(mode);
        Title = viewModel.GameTitle;
    }

    private void BuildLayout()
    {
        var submitButton = new Button
        {
            Text = "Submit result",
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold
        };
        submitButton.Clicked += async (_, _) => completedRoundId = await viewModel.SubmitAsync();

        var activePanel = ActivePanel(submitButton);
        activePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsActive));

        var completePanel = CompletePanel();
        completePanel.SetBinding(VisualElement.IsVisibleProperty, nameof(PuttingGameViewModel.IsComplete));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 10,
                Children =
                {
                    GameTitleLabel(),
                    activePanel,
                    completePanel
                }
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

    private View ActivePanel(Button submitButton)
    {
        var progress = new Label
        {
            FontSize = 15,
            TextColor = MutedTextColor
        };
        progress.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.ProgressText));

        var distance = new Label
        {
            FontSize = 56,
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

        return new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Card(new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        progress,
                        distance,
                        new Label
                        {
                            Text = "Putts used",
                            TextColor = MutedTextColor,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new HorizontalStackLayout
                        {
                            Spacing = 12,
                            HorizontalOptions = LayoutOptions.Center,
                            Children = { minus, putts, plus }
                        },
                        currentResult
                    }
                }),
                RunningTotalPanel(),
                submitButton
            }
        };
    }

    private View RunningTotalPanel()
    {
        var totalPutts = BoundLabel(nameof(PuttingGameViewModel.TotalPuttsText), "Total putts: {0}");
        var runningSg = BoundLabel(nameof(PuttingGameViewModel.RunningSgText), "Putting SG: {0}");
        var remaining = new Label
        {
            FontSize = 14,
            TextColor = TextColor
        };
        remaining.SetBinding(Label.TextProperty, nameof(PuttingGameViewModel.RemainingDistancesText));

        return Card(new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "Running total",
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                totalPutts,
                runningSg,
                remaining
            }
        });
    }

    private View CompletePanel()
    {
        var viewResult = new Button
        {
            Text = "View saved result",
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

            var page = Handler!.MauiContext!.Services.GetRequiredService<RoundResultPage>();
            await page.LoadAsync(completedRoundId);
            await Navigation.PushAsync(page);
        };

        var finish = new Button
        {
            Text = "Done",
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
                    Text = "Summary",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                BoundLabel(nameof(PuttingGameViewModel.TotalPuttsText), "Total putts: {0}"),
                BoundLabel(nameof(PuttingGameViewModel.TargetPuttsText), "Target putts: {0}"),
                BoundLabel(nameof(PuttingGameViewModel.FinalSgText), "Final putting SG: {0}", true),
                BoundLabel(nameof(PuttingGameViewModel.BestResultText), "Best result: {0}"),
                BoundLabel(nameof(PuttingGameViewModel.WorstResultText), "Worst result: {0}"),
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
