using System.Collections.ObjectModel;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class RoundHistoryViewModel : ViewModelBase
{
    private const string HistoryTypeAll = "Alle";
    private const string HistoryTypeRounds = "Runder";
    private const string HistoryTypePuttingGames = "Putting-spil";
    private const string HistoryTypeBenchmarks = "Benchmarks";
    private const string HistoryCategoryTotal = "Total SG";
    private const string HistoryCategoryPutting = "Putting";
    private const string HistoryCategoryApproach = "Approach";
    private const string HistoryCategoryAroundGreen = "Omkring green";
    private const string HistoryPeriodAll = "Alle datoer";
    private const string HistoryPeriodLast7Days = "Sidste 7 dage";
    private const string HistoryPeriodLast30Days = "Sidste 30 dage";
    private const string HistoryPeriodSeasonToDate = "S\u00e6son til dato";
    private const string HistorySortNewestFirst = "Nyeste f\u00f8rst";
    private const string HistorySortOldestFirst = "\u00c6ldste f\u00f8rst";
    private const string HistorySortBestSg = "Bedste SG";
    private const string HistorySortWorstSg = "V\u00e6rste SG";

    private readonly IRoundRepository repository;
    private readonly IDistanceUnitSettings distanceUnitSettings;
    private readonly List<RoundListItemViewModel> allRoundItems = [];
    private bool showRecoveredFromBackupWarning;
    private bool isBusy;
    private string errorMessage = string.Empty;
    private string selectedHistoryType = HistoryTypeAll;
    private string selectedHistoryCategory = HistoryCategoryTotal;
    private string selectedHistoryPeriod = HistoryPeriodAll;
    private string selectedHistorySort = HistorySortNewestFirst;
    private string historyComparisonText = string.Empty;

    public RoundHistoryViewModel(IRoundRepository repository, IDistanceUnitSettings? distanceUnitSettings = null)
    {
        this.repository = repository;
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
    }

    public ObservableCollection<RoundListItemViewModel> Rounds { get; } = [];

    public IReadOnlyList<string> HistoryTypeOptions { get; } =
        [HistoryTypeAll, HistoryTypeRounds, HistoryTypePuttingGames, HistoryTypeBenchmarks];

    public IReadOnlyList<string> HistoryCategoryOptions { get; } =
        [HistoryCategoryTotal, HistoryCategoryPutting, HistoryCategoryApproach, HistoryCategoryAroundGreen];

    public IReadOnlyList<string> HistoryPeriodOptions { get; } =
        [HistoryPeriodAll, HistoryPeriodLast7Days, HistoryPeriodLast30Days, HistoryPeriodSeasonToDate];

    public IReadOnlyList<string> HistorySortOptions { get; } =
        [HistorySortNewestFirst, HistorySortOldestFirst, HistorySortBestSg, HistorySortWorstSg];

    public string SelectedHistoryType
    {
        get => selectedHistoryType;
        set
        {
            if (!IsHistoryOption(value, HistoryTypeOptions))
            {
                return;
            }

            if (SetProperty(ref selectedHistoryType, value))
            {
                ApplyHistoryFilters();
            }
        }
    }

    public string SelectedHistoryCategory
    {
        get => selectedHistoryCategory;
        set
        {
            if (!IsHistoryOption(value, HistoryCategoryOptions))
            {
                return;
            }

            if (SetProperty(ref selectedHistoryCategory, value))
            {
                ApplyHistoryFilters();
            }
        }
    }

    public string SelectedHistoryPeriod
    {
        get => selectedHistoryPeriod;
        set
        {
            if (!IsHistoryOption(value, HistoryPeriodOptions))
            {
                return;
            }

            if (SetProperty(ref selectedHistoryPeriod, value))
            {
                ApplyHistoryFilters();
            }
        }
    }

    public string SelectedHistorySort
    {
        get => selectedHistorySort;
        set
        {
            if (!IsHistoryOption(value, HistorySortOptions))
            {
                return;
            }

            if (SetProperty(ref selectedHistorySort, value))
            {
                ApplyHistoryFilters();
            }
        }
    }

    public int TotalHistoryCount => allRoundItems.Count;

    public int FilteredHistoryCount => Rounds.Count;

    public bool IsHistoryEmpty => FilteredHistoryCount == 0;

    public string HistorySummaryText
    {
        get
        {
            if (TotalHistoryCount == 0)
            {
                return "Ingen gemte runder endnu.";
            }

            return FilteredHistoryCount == TotalHistoryCount
                ? $"{TotalHistoryCount} gemte runder og spil"
                : $"Viser {FilteredHistoryCount} af {TotalHistoryCount} gemte runder og spil";
        }
    }

    public string HistoryEmptyText => TotalHistoryCount == 0
        ? "Ingen gemte runder endnu."
        : "Ingen historik matcher de valgte filtre.";

    public string HistoryComparisonText
    {
        get => historyComparisonText;
        private set
        {
            if (SetProperty(ref historyComparisonText, value))
            {
                OnPropertyChanged(nameof(HasHistoryComparison));
            }
        }
    }

    public bool HasHistoryComparison => !string.IsNullOrWhiteSpace(HistoryComparisonText);

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
            var loadedItems = rounds
                .Select(round => new RoundListItemViewModel(round, distanceUnitSettings))
                .ToList();

            allRoundItems.Clear();
            allRoundItems.AddRange(loadedItems);
            ApplyHistoryFilters();
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
            allRoundItems.RemoveAll(item => string.Equals(item.Id, round.Id, StringComparison.Ordinal));
            ApplyHistoryFilters();
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

    private void ApplyHistoryFilters()
    {
        var category = SelectedSgCategory;
        var filtered = allRoundItems
            .Where(MatchesSelectedHistoryType)
            .Where(item => item.TracksCategory(category))
            .Where(MatchesSelectedHistoryPeriod)
            .ToList();

        var comparisonAverage = filtered.Count == 0
            ? 0
            : filtered.Average(item => item.GetSg(category));
        UpdateHistoryComparison(filtered, category, comparisonAverage);

        var sorted = SortHistory(filtered, category).ToList();

        Rounds.Clear();
        foreach (var round in sorted)
        {
            round.SetHistoryDisplay(category, comparisonAverage, filtered.Count);
            Rounds.Add(round);
        }

        OnHistoryCountsChanged();
    }

    private bool MatchesSelectedHistoryType(RoundListItemViewModel item)
    {
        return SelectedHistoryType switch
        {
            HistoryTypeRounds => !item.IsPuttingGame,
            HistoryTypePuttingGames => item.IsPuttingGame && !item.IsBenchmark,
            HistoryTypeBenchmarks => item.IsBenchmark,
            _ => true
        };
    }

    private bool MatchesSelectedHistoryPeriod(RoundListItemViewModel item)
    {
        var date = item.Round.Date.Date;
        var today = DateTime.Today;
        return SelectedHistoryPeriod switch
        {
            HistoryPeriodLast7Days => date >= today.AddDays(-6) && date <= today,
            HistoryPeriodLast30Days => date >= today.AddDays(-29) && date <= today,
            HistoryPeriodSeasonToDate => date >= new DateTime(today.Year, 1, 1) && date <= today,
            _ => true
        };
    }

    private IEnumerable<RoundListItemViewModel> SortHistory(
        IEnumerable<RoundListItemViewModel> items,
        HistorySgCategory category)
    {
        return SelectedHistorySort switch
        {
            HistorySortOldestFirst => items
                .OrderBy(item => item.Round.Date)
                .ThenBy(item => item.Id, StringComparer.Ordinal),
            HistorySortBestSg => items
                .OrderByDescending(item => item.GetSg(category))
                .ThenByDescending(item => item.Round.Date)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal),
            HistorySortWorstSg => items
                .OrderBy(item => item.GetSg(category))
                .ThenByDescending(item => item.Round.Date)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal),
            _ => SortNewestFirst(items)
        };
    }

    private void UpdateHistoryComparison(
        IReadOnlyList<RoundListItemViewModel> items,
        HistorySgCategory category,
        double average)
    {
        if (items.Count < 2)
        {
            HistoryComparisonText = string.Empty;
            return;
        }

        var best = items
            .OrderByDescending(item => item.GetSg(category))
            .ThenByDescending(item => item.Round.Date)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .First();
        var worst = items
            .OrderBy(item => item.GetSg(category))
            .ThenByDescending(item => item.Round.Date)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal)
            .First();

        HistoryComparisonText = $"{GetHistoryCategoryLabel(category)}: snit {UiFormat.Sg(average)} | " +
            $"bedst {best.Date} {UiFormat.Sg(best.GetSg(category))} | " +
            $"svagest {worst.Date} {UiFormat.Sg(worst.GetSg(category))}";
    }

    private HistorySgCategory SelectedSgCategory => SelectedHistoryCategory switch
    {
        HistoryCategoryPutting => HistorySgCategory.Putting,
        HistoryCategoryApproach => HistorySgCategory.Approach,
        HistoryCategoryAroundGreen => HistorySgCategory.AroundGreen,
        _ => HistorySgCategory.Total
    };

    private static IOrderedEnumerable<RoundListItemViewModel> SortNewestFirst(IEnumerable<RoundListItemViewModel> items) =>
        items
            .OrderByDescending(item => item.Round.Date)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal);

    private void OnHistoryCountsChanged()
    {
        OnPropertyChanged(nameof(TotalHistoryCount));
        OnPropertyChanged(nameof(FilteredHistoryCount));
        OnPropertyChanged(nameof(IsHistoryEmpty));
        OnPropertyChanged(nameof(HistorySummaryText));
        OnPropertyChanged(nameof(HistoryEmptyText));
        OnPropertyChanged(nameof(HistoryComparisonText));
        OnPropertyChanged(nameof(HasHistoryComparison));
    }

    private static string GetHistoryCategoryLabel(HistorySgCategory category)
    {
        return category switch
        {
            HistorySgCategory.Putting => "SG putting",
            HistorySgCategory.Approach => "SG approach",
            HistorySgCategory.AroundGreen => "SG omkring green",
            _ => "SG total"
        };
    }

    private static bool IsHistoryOption(string? value, IReadOnlyList<string> options)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            options.Any(option => string.Equals(option, value, StringComparison.Ordinal));
    }
}
