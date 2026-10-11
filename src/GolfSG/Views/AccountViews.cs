namespace GolfSG.Views;

internal static class AccountViews
{
    public static Entry TextEntry(string placeholder, Keyboard? keyboard = null, bool isPassword = false)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            Keyboard = keyboard ?? Keyboard.Default,
            IsPassword = isPassword,
            IsTextPredictionEnabled = false,
            IsSpellCheckEnabled = false,
            TextColor = GolfTheme.Colors.Text,
            PlaceholderColor = GolfTheme.Colors.MutedText,
            BackgroundColor = GolfTheme.Colors.InputBackground,
            FontFamily = "OpenSansRegular",
            FontSize = 16,
            MinimumHeightRequest = GolfTheme.Sizes.ButtonHeight
        };
        VisualStateManager.SetVisualStateGroups(entry, new VisualStateGroupList
        {
            new VisualStateGroup
            {
                Name = "CommonStates",
                States =
                {
                    new VisualState { Name = "Normal" },
                    new VisualState
                    {
                        Name = "Disabled",
                        Setters =
                        {
                            new Setter { Property = Entry.TextColorProperty, Value = GolfTheme.Colors.MutedText },
                            new Setter { Property = Entry.PlaceholderColorProperty, Value = GolfTheme.Colors.MutedText }
                        }
                    }
                }
            }
        });
        return entry;
    }
}
