using GolfPuttingSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace GolfPuttingSG.Views;

public sealed class HoleEntryPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color InputBackground = Color.FromArgb("#FAFBFA");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly RoundInputViewModel roundViewModel;
    private readonly HoleInputViewModel viewModel;

    public HoleEntryPage(RoundInputViewModel roundViewModel, HoleInputViewModel viewModel)
    {
        this.roundViewModel = roundViewModel;
        this.viewModel = viewModel;
        BindingContext = viewModel;
        Title = viewModel.Title;
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var previousHole = GetPreviousHole();
        var nextHole = GetNextHole();

        var previous = new Button
        {
            Text = "Forrige hul",
            HeightRequest = 40,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold,
            IsVisible = previousHole is not null
        };
        previous.Clicked += async (_, _) => await GoToHoleAsync(GetPreviousHole());

        var title = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center
        };
        title.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Title));

        var subtitle = new Label
        {
            FontSize = 14,
            TextColor = MutedTextColor,
            HorizontalTextAlignment = TextAlignment.Center
        };
        subtitle.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Subtitle));

        var puttingDistance = DistanceEntry("0,0", nextHole);
        puttingDistance.SetBinding(Entry.TextProperty, nameof(HoleInputViewModel.DistanceText), BindingMode.TwoWay);
        puttingDistance.Completed += async (_, _) =>
        {
            puttingDistance.Unfocus();
            await GoToNextHoleOrOverviewAsync();
        };

        var putts = CountLabel();
        putts.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Putts));

        var puttMinus = RoundStepperButton("-");
        puttMinus.Clicked += (_, _) => viewModel.DecreasePutts();

        var puttPlus = RoundStepperButton("+");
        puttPlus.Clicked += (_, _) => viewModel.IncreasePutts();

        var puttingSg = SgLabel();
        puttingSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedPuttingText), stringFormat: "SG Putting {0}"));

        var approachDistance = DistanceEntry("0", nextHole);
        approachDistance.SetBinding(Entry.TextProperty, nameof(HoleInputViewModel.ApproachDistanceText), BindingMode.TwoWay);
        approachDistance.Completed += async (_, _) =>
        {
            approachDistance.Unfocus();
            await GoToNextHoleOrOverviewAsync();
        };

        var approachShots = CountLabel();
        approachShots.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.ApproachShots));

        var approachMinus = RoundStepperButton("-");
        approachMinus.Clicked += (_, _) => viewModel.DecreaseApproachShots();

        var approachPlus = RoundStepperButton("+");
        approachPlus.Clicked += (_, _) => viewModel.IncreaseApproachShots();

        var approachSg = SgLabel();
        approachSg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedApproachText), stringFormat: "SG Approach {0}"));

        var puttingSection = PuttingSection(puttMinus, putts, puttPlus, puttingDistance, puttingSg);
        puttingSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.TrackPutting));

        var approachSection = ApproachSection(approachMinus, approachShots, approachPlus, approachDistance, approachSg);
        approachSection.SetBinding(VisualElement.IsVisibleProperty, nameof(HoleInputViewModel.TrackApproach));

        var done = new Button
        {
            Text = "Færdig",
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = PrimaryGreen,
            BorderWidth = 1,
            TextColor = PrimaryGreen,
            FontAttributes = FontAttributes.Bold
        };
        done.Clicked += async (_, _) => await Navigation.PopAsync();

        var next = new Button
        {
            Text = nextHole is null ? "Til oversigt" : "Næste hul",
            HeightRequest = 52,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold
        };
        next.Clicked += async (_, _) => await GoToNextHoleOrOverviewAsync();

        Content = new Grid
        {
            Padding = 20,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                previous.Row(0),
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children = { title, subtitle }
                }.Row(1).Margin(new Thickness(0, 12, 0, 0)),
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 16,
                        Children = { puttingSection, approachSection }
                    }
                }.Row(2).Margin(new Thickness(0, 22, 0, 18)),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 10,
                    Children =
                    {
                        done.Column(0),
                        next.Column(1)
                    }
                }.Row(3)
            }
        };

        var focusTarget = viewModel.TrackPutting ? puttingDistance : approachDistance;
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () => focusTarget.Focus());
    }

    private async Task GoToNextHoleOrOverviewAsync()
    {
        var nextHole = GetNextHole();
        if (nextHole is null)
        {
            await Navigation.PopAsync();
            return;
        }

        await GoToHoleAsync(nextHole);
    }

    private async Task GoToHoleAsync(HoleInputViewModel? hole)
    {
        if (hole is null)
        {
            return;
        }

        var page = new HoleEntryPage(roundViewModel, hole);
        Navigation.InsertPageBefore(page, this);
        await Navigation.PopAsync(animated: false);
    }

    private HoleInputViewModel? GetPreviousHole()
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var previousIndex = currentIndex - 1;
        return previousIndex >= 0 ? roundViewModel.Holes[previousIndex] : null;
    }

    private HoleInputViewModel? GetNextHole()
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var nextIndex = currentIndex + 1;
        return currentIndex >= 0 && nextIndex < roundViewModel.Holes.Count
            ? roundViewModel.Holes[nextIndex]
            : null;
    }

    private static View PuttingSection(Button minus, Label putts, Button plus, Entry distance, Label sg)
    {
        return Card(new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                CounterPanel("Antal putts", minus, putts, plus),
                DistancePanel("Første putt-afstand", "m", distance),
                sg
            }
        });
    }

    private static View ApproachSection(Button minus, Label shots, Button plus, Entry distance, Label sg)
    {
        return Card(new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                CounterPanel("Approach-slag brugt", minus, shots, plus),
                DistancePanel("Approach-afstand", "m", distance),
                sg
            }
        });
    }

    private static View CounterPanel(string title, Button minus, Label count, Button plus)
    {
        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 15,
                    TextColor = MutedTextColor,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new HorizontalStackLayout
                {
                    Spacing = 18,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { minus, count, plus }
                }
            }
        };
    }

    private static View DistancePanel(string title, string unit, Entry distance)
    {
        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Border
                {
                    BackgroundColor = InputBackground,
                    Stroke = CardStroke,
                    StrokeThickness = 2,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Padding = new Thickness(18, 0),
                    Content = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Children =
                        {
                            distance.Column(0),
                            new Label
                            {
                                Text = unit,
                                FontSize = 28,
                                TextColor = MutedTextColor,
                                VerticalTextAlignment = TextAlignment.Center,
                                Margin = new Thickness(10, 0, 0, 0)
                            }.Column(1)
                        }
                    }
                }
            }
        };
    }

    private static Entry DistanceEntry(string placeholder, HoleInputViewModel? nextHole)
    {
        return new Entry
        {
            Placeholder = placeholder,
            Keyboard = Keyboard.Numeric,
            ReturnType = nextHole is null ? ReturnType.Done : ReturnType.Next,
            BackgroundColor = Colors.Transparent,
            TextColor = TextColor,
            PlaceholderColor = MutedTextColor,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            FontSize = 44,
            HorizontalTextAlignment = TextAlignment.Center,
            HeightRequest = 92
        };
    }

    private static Label CountLabel()
    {
        return new Label
        {
            FontSize = 44,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 96,
            HeightRequest = 64
        };
    }

    private static Label SgLabel()
    {
        return new Label
        {
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center
        };
    }

    private static Button RoundStepperButton(string text)
    {
        return new Button
        {
            Text = text,
            WidthRequest = 64,
            HeightRequest = 64,
            CornerRadius = 32,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 28,
            Padding = 0
        };
    }

    private static Border Card(View content)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 16,
            Content = content
        };
    }
}
