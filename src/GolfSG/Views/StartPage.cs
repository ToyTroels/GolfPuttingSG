using GolfSG.Application.ViewModels;
using GolfSG.Application.Services;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Views;

public sealed class StartPage : ContentPage
{
    private readonly StartViewModel viewModel;
    private readonly IServiceProvider services;
    private Button? evaluationButton;
    private Button? statisticsButton;
    private View? betaNotice;
    private View? betaActions;
    private Button? resumePuttingGameButton;

    public StartPage(StartViewModel viewModel, IServiceProvider services)
    {
        this.viewModel = viewModel;
        this.services = services;
        BindingContext = viewModel;
        Title = "Putting SG";
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible(UiAutomationIds.StartPage, "GolfSG startside");
        BuildLayout();
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "\u2699",
            Order = ToolbarItemOrder.Primary,
            Priority = 0,
            Command = new Command(async () => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<SettingsPage>())))
        });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (evaluationButton is not null)
        {
            evaluationButton.IsVisible = FeatureSettings.EnableBetaFeatures;
        }
        if (statisticsButton is not null)
        {
            statisticsButton.IsVisible = FeatureSettings.EnableBetaFeatures;
        }
        if (betaNotice is not null)
        {
            betaNotice.IsVisible = FeatureSettings.EnableBetaFeatures;
        }
        if (betaActions is not null)
        {
            betaActions.IsVisible = FeatureSettings.EnableBetaFeatures;
        }


        try
        {
            await viewModel.LoadAsync();
            if (resumePuttingGameButton is not null)
                resumePuttingGameButton.IsVisible = await services.GetRequiredService<IActivePuttingGameRepository>().GetAsync() is not null;
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
        var newRoundButton = AppViews.PrimaryButton("Ny runde")
            .Accessible(UiAutomationIds.NewRound, "Start en ny runde");
        newRoundButton.SetBinding(VisualElement.IsEnabledProperty, nameof(StartViewModel.CanInteract));
        newRoundButton.Clicked += async (_, _) => await StartNewRoundAsync();

        var puttingGameButton = AppViews.SecondaryButton("Putting-spil")
            .Accessible(UiAutomationIds.PuttingGames, "Åbn putting-spil");
        puttingGameButton.SetBinding(VisualElement.IsEnabledProperty, nameof(StartViewModel.CanInteract));
        puttingGameButton.Clicked += async (_, _) =>
            await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<PuttingGamesPage>()));

        resumePuttingGameButton = AppViews.PrimaryButton("Fortsæt putting-spil");
        resumePuttingGameButton.IsVisible = false;
        resumePuttingGameButton.SetBinding(VisualElement.IsEnabledProperty, nameof(StartViewModel.CanInteract));
        resumePuttingGameButton.Clicked += async (_, _) => await this.RunNavigationOnceAsync(async () =>
        {
            try
            {
                var session = await services.GetRequiredService<IActivePuttingGameRepository>().GetAsync();
                if (session is null) { resumePuttingGameButton.IsVisible = false; return; }
                var page = services.GetRequiredService<PuttingGamePage>();
                page.Resume(session);
                await Navigation.PushAsync(page);
            }
            catch { await DisplayAlertAsync("Spillet kunne ikke åbnes", "Prøv igen, eller tjek lagring under Indstillinger.", "OK"); }
        });

        var historyButton = AppViews.SecondaryButton("Historik")
            .Accessible(UiAutomationIds.History, "Åbn gemte runder og spil");
        historyButton.SetBinding(VisualElement.IsEnabledProperty, nameof(StartViewModel.CanInteract));
        historyButton.Clicked += async (_, _) =>
            await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<RoundHistoryPage>()));

        evaluationButton = AppViews.SecondaryButton("SG-evaluering · Beta");
        var courseRoundButton = AppViews.SecondaryButton("Banerunde på billede · Beta")
            .Accessible(UiAutomationIds.CoursePractice, "Åbn menuen for banerunder på billede, beta");
        courseRoundButton.SetBinding(VisualElement.IsEnabledProperty, nameof(StartViewModel.CanInteract));
        courseRoundButton.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<CoursePracticeMenuPage>()));
        statisticsButton = AppViews.SecondaryButton("Statistik · Beta");
        statisticsButton.IsVisible = FeatureSettings.EnableBetaFeatures;
        statisticsButton.SetBinding(VisualElement.IsEnabledProperty, nameof(StartViewModel.CanInteract));
        statisticsButton.Clicked += async (_, _) =>
            await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<StatisticsPage>()));
        evaluationButton.SetBinding(VisualElement.IsEnabledProperty, nameof(StartViewModel.CanInteract));
        evaluationButton.IsVisible = FeatureSettings.EnableBetaFeatures;
        evaluationButton.Clicked += async (_, _) =>
            await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<EvaluationPage>()));

        foreach (var button in new[] { statisticsButton, evaluationButton })
        {
            button.BackgroundColor = GolfTheme.Colors.WarningBackground;
            button.BorderColor = GolfTheme.Colors.WarningStroke;
            button.TextColor = GolfTheme.Colors.WarningText;
        }
        betaActions = new VerticalStackLayout
        {
            IsVisible = FeatureSettings.EnableBetaFeatures,
            Spacing = 10,
            Margin = new Thickness(0, 6, 0, 0),
            Children =
            {
                new Label
                {
                    Text = "Beta-funktioner",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GolfTheme.Colors.WarningText
                },
                statisticsButton,
                evaluationButton,
                courseRoundButton
            }
        };

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

        var error = new Border
        {
            BackgroundColor = GolfTheme.Colors.DangerBackground,
            Stroke = GolfTheme.Colors.DangerText,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 12,
            Margin = new Thickness(0, 0, 0, 12),
            Content = new Label
            {
                FontSize = 14,
                TextColor = GolfTheme.Colors.DangerText
            }
        };
        error.SetBinding(IsVisibleProperty, nameof(StartViewModel.HasError));
        ((Label)error.Content).SetBinding(Label.TextProperty, nameof(StartViewModel.ErrorMessage));

        var activeRound = ActiveRoundCard();

        betaNotice = new Border
        {
            IsVisible = FeatureSettings.EnableBetaFeatures,
            BackgroundColor = GolfTheme.Colors.WarningBackground,
            Stroke = GolfTheme.Colors.WarningStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 12,
            Margin = new Thickness(0, 8, 0, 0),
            Content = new Label
            {
                Text = "BETA · Beta-funktioner er aktiveret. Funktioner mærket Beta er under udvikling.",
                FontSize = 14,
                TextColor = GolfTheme.Colors.WarningText
            }
        };

        var insights = InsightDashboard();
        insights.SetBinding(IsVisibleProperty, nameof(StartViewModel.HasInsights));

        var rounds = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = RoundTemplate(OpenRoundAsync, ShowRoundActionsAsync),
            Header = new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    warning,
                    error,
                    AppViews.PageTitle("Putting SG", 34),
                    betaNotice,
                    new Label
                    {
                        Text = "Registrer strokes gained putting mod en generel referencebaseline",
                        FontSize = 16,
                        TextColor = GolfTheme.Colors.MutedText
                    },
                    activeRound,
                    new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children = { newRoundButton, resumePuttingGameButton, puttingGameButton, historyButton, betaActions }
                    }.Margin(new Thickness(0, 16, 0, 18)),
                    insights,
                    new Label
                    {
                        Text = "Seneste runder og spil",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = GolfTheme.Colors.Text
                    }.Margin(new Thickness(0, 0, 0, 8))
                }
            },
            EmptyView = new Label
            {
                Text = "Ingen gemte runder endnu.",
                FontSize = 14,
                TextColor = GolfTheme.Colors.MutedText,
                Margin = new Thickness(0, 8, 0, 0)
            }
        };
        rounds.SetBinding(ItemsView.ItemsSourceProperty, nameof(StartViewModel.RecentRounds));

        Content = rounds.Margin(new Thickness(16));
    }

    private View ActiveRoundCard()
    {
        var detail = new Label
        {
            FontSize = 14,
            TextColor = GolfTheme.Colors.MutedText
        };
        detail.SetBinding(Label.TextProperty, nameof(StartViewModel.ActiveRoundDetailText));

        var resume = AppViews.PrimaryButton("Forts\u00e6t runde")
            .Accessible(UiAutomationIds.ResumeActiveRound, "Fortsæt den igangværende runde");
        resume.Clicked += async (_, _) => await ResumeActiveRoundAsync();

        var abandon = AppViews.SecondaryButton("Opgiv runde")
            .Accessible(UiAutomationIds.AbandonActiveRound, "Opgiv og slet den igangværende runde");
        abandon.Clicked += async (_, _) => await AbandonActiveRoundAsync();

        var card = AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Igangv\u00e6rende runde",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GolfTheme.Colors.Text
                },
                detail,
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 8,
                    Children =
                    {
                        resume.Column(0),
                        abandon.Column(1)
                    }
                }
            }
        }, new Thickness(0, 16, 0, 0));
        card.SetBinding(IsVisibleProperty, nameof(StartViewModel.HasActiveRound));
        return card;
    }

    private async Task StartNewRoundAsync()
    {
        if (viewModel.HasActiveRound)
        {
            await DisplayAlertAsync(
                "Igangv\u00e6rende runde",
                "Forts\u00e6t eller opgiv den igangv\u00e6rende runde, f\u00f8r du starter en ny.",
                "OK");
            return;
        }

        await this.RunNavigationOnceAsync(() => Navigation.PushAsync(services.GetRequiredService<RoundInputPage>()));
    }

    private async Task ResumeActiveRoundAsync()
    {
        try
        {
            var page = services.GetRequiredService<RoundInputPage>();
            if (!await page.LoadActiveAsync())
            {
                return;
            }

            await this.RunNavigationOnceAsync(() => Navigation.PushAsync(page));
            await page.OpenResumeHoleAsync();
        }
        catch (Exception)
        {
            await DisplayAlertAsync(
                "Runden kunne ikke indl\u00e6ses",
                "Pr\u00f8v igen, eller tjek lagring under Indstillinger.",
                "OK");
        }
    }

    private async Task AbandonActiveRoundAsync()
    {
        var confirmed = await DisplayAlertAsync(
            "Opgiv runde?",
            "Den gemte, igangv\u00e6rende runde bliver slettet permanent.",
            "Opgiv runde",
            "Annuller");
        if (!confirmed)
        {
            return;
        }

        if (!await viewModel.AbandonActiveRoundAsync() && viewModel.HasError)
        {
            await DisplayAlertAsync("Runden kunne ikke slettes", viewModel.ErrorMessage, "OK");
        }
    }
    private async Task OpenRoundAsync(RoundListItemViewModel item)
    {
        try
        {
            if (item.Round.CoursePractice is not null)
            {
                await this.RunNavigationOnceAsync(() => Navigation.PushAsync(new CoursePracticeResultPage(item.Round)));
                return;
            }
            var page = services.GetRequiredService<RoundResultPage>();
            await page.LoadAsync(item.Id, openedFromHistory: true);
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

    private static View InsightDashboard()
    {
        var summary = new Label
        {
            FontSize = 13,
            TextColor = GolfTheme.Colors.MutedText
        };
        summary.SetBinding(Label.TextProperty, nameof(StartViewModel.InsightSummaryText));

        return AppViews.Card(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = "Indblik",
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = GolfTheme.Colors.Text
                        },
                        summary
                    }
                },
                InsightMetricGrid()
            }
        }, new Thickness(0, 0, 0, 18));
    }

    private static View InsightMetricGrid()
    {
        return new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                InsightMetric("Form", nameof(StartViewModel.FormInsightValue), nameof(StartViewModel.FormInsightDetail)).Row(0).Column(0),
                InsightMetric("Udvikling", nameof(StartViewModel.TrendInsightValue), nameof(StartViewModel.TrendInsightDetail)).Row(0).Column(1),
                InsightMetric("Styrke", nameof(StartViewModel.StrengthInsightValue), nameof(StartViewModel.StrengthInsightDetail)).Row(1).Column(0),
                InsightMetric("Fokus", nameof(StartViewModel.FocusInsightValue), nameof(StartViewModel.FocusInsightDetail)).Row(1).Column(1)
            }
        };
    }

    private static View InsightMetric(string title, string valueBindingPath, string detailBindingPath)
    {
        var value = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.PrimaryGreen,
            LineBreakMode = LineBreakMode.WordWrap
        };
        value.SetBinding(Label.TextProperty, valueBindingPath);

        var detail = new Label
        {
            FontSize = 12,
            TextColor = GolfTheme.Colors.MutedText,
            LineBreakMode = LineBreakMode.WordWrap
        };
        detail.SetBinding(Label.TextProperty, detailBindingPath);

        return new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 12,
                    TextColor = GolfTheme.Colors.MutedText
                },
                value,
                detail
            }
        };
    }

    private static DataTemplate RoundTemplate(
        Func<RoundListItemViewModel, Task> openRoundAsync,
        Func<RoundListItemViewModel, Task> showRoundActionsAsync)
    {
        return new DataTemplate(() =>
        {
            var date = new Label { FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = GolfTheme.Colors.Text };
            date.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.Date));

            var sg = new Label
            {
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = GolfTheme.Colors.PrimaryGreen,
                HorizontalTextAlignment = TextAlignment.End
            };
            sg.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.TotalSgText));

            var detail = new Label { FontSize = 14, TextColor = GolfTheme.Colors.MutedText };
            detail.SetBinding(Label.TextProperty, nameof(RoundListItemViewModel.DetailText));

            var card = new LongPressBorder
            {
                BackgroundColor = Colors.White,
                Stroke = GolfTheme.Colors.CardStroke,
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
                        detail
                    }
                }
            };

#if ANDROID
            card.ShortPressed += async (_, _) =>
#else
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
#endif
            {
                if (card.BindingContext is RoundListItemViewModel item)
                {
                    await openRoundAsync(item);
                }
            };

#if !ANDROID
            card.GestureRecognizers.Add(tap);
#endif

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
                card.Stroke = isPressed ? GolfTheme.Colors.PrimaryGreen : GolfTheme.Colors.CardStroke;
                _ = card.ScaleToAsync(isPressed ? 0.985 : 1, 80, Easing.CubicOut);
            };

            return card;
        });
    }

}
