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

        var putts = new Label
        {
            FontSize = 44,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 96,
            HeightRequest = 64
        };
        putts.SetBinding(Label.TextProperty, nameof(HoleInputViewModel.Putts));

        var minus = StepperButton("-");
        minus.Clicked += (_, _) => viewModel.DecreasePutts();

        var plus = StepperButton("+");
        plus.Clicked += (_, _) => viewModel.IncreasePutts();

        var distance = new Entry
        {
            Placeholder = "0,0",
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Colors.Transparent,
            TextColor = TextColor,
            PlaceholderColor = MutedTextColor,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            FontSize = 44,
            HorizontalTextAlignment = TextAlignment.Center,
            HeightRequest = 92
        };
        distance.SetBinding(Entry.TextProperty, nameof(HoleInputViewModel.DistanceText), BindingMode.TwoWay);

        var sg = new Label
        {
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center
        };
        sg.SetBinding(Label.TextProperty, new Binding(nameof(HoleInputViewModel.StrokesGainedText), stringFormat: "SG {0}"));

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
            Text = GetNextHole() is null ? "Til oversigt" : "Næste hul",
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
                new VerticalStackLayout
                {
                    Spacing = 4,
                    Children = { title, subtitle }
                }.Row(0),
                PuttsPanel(minus, putts, plus).Row(1).Margin(new Thickness(0, 22, 0, 0)),
                DistancePanel(distance, sg).Row(2),
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

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), () => distance.Focus());
    }

    private async Task GoToNextHoleOrOverviewAsync()
    {
        var nextHole = GetNextHole();
        if (nextHole is null)
        {
            await Navigation.PopAsync();
            return;
        }

        var nextPage = new HoleEntryPage(roundViewModel, nextHole);
        Navigation.InsertPageBefore(nextPage, this);
        await Navigation.PopAsync(animated: false);
    }

    private HoleInputViewModel? GetNextHole()
    {
        var currentIndex = roundViewModel.Holes.IndexOf(viewModel);
        var nextIndex = currentIndex + 1;
        return currentIndex >= 0 && nextIndex < roundViewModel.Holes.Count
            ? roundViewModel.Holes[nextIndex]
            : null;
    }

    private static View PuttsPanel(Button minus, Label putts, Button plus)
    {
        return Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "Antal putts",
                    FontSize = 15,
                    TextColor = MutedTextColor,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new HorizontalStackLayout
                {
                    Spacing = 18,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { minus, putts, plus }
                }
            }
        });
    }

    private static View DistancePanel(Entry distance, Label sg)
    {
        return new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new Label
                {
                    Text = "Første putt-afstand",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor,
                    HorizontalTextAlignment = TextAlignment.Center
                }.Row(1),
                new Border
                {
                    BackgroundColor = InputBackground,
                    Stroke = CardStroke,
                    StrokeThickness = 2,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Padding = new Thickness(18, 0),
                    Margin = new Thickness(0, 14, 0, 18),
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
                                Text = "m",
                                FontSize = 28,
                                TextColor = MutedTextColor,
                                VerticalTextAlignment = TextAlignment.Center,
                                Margin = new Thickness(10, 0, 0, 0)
                            }.Column(1)
                        }
                    }
                }.Row(2),
                sg.Row(3)
            }
        };
    }

    private static Button StepperButton(string text)
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
