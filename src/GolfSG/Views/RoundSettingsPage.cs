using GolfSG.Application.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace GolfSG.Views;

public sealed class RoundSettingsPage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;

    private readonly RoundInputViewModel viewModel;
    private readonly int minimumHoleCount;

    public RoundSettingsPage(RoundInputViewModel viewModel, int minimumHoleCount = 1)
    {
        this.viewModel = viewModel;
        this.minimumHoleCount = Math.Max(1, minimumHoleCount);
        BindingContext = viewModel;
        Title = "Rundeindstillinger";
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    private void BuildLayout()
    {
        Content = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 14,
                        Children =
                        {
                            new Label
                            {
                                Text = "Rundeindstillinger",
                                FontSize = 28,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = TextColor
                            },
                            HoleCountPanel(),
                            TrackingPanel()
                        }
                    }
                }.Row(0),
                SaveButton().Row(1).Margin(new Thickness(0, 12, 0, 0))
            }
        };
    }

    private Button SaveButton()
    {
        var button = new Button
        {
            Text = "Gem indstillinger",
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold
        };
        button.Clicked += async (_, _) => await this.RunNavigationOnceAsync(() => Navigation.PopAsync());
        return button;
    }

    private View HoleCountPanel()
    {
        var holeCount = new Label
        {
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = TextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            WidthRequest = 132,
            HeightRequest = 48
        };
        holeCount.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.HoleCountText));

        var minus = StepperButton("-");
        minus.Clicked += async (_, _) =>
        {
            if (viewModel.HoleCount <= minimumHoleCount)
            {
                await DisplayAlertAsync(
                    "Kan ikke forkorte",
                    $"Runden kan ikke være kortere end hul {minimumHoleCount}, mens du redigerer dette hul.",
                    "OK");
                return;
            }

            viewModel.DecreaseHoleCount();
        };

        var plus = StepperButton("+");
        plus.Clicked += (_, _) => viewModel.IncreaseHoleCount();

        var progress = new Label
        {
            FontSize = 13,
            TextColor = MutedTextColor
        };
        progress.SetBinding(Label.TextProperty, nameof(RoundInputViewModel.RoundProgressText));

        return Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                SectionHeader("Rundelængde", "Vælg hvor mange huller runden skal have"),
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { minus, holeCount, plus }
                },
                QuickHoleButtons(),
                progress
            }
        });
    }

    private View QuickHoleButtons()
    {
        return new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            Children =
            {
                QuickHoleButton("9 huller", 9),
                QuickHoleButton("18 huller", 18)
            }
        };
    }

    private Button QuickHoleButton(string text, int targetCount)
    {
        var button = new Button
        {
            Text = text,
            HeightRequest = 42,
            MinimumWidthRequest = 110,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = CardStroke,
            BorderWidth = 1,
            TextColor = TextColor,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 0, 8, 8)
        };
        button.Clicked += async (_, _) =>
        {
            if (targetCount < minimumHoleCount)
            {
                await DisplayAlertAsync(
                    "Kan ikke forkorte",
                    $"Runden kan ikke være kortere end hul {minimumHoleCount}, mens du redigerer dette hul.",
                    "OK");
                return;
            }

            while (viewModel.HoleCount < targetCount)
            {
                viewModel.IncreaseHoleCount();
            }

            while (viewModel.HoleCount > targetCount)
            {
                var before = viewModel.HoleCount;
                viewModel.DecreaseHoleCount();
                if (viewModel.HoleCount == before)
                {
                    break;
                }
            }
        };
        return button;
    }

    private View TrackingPanel()
    {
        var putting = TrackingSwitch(nameof(RoundInputViewModel.TrackPutting));
        var approach = TrackingSwitch(nameof(RoundInputViewModel.TrackApproach));
        var aroundGreen = TrackingSwitch(nameof(RoundInputViewModel.TrackAroundGreen));

        return Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                SectionHeader("Registrering", "Vælg hvilke dele af runden du vil tracke"),
                TrackingRow("Putting", "Første putt-afstand og antal putts", putting),
                TrackingRow("Approach", "Start, slutposition og strafslag", approach),
                TrackingRow("Omkring green", "Chip, pitch, bunker og problemlie ved green", aroundGreen)
            }
        });
    }

    private static Switch TrackingSwitch(string bindingPath)
    {
        var toggle = new Switch
        {
            OnColor = PrimaryGreen,
            ThumbColor = Colors.White
        };
        toggle.SetBinding(Switch.IsToggledProperty, bindingPath, BindingMode.TwoWay);
        return toggle;
    }

    private static View SectionHeader(string title, string subtitle)
    {
        return new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                },
                new Label
                {
                    Text = subtitle,
                    FontSize = 13,
                    TextColor = MutedTextColor
                }
            }
        };
    }

    private static View TrackingRow(string title, string subtitle, Switch toggle)
    {
        return new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = title,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = TextColor
                        },
                        new Label
                        {
                            Text = subtitle,
                            FontSize = 13,
                            TextColor = MutedTextColor
                        }
                    }
                }.Column(0),
                toggle.Column(1)
            }
        };
    }

    private static Button StepperButton(string text)
    {
        return new Button
        {
            Text = text,
            WidthRequest = 48,
            HeightRequest = 48,
            CornerRadius = 8,
            BackgroundColor = PrimaryGreen,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 18,
            Padding = 0
        };
    }

    private static Border Card(View content)
    {
        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = content
        };
    }
}
