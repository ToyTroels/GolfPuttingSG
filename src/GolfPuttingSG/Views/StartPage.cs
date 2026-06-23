using GolfPuttingSG.Core;
using GolfPuttingSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace GolfPuttingSG.Views;

public sealed class StartPage : ContentPage
{
    private readonly StartViewModel viewModel;
    private readonly IServiceProvider services;

    public StartPage(StartViewModel viewModel, IServiceProvider services)
    {
        this.viewModel = viewModel;
        this.services = services;
        BindingContext = viewModel;
        Title = "Putting SG";
        BackgroundColor = Color.FromArgb("#F4F1E8");
        BuildLayout();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await viewModel.LoadAsync();
    }

    private void BuildLayout()
    {
        var newRoundButton = new Button
        {
            Text = "Ny runde",
            BackgroundColor = Color.FromArgb("#0F5132"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        newRoundButton.Clicked += async (_, _) =>
            await Navigation.PushAsync(services.GetRequiredService<RoundInputPage>());

        var puttingGameButton = new Button
        {
            Text = "Putting Game",
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#0F5132"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#0F5132"),
            CornerRadius = 8
        };
        puttingGameButton.Clicked += async (_, _) =>
        {
            var page = services.GetRequiredService<PuttingGamePage>();
            page.Start(PuttingGame.LadderMode);
            await Navigation.PushAsync(page);
        };

        var tourRoundGameButton = new Button
        {
            Text = "Tour Round Game",
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#0F5132"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#0F5132"),
            CornerRadius = 8
        };
        tourRoundGameButton.Clicked += async (_, _) =>
        {
            var page = services.GetRequiredService<PuttingGamePage>();
            page.Start(PuttingGame.TourRoundMode);
            await Navigation.PushAsync(page);
        };

        var rounds = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemTemplate = RoundTemplate()
        };
        rounds.SetBinding(ItemsView.ItemsSourceProperty, nameof(StartViewModel.Rounds));
        rounds.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not RoundListItemViewModel item)
            {
                return;
            }

            rounds.SelectedItem = null;
            var page = services.GetRequiredService<RoundResultPage>();
            await page.LoadAsync(item.Id);
            await Navigation.PushAsync(page);
        };

        Content = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new Label
                {
                    Text = "Putting SG",
                    FontSize = 34,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#202421")
                }.Row(0),
                new Label
                {
                    Text = "Track strokes gained putting mod PGA Tour-baseline",
                    FontSize = 16,
                    TextColor = Color.FromArgb("#4E5851")
                }.Row(1),
                new VerticalStackLayout
                {
                    Spacing = 10,
                    Children = { newRoundButton, puttingGameButton, tourRoundGameButton }
                }.Row(2).Margin(new Thickness(0, 16, 0, 18)),
                new Label
                {
                    Text = "Tidligere runder",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#202421")
                }.Row(3),
                rounds.Row(4).Margin(new Thickness(0, 8, 0, 0))
            }
        };
    }

    private static DataTemplate RoundTemplate()
    {
        return new DataTemplate(() =>
        {
            var date = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
            date.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.Date));

            var sg = new Label
            {
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#0F5132"),
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.TotalSgText));

            var detail = new Label { FontSize = 14, TextColor = Color.FromArgb("#4E5851") };
            detail.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.DetailText));

            return new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#DCE4DD"),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = 14,
                Margin = new Thickness(0, 0, 0, 10),
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition(GridLength.Star),
                                new ColumnDefinition(GridLength.Auto)
                            },
                            Children = { date.Column(0), sg.Column(1) }
                        },
                        detail
                    }
                }
            };
        });
    }
}
