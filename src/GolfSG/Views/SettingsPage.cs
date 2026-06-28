using GolfSG.Core;
using GolfSG.Services;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class SettingsPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");
    private readonly IRoundRepository repository;

    public SettingsPage(IRoundRepository repository)
    {
        this.repository = repository;
        Title = "Indstillinger";
        BackgroundColor = PageBackground;
        BuildLayout();
    }

    private void BuildLayout()
    {
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Indstillinger",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor
                    },
                    new Label
                    {
                        Text = "SG referenceværdier",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    ReferenceItem(
                        "Putting",
                        "Forventede putts pr. første putt-afstand",
                        () => new StrokesGainedReferencePage(
                            "Putting-reference",
                            "Afstand",
                            "Forventede putts",
                            StrokesGainedCalculator.PuttingReference,
                            useDecimalDistance: true)),
                    ReferenceItem(
                        "Indspil",
                        "Forventede slag pr. indspilsafstand",
                        () => new StrokesGainedReferencePage(
                            "Indspilsreference",
                            "Afstand",
                            "Forventede slag",
                            StrokesGainedCalculator.ApproachReference,
                            useDecimalDistance: false)),
                    ReferenceItem(
                        "Omkring green",
                        "Forventede slag pr. afstand og leje",
                        () => new AroundGreenReferencePage(StrokesGainedCalculator.AroundGreenReference)),
                    new Label
                    {
                        Text = "Data",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = TextColor,
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    ReferenceItem(
                        "Lagring",
                        "Vis aktiv filsti og antal gemte runder",
                        () => new StorageDiagnosticsPage(repository))
                }
            }
        };
    }

    private View ReferenceItem(string title, string subtitle, Func<Page> createPage)
    {
        var card = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = new Grid
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
                        Spacing = 4,
                        Children =
                        {
                            new Label
                            {
                                Text = title,
                                FontSize = 17,
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
                    new Label
                    {
                        Text = ">",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = PrimaryGreen,
                        VerticalTextAlignment = TextAlignment.Center
                    }.Column(1)
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Navigation.PushAsync(createPage());
        card.GestureRecognizers.Add(tap);

        return card;
    }
}
