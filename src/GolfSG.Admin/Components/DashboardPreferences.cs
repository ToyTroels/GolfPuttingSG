using GolfSG.Application.Server;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GolfSG.Admin.Components;

public sealed record DashboardPreferences(string Range = "all", string From = "", string To = "", string Lines = "total,putting,approach,around")
{
    public static readonly string[] Keys = ["total", "putting", "approach", "around"];
    public bool Shows(string key) => Lines.Split(',').Contains(key, StringComparer.Ordinal);
    public string? Error => Range == "custom" && (!Date(From, out var from) || !Date(To, out var to) || from > to)
        ? "Choose a valid start and end date, with the start on or before the end." : null;
    private static bool Date(string value, out DateOnly date) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    public static DashboardPreferences Read(HttpContext context, string account)
    {
        var name = "GolfSG.View." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account)))[..24];
        DashboardPreferences preferences = new();
        if (context.Request.Cookies.TryGetValue(name, out var stored) && stored.Length < 200)
        {
            var parts = stored.Split('|');
            if (parts.Length == 4) preferences = Normalize(parts[0], parts[1], parts[2], parts[3]);
        }
        if (context.Request.Query["apply"] == "1")
        {
            preferences = Normalize(context.Request.Query["range"].ToString(), context.Request.Query["from"].ToString(),
                context.Request.Query["to"].ToString(), string.Join(",", context.Request.Query["lines"].ToArray()));
            if (preferences.Error is null)
                context.Response.Cookies.Append(name, string.Join("|", preferences.Range, preferences.From, preferences.To, preferences.Lines),
                    new CookieOptions { HttpOnly = true, Secure = context.Request.IsHttps, SameSite = SameSiteMode.Lax, Path = "/my", MaxAge = TimeSpan.FromDays(365), IsEssential = true });
        }
        return preferences;
    }
    private static DashboardPreferences Normalize(string range, string from, string to, string lines) => new(
        range is "30" or "90" or "custom" ? range : "all",
        from.Length <= 10 ? from : "", to.Length <= 10 ? to : "",
        string.Join(",", Keys.Where(key => lines.Split(',').Contains(key, StringComparer.Ordinal))));

    public PersonalPerformance Filter(PersonalPerformance data, DateOnly today)
    {
        if (Error is not null) return data;
        DateOnly? from = Range == "30" ? today.AddDays(-29) : Range == "90" ? today.AddDays(-89) : Range == "custom" ? DateOnly.ParseExact(From, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
        DateOnly? to = Range is "30" or "90" ? today : Range == "custom" ? DateOnly.ParseExact(To, "yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
        var sessions = (data.Sessions ?? data.Recent.Concat(data.Trend).DistinctBy(r => r.Id).ToArray())
            .Where(r => (from is null || DateOnly.FromDateTime(r.Date) >= from) && (to is null || DateOnly.FromDateTime(r.Date) <= to))
            .OrderByDescending(r => r.Date).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
        var normal = sessions.Where(r => !r.Practice).ToArray();
        var scored = normal.Where(r => r.Completed > 0).ToArray();
        var categories = new List<PersonalCategoryScore>();
        foreach (var (name, select) in new (string, Func<PersonalRoundSummary, double?>)[] {
            ("Putting", r => r.PuttingSg), ("Approach", r => r.ApproachSg), ("Around green", r => r.AroundGreenSg) })
        {
            var scores = normal.Select(select).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            if (scores.Length > 0) categories.Add(new(name, scores.Length, scores.Average()));
        }
        return data with { NormalRounds = normal.Length, PracticeRounds = sessions.Length - normal.Length,
            TotalSg = scored.Sum(r => r.TotalSg), AverageSg = scored.Length == 0 ? null : scored.Average(r => r.TotalSg),
            Recent = sessions.Take(12).ToArray(), Trend = scored.Take(12).Reverse().ToArray(), Categories = categories, Sessions = sessions };
    }
}
