using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

public static class AppViews
{
    public static Border Card(View content, Thickness? margin = null, double padding = GolfTheme.Spacing.CardPadding)
    {
        return new Border
        {
            BackgroundColor = GolfTheme.Colors.CardBackground,
            Stroke = GolfTheme.Colors.CardStroke,
            StrokeShape = new RoundRectangle { CornerRadius = GolfTheme.Radius.Card },
            Padding = padding,
            Margin = margin ?? default,
            Content = content
        };
    }

    public static Button PrimaryButton(string text)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = GolfTheme.Colors.PrimaryGreen,
            TextColor = Microsoft.Maui.Graphics.Colors.White,
            CornerRadius = GolfTheme.Radius.Button,
            HeightRequest = GolfTheme.Sizes.ButtonHeight,
            FontAttributes = FontAttributes.Bold
        };
    }

    public static Button SecondaryButton(string text)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = GolfTheme.Colors.CardBackground,
            BorderColor = GolfTheme.Colors.PrimaryGreen,
            BorderWidth = 1,
            TextColor = GolfTheme.Colors.PrimaryGreen,
            CornerRadius = GolfTheme.Radius.Button,
            HeightRequest = GolfTheme.Sizes.ButtonHeight
        };
    }

    public static Label PageTitle(string text, double fontSize = GolfTheme.Sizes.TitleFont)
    {
        return new Label
        {
            Text = text,
            FontSize = fontSize,
            FontAttributes = FontAttributes.Bold,
            TextColor = GolfTheme.Colors.Text
        };
    }
}
