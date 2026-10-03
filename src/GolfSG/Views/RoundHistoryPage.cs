using GolfSG.Application.ViewModels;
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

    private readonly RoundHistoryViewModel viewModel;
    private readonly IServiceProvider services;

    public RoundHistoryPage(RoundHistoryViewModel viewModel, IServiceProvider services)
    {
        this.viewModel = viewModel;
        this.services = services;
        BindingContext = viewModel;
        Title = "Historik";
        BackgroundColor = PageBackground;
        this.Accessible(UiAutomationIds.HistoryPage, "Historik over gemte runder og spil");
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
                "Historik kunne ikke indl\u00e6ses",
                "Pr\u00f8v igen, eller tjek lagring under Indstillinger.",
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
        warning.SetBinding(IsVisibleProperty, nameof(RoundHistoryViewModel.ShowRecoveredFromBackupWarning));
        ((Label)warning.Content).SetBinding(Label.TextProperty, nameof(RoundHistoryViewModel.RecoveredFromBackupWarningText));

        var type = HistoryPicker("Type")
            .Accessible(UiAutomationIds.HistoryType, "Filtrer historik efter type");
        type.SetBinding(Picker.ItemsSourceProperty, nameof(RoundHistoryViewModel.HistoryTypeOptions));
        type.SetBinding(Picker.SelectedItemProperty, nameof(RoundHistoryViewModel.SelectedHistoryType), BindingMode.TwoWay);

        var category = HistoryPicker("SG-kategori")
            .Accessible(UiAutomationIds.HistoryCategory, "Filtrer historik efter strokes gained-kategori");
        category.SetBinding(Picker.ItemsSourceProperty, nameof(RoundHistoryViewModel.HistoryCategoryOptions));
        category.SetBinding(Picker.SelectedItemProperty, nameof(RoundHistoryViewModel.SelectedHistoryCategory), BindingMode.TwoWay);

        var period = HistoryPicker("Periode")
            .Accessible(UiAutomationIds.HistoryPeriod, "Filtrer historik efter periode");
        period.SetBinding(Picker.ItemsSourceProperty, nameof(RoundHistoryViewModel.HistoryPeriodOptions));
        period.SetBinding(Picker.SelectedItemProperty, nameof(RoundHistoryViewModel.SelectedHistoryPeriod), BindingMode.TwoWay);

        var sort = HistoryPicker("Sortering")
            .Accessible(UiAutomationIds.HistorySort, "Sorter historikken");
        sort.SetBinding(Picker.ItemsSourceProperty, nameof(RoundHistoryViewModel.HistorySortOptions));
        sort.SetBinding(Picker.SelectedItemProperty, nameof(RoundHistoryViewModel.SelectedHistorySort), BindingMode.TwoWay);

        var summary = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = MutedTextColor,
            Margin = new Thickness(0, 0, 0, 10)
        };
        summary.SetBinding(Label.TextProperty, nameof(RoundHistoryViewModel.HistorySummaryText));

        var empty = new Label
        {
            FontSize = 14,
            TextColor = MutedTextColor,
            Margin = new Thickness(0, 8, 0, 0)
        };
        empty.SetBinding(Label.TextProperty, nameof(RoundHistoryViewModel.HistoryEmptyText));
        empty.SetBinding(IsVisibleProperty, nameof(RoundHistoryViewModel.IsHistoryEmpty));

        var rounds = new VerticalStackLayout
        {
            Spacing = 0
        }.Accessible(UiAutomationIds.HistoryList, "Gemte runder og spil");
        BindableLayout.SetItemTemplate(rounds, RoundTemplate(OpenRoundAsync, ShowRoundActionsAsync));
        rounds.SetBinding(BindableLayout.ItemsSourceProperty, nameof(RoundHistoryViewModel.Rounds));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
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
                        Margin = new Thickness(0, 0, 0, 10)
                    },
                    FilterPanel(type, category, period, sort),
                    summary,
                    ComparisonPanel(),
                    empty,
                    rounds
                }
            }
        }.Margin(new Thickness(16));
    }

    private async Task OpenRoundAsync(RoundListItemViewModel item)
    {
        try
        {
            var page = services.GetRequiredService<RoundResultPage>();
            await page.LoadAsync(item.Id);
            await this.RunNavigationOnceAsync(() => Navigation.PushAsync(page));
        }
        catch (Exception)
        {
            await DisplayAlertAsync(
                "Runde kunne ikke \u00e5bnes",
                "Pr\u00f8v igen, eller tjek lagring under Indstillinger.",
                "OK");
        }
    }

    private async Task DeleteRoundAsync(RoundListItemViewModel item) =>
        await this.RunActionOnceAsync(() => DeleteRoundCoreAsync(item));

    private async Task DeleteRoundCoreAsync(RoundListItemViewModel item)
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
                "Pr\u00f8v igen, eller tjek lagring under Indstillinger.",
                "OK");
        }
    }

    private async Task ShowRoundActionsAsync(RoundListItemViewModel item)
    {
        await this.RunNavigationOnceAsync(() => Navigation.PushModalAsync(new RoundActionsSheetPage(item, DeleteRoundAsync), false));
    }

    private static View FilterPanel(Picker type, Picker category, Picker period, Picker sort)
    {
        return AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                Field("Vis", type),
                Field("SG", category),
                Field("Periode", period),
                Field("Sortering", sort)
            }
        }, new Thickness(0, 4, 0, 10));
    }

    private static View ComparisonPanel()
    {
        var text = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor
        };
        text.SetBinding(Label.TextProperty, nameof(RoundHistoryViewModel.HistoryComparisonText));

        var panel = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = "Sammenligning",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = MutedTextColor
                },
                text
            }
        }, new Thickness(0, 0, 0, 10));
        panel.SetBinding(IsVisibleProperty, nameof(RoundHistoryViewModel.HasHistoryComparison));
        return panel;
    }

    private static Picker HistoryPicker(string title)
    {
        return new Picker
        {
            Title = title,
            TextColor = TextColor,
            BackgroundColor = Colors.White,
            HeightRequest = 44
        };
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
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = MutedTextColor
                },
                input
            }
        };
    }

    private static DataTemplate RoundTemplate(
        Func<RoundListItemViewModel, Task> openRoundAsync,
        Func<RoundListItemViewModel, Task> showRoundActionsAsync)
    {
        return new DataTemplate(() =>
        {
            var date = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold };
            date.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.Date));

            var type = new Label
            {
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = PrimaryGreen,
                VerticalTextAlignment = TextAlignment.Center
            };
            type.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.HistoryTypeText));

            var sg = new Label
            {
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = PrimaryGreen,
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.DisplaySgText));

            var sgCaption = new Label
            {
                FontSize = 11,
                TextColor = MutedTextColor,
                HorizontalTextAlignment = TextAlignment.End,
                LineBreakMode = LineBreakMode.NoWrap
            };
            sgCaption.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.DisplaySgCaption));

            var sgStack = new VerticalStackLayout
            {
                Spacing = 0,
                HorizontalOptions = LayoutOptions.End,
                Children = { sg, sgCaption }
            };

            var detail = new Label { FontSize = 14, TextColor = MutedTextColor };
            detail.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.DetailText));

            var comparison = new Label
            {
                FontSize = 12,
                TextColor = MutedTextColor
            };
            comparison.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.ComparisonText));
            comparison.SetBinding(IsVisibleProperty, nameof(RoundListItemViewModel.HasComparisonText));

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
                                new ColumnDefinition(GridLength.Auto),
                                new ColumnDefinition(GridLength.Auto)
                            },
                            ColumnSpacing = 8,
                            Children = { date.Column(0), type.Column(1), sgStack.Column(2) }
                        },
                        detail,
                        comparison
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
