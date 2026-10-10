using Microsoft.Maui.Controls.Shapes;

namespace GolfSG.Views;

internal static class CoursePracticeViews
{
    public static string FormatDistanceMeters(double metres) => $"{Math.Round(metres, MidpointRounding.AwayFromZero):0} m";

    public static void ConfigurePage(ContentPage page)
    {
        // These pages use a light course palette even when the device uses dark mode.
        page.Resources.Add(new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.TextColorProperty, Value = GolfTheme.Colors.Text },
                new Setter { Property = Label.FontSizeProperty, Value = 16d },
                new Setter { Property = Label.FontFamilyProperty, Value = "OpenSansRegular" }
            }
        });
    }

    public static View Field(string labelText, View input)
    {
        switch (input)
        {
            case Entry entry:
                entry.TextColor = GolfTheme.Colors.Text;
                entry.PlaceholderColor = GolfTheme.Colors.MutedText;
                entry.BackgroundColor = GolfTheme.Colors.CardBackground;
                entry.FontSize = 16;
                entry.MinimumHeightRequest = 48;
                SetDisabledState(entry,
                    new Setter { Property = Entry.TextColorProperty, Value = GolfTheme.Colors.MutedText },
                    new Setter { Property = Entry.PlaceholderColorProperty, Value = GolfTheme.Colors.MutedText },
                    new Setter { Property = Entry.BackgroundColorProperty, Value = GolfTheme.Colors.SoftTableGreen });
                break;
            case Picker picker:
                picker.TextColor = GolfTheme.Colors.Text;
                picker.TitleColor = GolfTheme.Colors.MutedText;
                picker.BackgroundColor = GolfTheme.Colors.CardBackground;
                picker.FontSize = 16;
                picker.MinimumHeightRequest = 48;
                SetDisabledState(picker,
                    new Setter { Property = Picker.TextColorProperty, Value = GolfTheme.Colors.MutedText },
                    new Setter { Property = Picker.TitleColorProperty, Value = GolfTheme.Colors.MutedText },
                    new Setter { Property = Picker.BackgroundColorProperty, Value = GolfTheme.Colors.SoftTableGreen });
                break;
        }
        var label = AppViews.FieldLabel(labelText);
        label.TextColor = GolfTheme.Colors.Text;
        label.FontSize = 16;
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                label,
                new Border
                {
                    BackgroundColor = GolfTheme.Colors.CardBackground,
                    Stroke = GolfTheme.Colors.PrimaryGreen,
                    StrokeThickness = 1.5,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(12, 4),
                    Content = input
                }
            }
        };
    }

    public static Button PrimaryButton(string text) => Button(text, true);
    public static Button SecondaryButton(string text) => Button(text, false);

    private static Button Button(string text, bool primary)
    {
        var button = primary ? AppViews.PrimaryButton(text) : AppViews.SecondaryButton(text);
        button.FontSize = 16;
        button.FontAttributes = FontAttributes.Bold;
        button.HeightRequest = -1;
        button.MinimumHeightRequest = 56;
        button.Padding = new Thickness(12, 12);
        button.BorderColor = GolfTheme.Colors.PrimaryGreen;
        button.BorderWidth = primary ? 0 : 2;
        SetDisabledState(button,
            new Setter { Property = Microsoft.Maui.Controls.Button.TextColorProperty, Value = GolfTheme.Colors.MutedText },
            new Setter { Property = VisualElement.BackgroundColorProperty, Value = GolfTheme.Colors.SoftTableGreen });
        return button;
    }

    private static void SetDisabledState(VisualElement view, params Setter[] setters)
    {
        var disabled = new VisualState { Name = "Disabled" };
        foreach (var setter in setters) disabled.Setters.Add(setter);
        VisualStateManager.SetVisualStateGroups(view, new VisualStateGroupList
        {
            new VisualStateGroup { Name = "CommonStates", States = { new VisualState { Name = "Normal" }, disabled } }
        });
    }
}
