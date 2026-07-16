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

    public static Button StepperButton(string text, double size = 48, double fontSize = 18)
    {
        return new Button
        {
            Text = text,
            WidthRequest = size,
            HeightRequest = size,
            CornerRadius = GolfTheme.Radius.Button,
            BackgroundColor = GolfTheme.Colors.PrimaryGreen,
            TextColor = Microsoft.Maui.Graphics.Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = fontSize,
            Padding = 0
        };
    }

    public static Entry NumericEntry(string bindingPath)
    {
        var entry = new Entry
        {
            Keyboard = Keyboard.Numeric,
            TextColor = GolfTheme.Colors.Text,
            BackgroundColor = GolfTheme.Colors.CardBackground,
            HeightRequest = GolfTheme.Sizes.ButtonHeight
        };
        entry.SetBinding(Entry.TextProperty, bindingPath, BindingMode.TwoWay);
        return entry;
    }

    public static View Field(string labelText, View input) =>
        Field(FieldLabel(labelText), input);

    public static View BoundField(string labelBindingPath, View input)
    {
        var label = FieldLabel(string.Empty);
        label.SetBinding(Label.TextProperty, labelBindingPath);
        return Field(label, input);
    }

    public static View Field(Label label, View input) =>
        new VerticalStackLayout
        {
            Spacing = 4,
            Children = { label, input }
        };

    public static Label FieldLabel(string text) =>
        new()
        {
            Text = text,
            TextColor = GolfTheme.Colors.MutedText,
            FontAttributes = FontAttributes.Bold
        };

    public static Border FormCard(View content, bool includeBottomMargin = true) =>
        Card(
            content,
            includeBottomMargin ? new Thickness(0, 0, 0, 10) : default,
            padding: 14);
}
