using System.Globalization;

namespace GolfSG.ViewModels;

public static class UiFormat
{
    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");

    public static string Sg(double value) => value.ToString("+0.00;-0.00", DanishCulture);

    public static string Meters(double value) => $"{value.ToString("0.0", DanishCulture)} m";

    public static string WholeMeters(double value) => $"{value.ToString("0", DanishCulture)} m";

    public static string Date(DateTime value) => value.ToString("dd. MMM yyyy", DanishCulture);
}
