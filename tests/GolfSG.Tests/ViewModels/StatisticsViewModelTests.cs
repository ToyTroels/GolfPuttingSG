using GolfSG.Application.Services;
using GolfSG.Application.ViewModels;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class StatisticsViewModelTests
{
    [TestMethod]
    public async Task SeasonAndCustomDatesFilterRoundsAndExcludeGames()
    {
        var year = DateTime.Today.Year;
        var current = new Round("current", new DateTime(year, 5, 10),
            [StrokesGainedCalculator.BuildHole(1, 9, 3)], RoundTrackingOptions.PuttingOnly, 1);
        var previous = current with { Id = "previous", Date = new DateTime(year - 1, 5, 10), Holes = [StrokesGainedCalculator.BuildHole(1, 1, 1)] };
        var game = current with { Id = "game", TrackingOptions = new RoundTrackingOptions(true, false, false, true, PuttingGame.LadderMode) };
        var model = new StatisticsViewModel(new TestRoundRepository([current, previous, game]), FixedDistanceUnitSettings.Meters);
        await model.LoadAsync();
        Assert.AreEqual("1 runder · 1 huller med putning", model.SampleText);
        StringAssert.StartsWith(model.ThreePuttDistanceText, "9,0 m");
        StringAssert.StartsWith(model.OpportunityText, "Lange putts");
        model.SelectedPeriod = "Alle sæsoner";
        Assert.AreEqual("2 runder · 2 huller med putning", model.SampleText);
        model.SelectedPeriod = $"Sæson {year - 1}";
        Assert.AreEqual("Ingen 3-putts registreret", model.ThreePuttDistanceText);
        Assert.AreEqual("Ingen afstandsgruppe har et samlet SG-tab.", model.OpportunityText);
        model.SelectedPeriod = "Valgfri periode";
        model.StartDate = current.Date;
        model.EndDate = current.Date;
        Assert.AreEqual("1 runder · 1 huller med putning", model.SampleText);
        model.StartDate = current.Date.AddDays(1);
        Assert.IsFalse(model.HasData);
        Assert.AreEqual("Startdato skal være før slutdato.", model.SampleText);
    }
}
