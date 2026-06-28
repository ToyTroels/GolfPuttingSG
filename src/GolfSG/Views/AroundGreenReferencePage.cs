using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public sealed class AroundGreenReferencePage : ContentPage
{
    private const double MetersPerYard = 0.9144;

    private static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
    private static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
    private static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
    private static readonly Color TextColor = Color.FromArgb("#202421");
    private static readonly Color MutedTextColor = Color.FromArgb("#4E5851");

    private readonly IReadOnlyList<AroundGreenReferencePoint> reference;

    public AroundGreenReferencePage(IReadOnlyList<AroundGreenReferencePoint> reference)
    {
        Title = "Omkring green-reference";
        this.reference = reference;
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
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new Label
                {
                    Text = "Omkring green-reference",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TextColor
                }.Row(0),
                new Label
                {
                    Text = "Forventede slag varierer efter startleje. Afstande imellem punkterne beregnes med lineær interpolation.",
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
                var lie = RowLabel();
                lie.SetBinding(Label.TextProperty, nameof(AroundGreenReferenceRow.LieText));

                var distance = RowLabel();
                distance.SetBinding(Label.TextProperty, nameof(AroundGreenReferenceRow.DistanceText));

                var expected = new Label
                {
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = PrimaryGreen,
                    HorizontalTextAlignment = TextAlignment.End,
                    VerticalTextAlignment = TextAlignment.Center
                };
                expected.SetBinding(Label.TextProperty, nameof(AroundGreenReferenceRow.ExpectedText));

                return new Grid
                {
                    Padding = new Thickness(12, 10),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 12,
                    Children =
                    {
                        lie.Column(0),
                        distance.Column(1),
                        expected.Column(2)
                    }
                };
            })
        };
        rows.ItemsSource = reference
            .OrderBy(point => LieSortOrder(point.Lie))
            .ThenBy(point => point.DistanceYards)
            .Select(AroundGreenReferenceRow.From)
            .ToList();

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
                        BackgroundColor = Color.FromArgb("#EEF4EF"),
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        ColumnSpacing = 12,
                        Children =
                        {
                            HeaderLabel("Leje").Column(0),
                            HeaderLabel("Afstand").Column(1),
                            HeaderLabel("Forventede slag").Column(2)
                        }
                    }.Row(0),
                    rows.Row(1)
                }
            }
        };
    }

    private static Label RowLabel()
    {
        return new Label
        {
            FontSize = 15,
            TextColor = TextColor,
            VerticalTextAlignment = TextAlignment.Center
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

    private static int LieSortOrder(ShotLie lie)
    {
        return lie switch
        {
            ShotLie.FairwayCut => 0,
            ShotLie.Rough => 1,
            ShotLie.Sand => 2,
            ShotLie.Recovery => 3,
            _ => 4
        };
    }

    private sealed record AroundGreenReferenceRow(string LieText, string DistanceText, string ExpectedText)
    {
        public static AroundGreenReferenceRow From(AroundGreenReferencePoint point)
        {
            return new AroundGreenReferenceRow(
                FormatLie(point.Lie),
                UiFormat.WholeMeters(point.DistanceYards * MetersPerYard),
                point.ExpectedShots.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("da-DK")));
        }

        private static string FormatLie(ShotLie lie)
        {
            return lie switch
            {
                ShotLie.FairwayCut => "Kortklippet",
                ShotLie.Sand => "Sand",
                ShotLie.Recovery => "Problemlie",
                _ => "Rough"
            };
        }
    }
}
