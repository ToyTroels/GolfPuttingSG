namespace GolfSG.Views;

public static class GolfTheme
{
    public static class Colors
    {
        public static readonly Color PageBackground = Color.FromArgb("#F4F1E8");
        public static readonly Color CardBackground = Microsoft.Maui.Graphics.Colors.White;
        public static readonly Color CardStroke = Color.FromArgb("#DCE4DD");
        public static readonly Color InputBackground = Color.FromArgb("#FAFBFA");
        public static readonly Color PrimaryGreen = Color.FromArgb("#0F5132");
        public static readonly Color SoftGreen = Color.FromArgb("#EEF5EF");
        public static readonly Color SoftPressedGreen = Color.FromArgb("#F6FAF6");
        public static readonly Color SoftTableGreen = Color.FromArgb("#EEF4EF");
        public static readonly Color Text = Color.FromArgb("#202421");
        public static readonly Color MutedText = Color.FromArgb("#4E5851");
        public static readonly Color WarningBackground = Color.FromArgb("#FFF7E0");
        public static readonly Color WarningStroke = Color.FromArgb("#D59A20");
        public static readonly Color WarningText = Color.FromArgb("#5A3B00");
        public static readonly Color DangerText = Color.FromArgb("#9D2F2F");
        public static readonly Color DangerBackground = Color.FromArgb("#FFF5F5");
        public static readonly Color Divider = Color.FromArgb("#E4E8E4");
        public static readonly Color Scrim = Color.FromArgb("#99000000");
        public static readonly Color SheetHandle = Color.FromArgb("#CDD4CE");
        public static readonly Color LoadingBackground = Color.FromArgb("#0B241D");
    }

    public static class Spacing
    {
        public const double PagePadding = 16;
        public const double CardPadding = 14;
        public const double CompactCardPadding = 12;
        public const double SectionGap = 14;
        public const double ItemGap = 10;
        public const double TightGap = 6;
    }

    public static class Radius
    {
        public const double Card = 8;
        public const double LargeCard = 10;
        public const int Button = 8;
    }

    public static class Sizes
    {
        public const double ButtonHeight = 50;
        public const double TitleFont = 30;
        public const double PageTitleFont = 26;
        public const double BodyFont = 14;
    }
}
