using System.Globalization;
using GolfSG.Core;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public static class UiFormat
{
    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");

    public static string Sg(double value) => value.ToString("+0.00;-0.00", DanishCulture);

    public static string Meters(double value) => $"{value.ToString("0.0", DanishCulture)} m";

    public static string WholeMeters(double value) => $"{value.ToString("0", DanishCulture)} m";

    public static string PuttingDistance(double distanceMeters, PuttingDistanceUnitPreference unit) =>
        $"{ToPreferredPuttingDistance(distanceMeters, unit).ToString("0.0", DanishCulture)} {PuttingDistanceUnitText(unit)}";

    public static string WholePuttingDistance(double distanceMeters, PuttingDistanceUnitPreference unit) =>
        $"{ToPreferredPuttingDistance(distanceMeters, unit).ToString("0", DanishCulture)} {PuttingDistanceUnitText(unit)}";

    public static string PuttingDistanceUnitText(PuttingDistanceUnitPreference unit) =>
        unit == PuttingDistanceUnitPreference.Feet ? "ft" : "m";

    public static double ToPreferredPuttingDistance(double distanceMeters, PuttingDistanceUnitPreference unit) =>
        unit == PuttingDistanceUnitPreference.Feet
            ? DistanceConversions.MetersToFeet(distanceMeters)
            : distanceMeters;

    public static double FromPreferredPuttingDistance(double distance, PuttingDistanceUnitPreference unit) =>
        unit == PuttingDistanceUnitPreference.Feet
            ? DistanceConversions.FeetToMeters(distance)
            : distance;

    public static string Date(DateTime value) => value.ToString("dd. MMM yyyy", DanishCulture);
}