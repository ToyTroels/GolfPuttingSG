using GolfSG.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class RoundHistoryPage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;

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
            BackgroundColor = GolfTheme.Colors.WarningBackground,
            Stroke = GolfTheme.Colors.WarningStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 12,
            Margin = new Thickness(0, 0, 0, 12),
            Content = new Label
            {
                FontSize = 14,
                TextColor = GolfTheme.Colors.WarningText
            }
        };
        warning.SetBinding(IsVisibleProperty, nameof(StartViewModel.ShowRecoveredFromBackupWarning));
        ((Label)warning.Content).SetBinding(Label.TextProperty, nameof(StartViewModel.RecoveredFromBackupWarningText));

        var rounds = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = RoundTemplate(OpenRoundAsync, ShowRoundActionsAsync),
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

    private async Task ShowRoundActionsAsync(RoundListItemViewModel item)
    {
        await Navigation.PushModalAsync(new RoundActionsSheetPage(item, DeleteRoundAsync), false);
    }

    private static DataTemplate RoundTemplate(
        Func<RoundListItemViewModel, Task> openRoundAsync,
        Func<RoundListItemViewModel, Task> showRoundActionsAsync)
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

            var card = new LongPressBorder
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
                        detail
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

            card.LongPressed += async (_, _) =>
            {
                if (card.BindingContext is RoundListItemViewModel item)
                {
                    await showRoundActionsAsync(item);
                }
            };
            card.PressedChanged += (_, isPressed) =>
            {
                card.BackgroundColor = isPressed ? GolfTheme.Colors.SoftPressedGreen : Colors.White;
                card.Stroke = isPressed ? PrimaryGreen : CardStroke;
                _ = card.ScaleToAsync(isPressed ? 0.985 : 1, 80, Easing.CubicOut);
            };

            return card;
        });
    }
}
