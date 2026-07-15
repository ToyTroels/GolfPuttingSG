using System.Collections.ObjectModel;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class StartViewModel : ViewModelBase
{
    private const int RecentRoundLimit = 5;
    private const int TrendGroupSize = 3;

    private readonly IRoundRepository repository;
    private readonly IDistanceUnitSettings distanceUnitSettings;
    private bool showRecoveredFromBackupWarning;
    private bool hasInsights;
    private bool isBusy;
    private string errorMessage = string.Empty;
    private string insightSummaryText = "Ingen gemte runder endnu.";
    private string formInsightValue = "-";
    private string formInsightDetail = "Gns. total SG";
    private string trendInsightValue = "-";
    private string trendInsightDetail = "Seneste registrering";
    private string strengthInsightValue = "Mere data";
    private string strengthInsightDetail = "Track flere runder";
    private string focusInsightValue = "Mere data";
    private string focusInsightDetail = "Track flere SG-kategorier";

    public StartViewModel(IRoundRepository repository, IDistanceUnitSettings? distanceUnitSettings = null)
    {
        this.repository = repository;
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
    }

    public ObservableCollection<RoundListItemViewModel> Rounds { get; } = [];

    public ObservableCollection<RoundListItemViewModel> RecentRounds { get; } = [];

    public bool HasInsights
    {
        get => hasInsights;
        private set => SetProperty(ref hasInsights, value);
    }

    public string InsightSummaryText
    {
        get => insightSummaryText;
        private set => SetProperty(ref insightSummaryText, value);
    }

    public string FormInsightValue
    {
        get => formInsightValue;
        private set => SetProperty(ref formInsightValue, value);
    }

    public string FormInsightDetail
    {
        get => formInsightDetail;
        private set => SetProperty(ref formInsightDetail, value);
    }

    public string TrendInsightValue
    {
        get => trendInsightValue;
        private set => SetProperty(ref trendInsightValue, value);
    }

    public string TrendInsightDetail
    {
        get => trendInsightDetail;
        private set => SetProperty(ref trendInsightDetail, value);
    }

    public string StrengthInsightValue
    {
        get => strengthInsightValue;
        private set => SetProperty(ref strengthInsightValue, value);
    }

    public string StrengthInsightDetail
    {
        get => strengthInsightDetail;
        private set => SetProperty(ref strengthInsightDetail, value);
    }

    public string FocusInsightValue
    {
        get => focusInsightValue;
        private set => SetProperty(ref focusInsightValue, value);
    }

    public string FocusInsightDetail
    {
        get => focusInsightDetail;
        private set => SetProperty(ref focusInsightDetail, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(CanInteract));
            }
        }
    }

    public bool CanInteract => !IsBusy;

    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowRecoveredFromBackupWarning
    {
        get => showRecoveredFromBackupWarning;
        private set => SetProperty(ref showRecoveredFromBackupWarning, value);
    }

    public string RecoveredFromBackupWarningText { get; } =
        "Gemte runder blev gendannet fra backup. Tjek gerne historikken, f\u00f8r du forts\u00e6tter.";

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            var rounds = await repository.GetRoundsAsync();
            ShowRecoveredFromBackupWarning = repository.WasLastReadRecoveredFromBackup;
            var sortedRounds = rounds
                .OrderByDescending(round => round.Date)
                .ThenByDescending(round => round.Id, StringComparer.Ordinal)
                .Select(round => new RoundListItemViewModel(round, distanceUnitSettings))
                .ToList();

            ReplaceRoundLists(sortedRounds);
        }
        catch (Exception)
        {
            ErrorMessage = "Historik kunne ikke indl\u00e6ses. Pr\u00f8v igen, eller tjek lagring under Indstillinger.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> DeleteRoundAsync(RoundListItemViewModel round)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await repository.DeleteRoundAsync(round.Id);
            Rounds.Remove(round);
            RefreshRecentRoundsAndInsights(Rounds.ToList());
            return true;
        }
        catch (Exception)
        {
            ErrorMessage = "Runde kunne ikke slettes. Pr\u00f8v igen, eller tjek lagring under Indstillinger.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReplaceRoundLists(IReadOnlyList<RoundListItemViewModel> sortedRounds)
    {
        Rounds.Clear();
        foreach (var round in sortedRounds)
        {
            Rounds.Add(round);
        }

        RefreshRecentRoundsAndInsights(sortedRounds);
    }

    private void RefreshRecentRoundsAndInsights(IReadOnlyList<RoundListItemViewModel> sortedRounds)
    {
        RecentRounds.Clear();
        foreach (var round in sortedRounds.Take(RecentRoundLimit))
        {
            RecentRounds.Add(round);
        }

        RefreshInsights(sortedRounds);
    }

    private void RefreshInsights(IReadOnlyList<RoundListItemViewModel> sortedRounds)
    {
        var recent = sortedRounds.Take(RecentRoundLimit).ToList();
        HasInsights = recent.Count > 0;
        if (recent.Count == 0)
        {
            InsightSummaryText = "Ingen gemte runder endnu.";
            FormInsightValue = "-";
            FormInsightDetail = "Gns. total SG";
            TrendInsightValue = "-";
            TrendInsightDetail = "Seneste registrering";
            StrengthInsightValue = "Mere data";
            StrengthInsightDetail = "Track flere runder";
            FocusInsightValue = "Mere data";
            FocusInsightDetail = "Track flere SG-kategorier";
            return;
        }

        InsightSummaryText = recent.Count == 1
            ? "Baseret p\u00e5 seneste runde/spil."
            : $"Baseret p\u00e5 seneste {recent.Count} runder/spil.";
        FormInsightValue = UiFormat.Sg(recent.Average(round => round.TotalSg));
        FormInsightDetail = "Gns. total SG";

        if (sortedRounds.Count >= TrendGroupSize * 2)
        {
            var latestAverage = sortedRounds.Take(TrendGroupSize).Average(round => round.TotalSg);
            var previousAverage = sortedRounds.Skip(TrendGroupSize).Take(TrendGroupSize).Average(round => round.TotalSg);
            TrendInsightValue = UiFormat.Sg(latestAverage - previousAverage);
            TrendInsightDetail = "Seneste 3 vs forrige 3";
        }
        else
        {
            TrendInsightValue = UiFormat.Sg(recent[0].TotalSg);
            TrendInsightDetail = "Seneste registrering";
        }

        var categoryAverages = GetTrackedCategoryAverages(recent).ToList();
        if (categoryAverages.Count == 0)
        {
            StrengthInsightValue = "Mere data";
            StrengthInsightDetail = "Track flere runder";
            FocusInsightValue = "Mere data";
            FocusInsightDetail = "Track flere SG-kategorier";
            return;
        }

        var strength = categoryAverages
            .OrderByDescending(category => category.Average)
            .ThenBy(category => category.Label, StringComparer.Ordinal)
            .First();
        StrengthInsightValue = strength.Label;
        StrengthInsightDetail = $"{UiFormat.Sg(strength.Average)} pr. registrering";

        if (categoryAverages.Count == 1)
        {
            FocusInsightValue = "Mere data";
            FocusInsightDetail = "Track flere SG-kategorier";
            return;
        }

        var focus = categoryAverages
            .OrderBy(category => category.Average)
            .ThenBy(category => category.Label, StringComparer.Ordinal)
            .First();
        FocusInsightValue = focus.Label;
        FocusInsightDetail = $"{UiFormat.Sg(focus.Average)} pr. registrering";
    }

    private static IEnumerable<CategoryAverage> GetTrackedCategoryAverages(IReadOnlyList<RoundListItemViewModel> rounds)
    {
        var puttingRounds = rounds.Where(round => round.TracksPutting).ToList();
        if (puttingRounds.Count > 0)
        {
            yield return new CategoryAverage("Putting", puttingRounds.Average(round => round.PuttingSg));
        }

        var approachRounds = rounds.Where(round => round.TracksApproach).ToList();
        if (approachRounds.Count > 0)
        {
            yield return new CategoryAverage("Approach", approachRounds.Average(round => round.ApproachSg));
        }

        var aroundGreenRounds = rounds.Where(round => round.TracksAroundGreen).ToList();
        if (aroundGreenRounds.Count > 0)
        {
            yield return new CategoryAverage("Omkring green", aroundGreenRounds.Average(round => round.AroundGreenSg));
        }
    }

    private sealed record CategoryAverage(string Label, double Average);
}
