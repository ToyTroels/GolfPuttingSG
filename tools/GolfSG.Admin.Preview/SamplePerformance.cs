using GolfSG.Application.Server;

namespace GolfSG.Admin.Preview;

public static class SamplePerformance
{
    public static readonly PersonalPerformance Overview = new(30, 12, 0, 9, 3, 2.65, .294,
        Enumerable.Range(1, 12).Select(i => Summary(i)).ToArray(),
        Enumerable.Range(1, 9).Select(i => Summary(i)).Reverse().ToArray(),
        [new("Putting", 9, .48), new("Approach", 6, -.22), new("Around green", 4, .13)]);
    private static PersonalRoundSummary Summary(int i) => WithCategories(new($"my-sample-rounds-{i:000}", i > 9 ? "Putting practice" : "Golf round",
        new DateTime(2026, 10, 18).AddDays(-i), i > 9, i > 9 ? "Putting practice" : "Normal round", i % 3 == 0 ? 9 : 18, 18,
        new[] { 1.2, .3, -.7, 1.1, .8, -.4, .1, .6, -.35, .4, -.2, .7 }[i - 1], i % 3 == 0 ? 17 : 33), i);
    private static PersonalRoundSummary WithCategories(PersonalRoundSummary round, int i)
    {
        double? approach = round.Practice || i % 4 == 0 ? null : -.5 + i * .06;
        double? around = round.Practice || i % 3 == 0 ? null : .1 - i * .03;
        return round with { PuttingSg = round.TotalSg - (approach ?? 0) - (around ?? 0), ApproachSg = approach, AroundGreenSg = around };
    }
    public static PersonalRoundDetail? Detail(string id)
    {
        var summary = Overview.Recent.SingleOrDefault(r => r.Id == id);
        if (summary is null) return null;
        return new(summary, Enumerable.Range(1, summary.Completed).Select(i => new PersonalScoreRow(i,
            (summary.Practice ? "Attempt " : "Hole ") + i, $"Putting {2 + i % 6} m", i % 5 == 0 ? 1 : 2, 0,
            summary.PuttingSg / summary.Completed, summary.ApproachSg / summary.Completed, summary.AroundGreenSg / summary.Completed, summary.TotalSg / summary.Completed)).ToArray());
    }
}
