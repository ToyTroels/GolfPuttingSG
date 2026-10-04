using GolfSG.Application.ViewModels;

namespace GolfSG.Views;

public sealed class HolePickerPage : ContentPage
{
    public HolePickerPage(
        IEnumerable<HoleInputViewModel> holes,
        int currentHoleNumber,
        Func<HoleInputViewModel, Task> selectHoleAsync)
    {
        BackgroundColor = GolfTheme.Colors.PageBackground;
        this.Accessible("hole-picker.page", "Vælg et hul i runden");

        var close = new Button
        {
            Text = "Luk",
            BackgroundColor = Colors.Transparent,
            TextColor = GolfTheme.Colors.PrimaryGreen,
            HeightRequest = 44
        };
        close.Accessible("hole-picker.close", "Luk hulvælger");
        close.Clicked += async (_, _) => await CloseAsync();

        var numbers = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        for (var column = 0; column < 4; column++)
        {
            numbers.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        var index = 0;
        foreach (var hole in holes)
        {
            if (index % 4 == 0)
            {
                numbers.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            var isCurrent = hole.HoleNumber == currentHoleNumber;
            var number = new Button
            {
                Text = hole.HoleNumber.ToString(),
                HeightRequest = 60,
                CornerRadius = 8,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = isCurrent ? GolfTheme.Colors.PrimaryGreen : Colors.White,
                TextColor = isCurrent ? Colors.White : GolfTheme.Colors.PrimaryGreen,
                BorderColor = GolfTheme.Colors.CardStroke,
                BorderWidth = 1
            };
            number.Accessible($"hole-picker.hole-{hole.HoleNumber}",
                isCurrent ? $"Hul {hole.HoleNumber}, aktuelt hul" : $"Gå til hul {hole.HoleNumber}");
            number.Clicked += async (_, _) => await this.RunNavigationOnceAsync(async () =>
            {
                await Navigation.PopModalAsync();
                await selectHoleAsync(hole);
            });
            numbers.Children.Add(number.Row(index / 4).Column(index % 4));
            index++;
        }

        Content = new Grid
        {
            Padding = 20,
            RowSpacing = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            Children =
            {
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Children =
                    {
                        new Label
                        {
                            Text = "Vælg hul",
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = GolfTheme.Colors.Text,
                            VerticalOptions = LayoutOptions.Center
                        }.Column(0),
                        close.Column(1)
                    }
                }.Row(0),
                new Label
                {
                    Text = "Tryk på et hulnummer. Det aktuelle hul er markeret med grønt.",
                    TextColor = GolfTheme.Colors.MutedText
                }.Row(1),
                new ScrollView { Content = numbers }.Row(2)
            }
        };
    }

    private Task CloseAsync() => this.RunNavigationOnceAsync(() => Navigation.PopModalAsync());

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }
}
