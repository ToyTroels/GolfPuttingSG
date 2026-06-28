using GolfSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class RoundHistoryPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color DangerRed = Color.FromArgb("#9D2F2F");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly StartViewModel viewModel;
    private readonly IServiceProvider services;

    public RoundHistoryPage(StartViewModel viewModel, IServiceProvider services)
    {
        this.viewModel = viewModel;
        this.services = services;
        BindingContext = viewModel;
        Title = "Historik";
        BackgroundColor = PageBackground;
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
                Spacing = 4,
                Children =
                {
                    warning,
                    new Label
                    {
                        Text = "Historik",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
                    new Label
                    {
                        Text = "Gemte runder og putting-spil",
                        FontSize = 15,
                        TextColor = MutedTextColor,
                        Margin = new Thickness(0, 0, 0, 14)
                    }
                }
            },
            EmptyView = new Label
            {
                Text = "Ingen gemte runder endnu.",
                FontSize = 14,
                TextColor = MutedTextColor,
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
                TextColor = PrimaryGreen,
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.TotalSgText));

            var detail = new Label { FontSize = 14, TextColor = MutedTextColor };
            detail.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.DetailText));

            var delete = new Button
            {
                Text = "Slet",
                BackgroundColor = Colors.White,
                BorderColor = DangerRed,
                BorderWidth = 1,
                TextColor = DangerRed,
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
                Stroke = CardStroke,
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
