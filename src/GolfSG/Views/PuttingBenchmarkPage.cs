using GolfSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class PuttingBenchmarkPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly PuttingGameViewModel viewModel;

    public PuttingBenchmarkPage(PuttingGameViewModel viewModel)
    {
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = "Benchmark";
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var distanceOrder = new Picker
        {
            Title = "Afstandsrækkefølge",
            TextColor = TextColor,
            BackgroundColor = Colors.White,
            HeightRequest = 48
        };
        distanceOrder.SetBinding(Picker.ItemsSourceProperty, nameof(PuttingGameViewModel.BenchmarkDistanceOrderOptions));
        distanceOrder.SetBinding(Picker.SelectedItemProperty, nameof(PuttingGameViewModel.SelectedBenchmarkDistanceOrder), BindingMode.TwoWay);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "Benchmark",
                        FontSize = 26,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
                    Card(new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label
                            {
                                Text = "Afstandsrækkefølge",
                                TextColor = MutedTextColor,
                                FontAttributes = FontAttributes.Bold
                            },
                            distanceOrder,
                            new Label
                            {
                                Text = "Benchmark-længde",
                                TextColor = MutedTextColor,
                                FontAttributes = FontAttributes.Bold,
                                Margin = new Thickness(0, 8, 0, 0)
                            },
                            BenchmarkButton(nameof(PuttingGameViewModel.ShortBenchmarkText), viewModel.StartShortBenchmark),
                            BenchmarkButton(nameof(PuttingGameViewModel.NormalBenchmarkText), viewModel.StartNormalBenchmark),
                            BenchmarkButton(nameof(PuttingGameViewModel.ThoroughBenchmarkText), viewModel.StartThoroughBenchmark),
                            BackButton()
                        }
                    })
                }
            }
        };
    }

    protected override bool OnBackButtonPressed()
    {
        _ = ReturnToPuttingGameAsync();
        return true;
    }

    private Button BenchmarkButton(string bindingPath, Action startBenchmark)
    {
        var button = new Button
        {
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        button.SetBinding(Button.TextProperty, bindingPath);
        button.Clicked += async (_, _) =>
        {
            startBenchmark();
            await ReturnToPuttingGameAsync();
        };
        return button;
    }

    private Button BackButton()
    {
        var button = new Button
        {
            Text = "Tilbage",
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            CornerRadius = 8,
            HeightRequest = 50
        };
        button.Clicked += async (_, _) => await ReturnToPuttingGameAsync();
        return button;
    }

    private async Task ReturnToPuttingGameAsync()
    {
        while (Navigation.NavigationStack.LastOrDefault() is not null &&
            Navigation.NavigationStack.LastOrDefault() is not PuttingGamePage)
        {
            await Navigation.PopAsync();
        }
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
