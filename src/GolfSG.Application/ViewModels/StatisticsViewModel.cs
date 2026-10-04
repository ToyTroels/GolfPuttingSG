using System.Collections.ObjectModel;
using GolfSG.Application.Services;
using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Application.ViewModels;

public sealed class StatisticsViewModel(IRoundRepository repository, IDistanceUnitSettings distanceSettings) : ViewModelBase
{
    private IReadOnlyList<Round> rounds = [];
    private DateTime startDate = new(DateTime.Today.Year, 1, 1);
    private DateTime endDate = new(DateTime.Today.Year, 12, 31);
    private string selectedPeriod = $"Sæson {DateTime.Today.Year}";
    public ObservableCollection<string> Periods { get; } = [$"Sæson {DateTime.Today.Year}", "Alle sæsoner", "Valgfri periode"];
    public ObservableCollection<PuttingDistanceBucketItemViewModel> DistanceBuckets { get; } = [];
    public string SelectedPeriod
    {
        get => selectedPeriod;
        set
        {
            if (!SetProperty(ref selectedPeriod, value)) return;
            if (value.StartsWith("Sæson ") && int.TryParse(value[6..], out var year))
            {
                startDate = new DateTime(year, 1, 1);
                endDate = new DateTime(year, 12, 31);
                OnPropertyChanged(nameof(StartDate));
                OnPropertyChanged(nameof(EndDate));
            }
            OnPropertyChanged(nameof(IsCustomPeriod));
            Refresh();
        }
    }
    public bool IsCustomPeriod => SelectedPeriod == "Valgfri periode";
    public DateTime StartDate { get => startDate; set { if (SetProperty(ref startDate, value)) Refresh(); } }
    public DateTime EndDate { get => endDate; set { if (SetProperty(ref endDate, value)) Refresh(); } }
    public string SampleText { get; private set; } = "";
    public string ThreePuttText { get; private set; } = "";
    public string ThreePuttDistanceText { get; private set; } = "";
    public string OpportunityText { get; private set; } = "";
    public string SgText { get; private set; } = "";
    public bool HasData { get; private set; }

    public async Task LoadAsync()
    {
        rounds = await repository.GetRoundsAsync();
        foreach (var year in rounds.Select(round => round.Date.Year).Distinct().OrderDescending())
            if (!Periods.Contains($"Sæson {year}")) Periods.Add($"Sæson {year}");
        Refresh();
    }

    private void Refresh()
    {
        var filtered = rounds.Where(round =>
            !(round.TrackingOptions?.IsPuttingGame ?? false) &&
            (round.TrackingOptions?.TrackPutting ?? true) &&
            (SelectedPeriod == "Alle sæsoner" || (round.Date.Date >= StartDate.Date && round.Date.Date <= EndDate.Date)))
            .ToList();
        var holes = filtered.SelectMany(round => round.Holes).Where(hole => hole.IsCompleted).ToList();
        var threePutts = holes.Where(hole => hole.Putts == 3).ToList();
        HasData = holes.Count > 0;
        SampleText = StartDate > EndDate && SelectedPeriod != "Alle sæsoner" ? "Startdato skal være før slutdato."
            : $"{filtered.Count(round => round.Holes.Any(hole => hole.IsCompleted))} runder · {holes.Count} huller med putning";
        ThreePuttText = HasData ? $"{holes.Count(hole => hole.Putts >= 3)} af {holes.Count} huller · {100d * holes.Count(hole => hole.Putts >= 3) / holes.Count:0}% med 3+ putts" : "Ingen registrerede putts i perioden.";
        ThreePuttDistanceText = threePutts.Count == 0 ? "Ingen 3-putts registreret"
            : $"{UiFormat.PuttingDistance(threePutts.Average(hole => hole.FirstPuttDistanceMeters), distanceSettings.PuttingDistanceUnit)} · {threePutts.Count} huller med præcis 3 putts";
        SgText = HasData ? $"{UiFormat.Sg(holes.Sum(hole => hole.StrokesGainedPutting))} SG i alt" : "Ingen data";
        DistanceBuckets.Clear();
        var buckets = PuttingStrokesGainedCalculator.CalculateSummary(holes).DistanceBuckets;
        foreach (var bucket in RoundResultViewModel.BuildRecapDistanceBuckets(buckets))
        {
            DistanceBuckets.Add(new(bucket, distanceSettings.PuttingDistanceUnit));
        }
        var weakest = DistanceBuckets.Where(bucket => bucket.IsLoss).MinBy(bucket => bucket.TotalStrokesGained);
        OpportunityText = !HasData ? "Registrer runder for at se, hvor du taber flest slag."
            : weakest is null ? "Ingen afstandsgruppe har et samlet SG-tab."
            : $"{weakest.Name}: {UiFormat.Sg(weakest.TotalStrokesGained)} SG på {weakest.Attempts} huller. Største samlede tab i perioden.";
        foreach (var property in new[] { nameof(SampleText), nameof(ThreePuttText), nameof(ThreePuttDistanceText), nameof(OpportunityText), nameof(SgText), nameof(HasData) })
            OnPropertyChanged(property);
    }
}
