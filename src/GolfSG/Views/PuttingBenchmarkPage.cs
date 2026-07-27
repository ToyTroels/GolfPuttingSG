using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

public sealed class PuttingBenchmarkPage : ContentPage
{
    private readonly IServiceProvider services;
    private readonly PuttingGameViewModel viewModel;

    public PuttingBenchmarkPage(IServiceProvider services)
    {
        this.services = services;
        viewModel = services.GetRequiredService<PuttingGameViewModel>();
        BindingContext = viewModel;
        Title = "Test";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var distanceOrder = new Picker
        {
            Title = "Afstandsrækkefølge",
            TextColor = GolfTheme.Colors.Text,
            BackgroundColor = Colors.White,
            HeightRequest = 48
        };
        distanceOrder.SetBinding(Picker.ItemsSourceProperty, nameof(PuttingGameViewModel.BenchmarkDistanceOrderOptions));
        distanceOrder.SetBinding(Picker.SelectedItemProperty, nameof(PuttingGameViewModel.SelectedBenchmarkDistanceOrder), BindingMode.TwoWay);

        var benchmarkOptions = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "Afstandsrækkefølge",
                    TextColor = GolfTheme.Colors.MutedText,
                    FontAttributes = FontAttributes.Bold
                },
                distanceOrder,
                new Label
                {
                    Text = "Bell-curve benchmark",
                    TextColor = GolfTheme.Colors.MutedText,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 8, 0, 0)
                },
                BenchmarkButton(nameof(PuttingGameViewModel.ShortBenchmarkText), viewModel.StartShortBenchmark),
                BenchmarkButton(nameof(PuttingGameViewModel.NormalBenchmarkText), viewModel.StartNormalBenchmark),
                BenchmarkButton(nameof(PuttingGameViewModel.ThoroughBenchmarkText), viewModel.StartThoroughBenchmark)
            }
        };

        if (FeatureSettings.EnableBetaFeatures)
        {
            benchmarkOptions.Children.Add(new BoxView
            {
                HeightRequest = 1,
                BackgroundColor = GolfTheme.Colors.CardStroke,
                Margin = new Thickness(0, 4)
            });
            benchmarkOptions.Children.Add(new Label
            {
                Text = "Ladder benchmark",
                TextColor = GolfTheme.Colors.MutedText,
                FontAttributes = FontAttributes.Bold
            });
            benchmarkOptions.Children.Add(BenchmarkButton(nameof(PuttingGameViewModel.ShortLadderBenchmarkText), viewModel.StartShortLadderBenchmark));
            benchmarkOptions.Children.Add(BenchmarkButton(nameof(PuttingGameViewModel.NormalLadderBenchmarkText), viewModel.StartNormalLadderBenchmark));
            benchmarkOptions.Children.Add(BenchmarkButton(nameof(PuttingGameViewModel.ThoroughLadderBenchmarkText), viewModel.StartThoroughLadderBenchmark));
        }

        benchmarkOptions.Children.Add(BackButton());

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 10,
                Children =
                {
                    AppViews.PageTitle("Test"),
                    AppViews.Card(new VerticalStackLayout
                    {
                        Children = { benchmarkOptions }
                    }, new Thickness(0, 0, 0, 10))
                }
            }
        };
    }

    private Button BenchmarkButton(string bindingPath, Action startBenchmark)
    {
        var button = AppViews.PrimaryButton("");
        button.SetBinding(Button.TextProperty, bindingPath);
        button.Clicked += async (_, _) =>
        {
            startBenchmark();
            await OpenBenchmarkGameAsync();
        };
        return button;
    }

    private Button BackButton()
    {
        var button = AppViews.SecondaryButton("Tilbage");
        button.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        return button;
    }

    private async Task OpenBenchmarkGameAsync()
    {
        var page = new PuttingGamePage(viewModel);
        await this.RunNavigationOnceAsync(() => Navigation.PushAsync(page));
        if (Navigation.NavigationStack.Contains(this))
        {
            Navigation.RemovePage(this);
        }
    }

}
