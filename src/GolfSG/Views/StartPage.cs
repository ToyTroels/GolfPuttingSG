using GolfSG.Core;
using GolfSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

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
        try
        {
            await viewModel.LoadAsync();
        }
        catch (Exception)
        {
            await DisplayAlertAsync(
                "Historik kunne ikke indlæses",
                "Prøv igen, eller tjek lagring under Indstillinger.",
                "OK");
        }
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
            Text = "Putting-spil",
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
            Text = "Tour-runde",
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

        var settingsButton = new Button
        {
            Text = "Indstillinger",
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#0F5132"),
            BorderWidth = 1,
            TextColor = Color.FromArgb("#0F5132"),
            CornerRadius = 8
        };
        settingsButton.Clicked += async (_, _) =>
            await Navigation.PushAsync(services.GetRequiredService<SettingsPage>());

        var warning = new Border
        {
            BackgroundColor = Color.FromArgb("#FFF7E0"),
            Stroke = Color.FromArgb("#D59A20"),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 12,
            Margin = new Thickness(0, 0, 0, 12),
            Content = new Label
            {
                FontSize = 14,
                TextColor = Color.FromArgb("#5A3B00")
            }
        };
        warning.SetBinding(IsVisibleProperty, nameof(StartViewModel.ShowRecoveredFromBackupWarning));
        ((Label)warning.Content).SetBinding(Label.TextProperty, nameof(StartViewModel.RecoveredFromBackupWarningText));

        var rounds = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = RoundTemplate(OpenRoundAsync, DeleteRoundAsync),
            Header = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    warning,
                    new Label
                    {
                        Text = "Putting SG",
                        FontSize = 34,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#202421")
                    },
                    new Label
                    {
                        Text = "Registrer strokes gained putting mod PGA Tour-baseline",
                        FontSize = 16,
                        TextColor = Color.FromArgb("#4E5851")
                    },
                    new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { newRoundButton, puttingGameButton, tourRoundGameButton, settingsButton }
                    }.Margin(new Thickness(0, 16, 0, 18)),
                    new Label
                    {
                        Text = "Tidligere runder",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#202421")
                    }.Margin(new Thickness(0, 0, 0, 8))
                }
            },
            EmptyView = new Label
            {
                Text = "Ingen gemte runder endnu.",
                FontSize = 14,
                TextColor = Color.FromArgb("#4E5851"),
                Margin = new Thickness(0, 8, 0, 0)
            }
        };
        rounds.SetBinding(ItemsView.ItemsSourceProperty, nameof(StartViewModel.Rounds));

        Content = rounds.Margin(new Thickness(16));
    }

    private async Task OpenRoundAsync(RoundListItemViewModel item)
    {
        try
        {
            var page = services.GetRequiredService<RoundResultPage>();
            await page.LoadAsync(item.Id);
            await Navigation.PushAsync(page);
        }
        catch (Exception)
        {
            await DisplayAlertAsync(
                "Runde kunne ikke åbnes",
                "Prøv igen, eller tjek lagring under Indstillinger.",
                "OK");
        }
    }

    private async Task DeleteRoundAsync(RoundListItemViewModel item)
    {
        var confirmed = await DisplayAlertAsync(
            "Slet runde",
            $"Vil du slette runden fra {item.Date}?",
            "Slet",
            "Annuller");

        if (!confirmed)
        {
            return;
        }

        try
        {
            await viewModel.DeleteRoundAsync(item);
        }
        catch (Exception)
        {
            await DisplayAlertAsync(
                "Runde kunne ikke slettes",
                "Prøv igen, eller tjek lagring under Indstillinger.",
                "OK");
        }
    }

    private static DataTemplate RoundTemplate(
        Func<RoundListItemViewModel, Task> openRoundAsync,
        Func<RoundListItemViewModel, Task> deleteRoundAsync)
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

            var delete = new Button
            {
                Text = "Slet",
                BackgroundColor = Colors.White,
                BorderColor = Color.FromArgb("#9D2F2F"),
                BorderWidth = 1,
                TextColor = Color.FromArgb("#9D2F2F"),
                CornerRadius = 8,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                HeightRequest = 36,
                Padding = new Thickness(10, 0)
            };
            delete.Clicked += async (sender, _) =>
            {
                if (sender is BindableObject { BindingContext: RoundListItemViewModel item })
                {
                    await deleteRoundAsync(item);
                }
            };

            var card = new Border
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
                                new ColumnDefinition(GridLength.Auto),
                                new ColumnDefinition(GridLength.Auto)
                            },
                            ColumnSpacing = 8,
                            Children = { date.Column(0), sg.Column(1) }
                        },
                        detail,
                        delete
                    }
                }
            };

            var tap = new TapGestureRecognizer();

            tap.Tapped += async (_, _) =>
            {
                if (card.BindingContext is RoundListItemViewModel item)
                {
                    await openRoundAsync(item);
                }
            };

            card.GestureRecognizers.Add(tap);

            return card;
        });
    }
}
