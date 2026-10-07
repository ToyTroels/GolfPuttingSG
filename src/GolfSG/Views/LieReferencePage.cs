using System.Globalization;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class LieReferencePage : ContentPage
{
    public LieReferencePage(string title, IReadOnlyList<LieReferencePoint> reference,
        PuttingDistanceUnitPreference distanceUnit)
    {
        Title = title;
        BackgroundColor = GolfTheme.Colors.PageBackground;
        var lies = reference.Select(point => point.Lie).Distinct().ToList();
        var table = new Grid { ColumnSpacing = 0, RowSpacing = 0 };
        var distances = new Grid { ColumnSpacing = 0, RowSpacing = 0 };
        distances.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(95)));
        foreach (var lie in lies)
            table.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(110)));

        void AddCell(string text, int row, int column, bool header = false)
        {
            var label = new Label
            {
                Text = text,
                Padding = new Thickness(10, 12),
                FontSize = 15,
                LineBreakMode = LineBreakMode.NoWrap,
                FontAttributes = header ? FontAttributes.Bold : FontAttributes.None,
                TextColor = header || column == 0 ? GolfTheme.Colors.Text : GolfTheme.Colors.PrimaryGreen,
                BackgroundColor = header ? GolfTheme.Colors.SoftTableGreen :
                    row % 2 == 0 ? GolfTheme.Colors.PageBackground : Colors.White,
                HorizontalTextAlignment = column == 0 ? TextAlignment.Start : TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, column == 0 ? 0 : column - 1);
            (column == 0 ? distances : table).Children.Add(label);
        }

        table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        distances.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddCell("Afstand", 0, 0, true);
        for (var column = 0; column < lies.Count; column++)
            AddCell(LieLabel(lies[column]), 0, column + 1, true);

        var row = 1;
        foreach (var distance in reference.Select(point => point.DistanceYards).Distinct().Order())
        {
            table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            distances.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddCell(UiFormat.PuttingDistance(distance * 0.9144, distanceUnit), row, 0);
            for (var column = 0; column < lies.Count; column++)
            {
                var point = reference.FirstOrDefault(point => point.Lie == lies[column] && point.DistanceYards == distance);
                AddCell(point?.ExpectedShots.ToString("0.00", CultureInfo.GetCultureInfo("da-DK")) ?? "—",
                    row, column + 1);
            }
            row++;
        }

        Content = new Grid
        {
            Padding = 16,
            RowSpacing = 12,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) },
            Children =
            {
                new Label { Text = title, FontSize = 28, FontAttributes = FontAttributes.Bold,
                    TextColor = GolfTheme.Colors.Text }.Row(0),
                new Label
                {
                    Text = "Forventede slag efter afstand og leje. Tabellen viser beregningens lagrede referencepunkter. — betyder intet lagret punkt ved denne afstand; beregningen interpolerer mellem punkterne. Rul sidelæns for at se alle lejer.",
                    FontSize = 14, TextColor = GolfTheme.Colors.MutedText
                }.Row(1),
                new ScrollView
                {
                    Orientation = ScrollOrientation.Vertical,
                    Content = new Grid
                    {
                        ColumnSpacing = 0,
                        ColumnDefinitions = { new(new GridLength(95)), new(GridLength.Star) },
                        Children =
                        {
                            distances.Column(0),
                            new ScrollView
                            {
                                Orientation = ScrollOrientation.Horizontal,
                                Content = table
                            }.Column(1)
                        }
                    }
                }.Row(2)
            }
        };
    }

    private static string LieLabel(ShotLie lie) => lie switch
    {
        ShotLie.Tee => "Tee",
        ShotLie.Fairway => "Fairway",
        ShotLie.FairwayCut => "Kortklippet",
        ShotLie.Rough => "Rough",
        ShotLie.Sand => "Sand",
        ShotLie.Recovery => "Problemlie",
        _ => lie.ToString()
    };
}
