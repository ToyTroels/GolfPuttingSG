using GolfSG.Admin.Components;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Http;

namespace GolfSG.Admin.Tests;

[TestClass]
public sealed class DashboardPreferencesTests
{
    private static PersonalRoundSummary Session(string id, DateTime date, double sg, bool practice = false) =>
        new(id, id, date, practice, practice ? "Practice" : "Normal", 1, 9, sg, 2, sg, null, null);

    [TestMethod]
    public void DateFilterUsesFullWindowBeforeSelectingRecentTrendAndKeepsPracticeSeparate()
    {
        var sessions = Enumerable.Range(1, 30).Select(i => Session(i.ToString(), new DateTime(2026, 10, i), i, i == 6)).ToArray();
        var data = new PersonalPerformance(30, 30, 0, 29, 1, 0, 0, [], [], [], sessions);
        var filtered = new DashboardPreferences("custom", "2026-10-05", "2026-10-07").Filter(data, new(2026, 10, 17));
        Assert.AreEqual(2, filtered.NormalRounds);
        Assert.AreEqual(1, filtered.PracticeRounds);
        Assert.AreEqual(6d, filtered.AverageSg);
        CollectionAssert.AreEqual(new[] { "5", "7" }, filtered.Trend.Select(r => r.Id).ToArray());
        Assert.AreEqual(6d, filtered.Categories.Single().AverageSg);
        var empty = new DashboardPreferences("custom", "2025-01-01", "2025-01-02").Filter(data, new(2026, 10, 17));
        Assert.IsNull(empty.AverageSg);
        Assert.IsEmpty(empty.Trend);
    }

    [TestMethod]
    public void PreferencesPersistPerAccountAndInvalidDatesDoNotOverwriteSavedView()
    {
        var request = new DefaultHttpContext();
        request.Request.Scheme = "https";
        request.Request.QueryString = new("?apply=1&range=custom&from=2026-10-01&to=2026-10-17&lines=approach&lines=around&lines=invalid");
        var saved = DashboardPreferences.Read(request, "alice");
        Assert.IsTrue(saved.Shows("approach"));
        Assert.IsFalse(saved.Shows("total"));
        var cookie = request.Response.Headers.SetCookie.ToString().Split(';')[0];
        var next = new DefaultHttpContext();
        next.Request.Headers.Cookie = cookie;
        Assert.AreEqual(saved, DashboardPreferences.Read(next, "alice"));
        Assert.AreEqual("all", DashboardPreferences.Read(next, "bob").Range);
        next.Request.QueryString = new("?apply=1&range=custom&from=2026-10-17&to=2026-10-01");
        Assert.IsNotNull(DashboardPreferences.Read(next, "alice").Error);
        Assert.IsFalse(next.Response.Headers.ContainsKey("Set-Cookie"));
    }
}
