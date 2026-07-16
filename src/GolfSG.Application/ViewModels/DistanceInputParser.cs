using System.Globalization;

namespace GolfSG.ViewModels;

public static class DistanceInputParser
{
    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    public static string NormalizeDecimalSeparator(string? text) => (text ?? string.Empty).Replace(',', '.');

    public static double ParseOrZero(string? text)
    {
        return TryParse(text, out var value) && value >= 0 ? value : 0;
    }

    public static bool TryParse(string? text, out double distance)
    {
        return double.TryParse(
            NormalizeDecimalSeparator(text),
            NumberStyles.Float,
            InvariantCulture,
            out distance);
    }

    public static string FormatStored(double value, int decimals)
    {
        return value.ToString(decimals == 0 ? "0" : "0.#", InvariantCulture);
    }

    public static string FormatSlider(double value, int decimals)
    {
        var rounded = Math.Round(Math.Max(0, value), decimals);
        return rounded <= 0
            ? string.Empty
            : rounded.ToString(decimals == 0 ? "0" : "0.#", InvariantCulture);
    }
}
