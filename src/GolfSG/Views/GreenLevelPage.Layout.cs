using GolfSG.Application.Services;

namespace GolfSG.Views;

public sealed partial class GreenLevelPage
{
    private void BuildLayout()
    {
        SemanticProperties.SetDescription(level, "Vaterpas. Boblen viser den høje side i den færdige måling.");
        start.AutomationId = "green-level.start";
        calibrate.AutomationId = "green-level.calibrate";
        cancel.AutomationId = "green-level.cancel";
        cancel.IsVisible = false;
        start.Clicked += async (_, _) => await MeasureAsync(false);
        calibrate.Clicked += async (_, _) =>
        {
            if (measurement is not null) return;
            if (await DisplayAlertAsync("Kalibrér", "Læg telefonen på en kendt vandret, fast flade, ikke greenens skråning. Tryk Start og slip telefonen. Kalibreringen starter et halvt sekund efter trykket.", "Start", "Annuller"))
                await MeasureAsync(true);
        };
        cancel.Clicked += (_, _) => CancelMeasurement("Målingen er annulleret. Tryk Start måling for at prøve igen.");
        reset.Clicked += (_, _) =>
        {
            rightOffset = forwardOffset = 0;
            Preferences.Default.Remove("green-level.right-offset");
            Preferences.Default.Remove("green-level.forward-offset");
            UpdateCalibrationText();
            ClearResult();
            status.Text = "Kalibrering nulstillet · tryk Start måling.";
        };
        UpdateCalibrationText();
        var directionArrow = new Label
        {
            Text = "MOD\nHULLET\n→", FontSize = 24, FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.PrimaryGreen,
            HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center,
            AutomationId = "green-level.hole-direction"
        };
        var levelPanel = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) },
            Children = { level, directionArrow }
        };
        Grid.SetRow(level, 1);
        Grid.SetColumn(level, 1);
        void UpdateDirection()
        {
            var direction = directionPicker.SelectedIndex;
            if (direction < 0 || direction > 3) return;
            holeDirection = (GreenLevelHoleDirection)direction;
            directionArrow.Text = direction switch
            {
                0 => "↑ MOD HULLET", 1 => "MOD\nHULLET\n→",
                2 => "MOD HULLET ↓", _ => "MOD\nHULLET\n←"
            };
            directionArrow.WidthRequest = direction is 1 or 3 ? 85 : -1;
            Grid.SetRow(directionArrow, direction == 0 ? 0 : direction == 2 ? 2 : 1);
            Grid.SetColumn(directionArrow, direction == 1 ? 2 : direction == 3 ? 0 : 1);
            SemanticProperties.SetDescription(directionArrow, $"Peg denne kant mod hullet: {directionPicker.Items[direction]}");
            Preferences.Default.Set("green-level.hole-direction", direction);
        }
        var savedDirection = Preferences.Default.Get("green-level.hole-direction", 1);
        directionPicker.SelectedIndex = savedDirection is >= 0 and <= 3 ? savedDirection : 1;
        UpdateDirection();
        directionPicker.SelectedIndexChanged += (_, _) =>
        {
            UpdateDirection();
            ClearResult();
            progress.IsVisible = false;
            status.Text = "Retning ændret · peg den valgte kant mod hullet og tryk Start måling.";
        };
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 20, Spacing = 14,
            Children =
            {
                AppViews.PageTitle("Greenens hældning"),
                new Label { Text = "Læg telefonen med skærmen opad. Vælg hvilken kant der peger mod hullet, og følg pilen. Tryk Start måling og slip telefonen. Venstre/højre ses altid fra bolden mod hullet." },
                new Label { Text = "Hvilken kant peger mod hullet?" }, directionPicker,
                levelPanel, sideways, forward, status, progress, start, cancel, calibrate, calibrationStatus, reset,
                new Label { Text = "Et halvt sekunds pause efter trykket. Målingen stopper, så snart der er nok stabile aflæsninger, dog senest efter 3 sekunder. Bjælken viser måletiden, ikke nøjagtigheden. Resultatet bliver stående, så du kan løfte telefonen." },
                new Label { Text = "Boblen viser den høje side. 1 % svarer til 1 cm pr. meter. Boblen stopper ved kanten ved 5 %." },
                new Label { Text = "Øvelsesværktøj: Måler kun hældningen under telefonen, ikke hele puttets break. Kamerabule, cover og græs kan påvirke resultatet. Decimaler er ikke en garanti for nøjagtighed." }
            }
        } };
    }

}
