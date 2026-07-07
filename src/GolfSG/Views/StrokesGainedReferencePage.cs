using GolfSG.Core;
using GolfSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class StrokesGainedReferencePage : ContentPage
{
    private static readonly Color PageBackground = GolfTheme.Colors.PageBackground;
    private static readonly Color CardStroke = GolfTheme.Colors.CardStroke;
    private static readonly Color PrimaryGreen = GolfTheme.Colors.PrimaryGreen;
    private static readonly Color TextColor = GolfTheme.Colors.Text;
    private static readonly Color MutedTextColor = GolfTheme.Colors.MutedText;

    private readonly string distanceHeader;
    private readonly string expectedHeader;
    private readonly IReadOnlyList<StrokesGainedReferencePoint> reference;
    private readonly bool useDecimalDistance;

    public StrokesGainedReferencePage(
        string title,
        string distanceHeader,
        string expectedHeader,
        IReadOnlyList<StrokesGainedReferencePoint> reference,
        bool useDecimalDistance)
    {
        Title = title;
        this.distanceHeader = distanceHeader;
        this.expectedHeader = expectedHeader;
        this.reference = reference;
        this.useDecimalDistance = useDecimalDistance;
        BackgroundColor = PageBackground;
        BuildLayout(title);
    }

    private void BuildLayout(string title)
    {
        Content = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                }.Row(0),
                new Label
                {
                    Text = "Disse punkter er de lagrede referenceværdier. Afstande imellem punkterne beregnes med lineær interpolation.",
                    FontSize = 14,
                    TextColor = MutedTextColor
                }.Row(1).Margin(new Thickness(0, 4, 0, 14)),
                ReferenceTable().Row(2)
            }
        };
    }

    private View ReferenceTable()
    {
        var rows = new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var distance = new Label
                {
                    FontSize = 15,
                    TextColor = TextColor,
                    VerticalTextAlignment = TextAlignment.Center
                };
                distance.SetBinding(Label.TextProperty, nameof(ReferenceRow.DistanceText));

                var expected = new Label
                {
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = PrimaryGreen,
                    HorizontalTextAlignment = TextAlignment.End,
                    VerticalTextAlignment = TextAlignment.Center
                };
                expected.SetBinding(Label.TextProperty, nameof(ReferenceRow.ExpectedText));

                return new Grid
                {
                    Padding = new Thickness(12, 10),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Children =
                    {
                        distance.Column(0),
                        expected.Column(1)
                    }
                };
            })
        };
        rows.ItemsSource = reference.Select(point => ReferenceRow.From(point, useDecimalDistance)).ToList();

        return new Border
        {
            BackgroundColor = Colors.White,
            Stroke = CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Star)
                },
                Children =
                {
                    new Grid
                    {
                        Padding = new Thickness(12, 10),
                        BackgroundColor = GolfTheme.Colors.SoftTableGreen,
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        Children =
                        {
                            HeaderLabel(distanceHeader).Column(0),
                            HeaderLabel(expectedHeader).Column(1)
                        }
                    }.Row(0),
                    rows.Row(1)
                }
            }
        };
    }

    private static Label HeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = MutedTextColor
        };
    }

    private sealed record ReferenceRow(string DistanceText, string ExpectedText)
    {
        public static ReferenceRow From(StrokesGainedReferencePoint point, bool useDecimalDistance)
        {
            var distance = useDecimalDistance
                ? UiFormat.Meters(point.DistanceMeters)
                : UiFormat.WholeMeters(point.DistanceMeters);

            return new ReferenceRow(distance, point.ExpectedShots.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("da-DK")));
        }
    }
}
