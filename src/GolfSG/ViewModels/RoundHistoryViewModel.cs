using System.Collections.ObjectModel;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class RoundHistoryViewModel : ViewModelBase
{
    private const string HistoryTypeAll = "Alle";
    private const string HistoryTypeRounds = "Runder";
    private const string HistoryTypePuttingGames = "Putting-spil";
    private const string HistoryTypeBenchmarks = "Benchmarks";
    private const string HistoryPeriodAll = "Alle datoer";
    private const string HistoryPeriodLast7Days = "Sidste 7 dage";
    private const string HistoryPeriodLast30Days = "Sidste 30 dage";
    private const string HistoryPeriodSeasonToDate = "S\u00e6son til dato";
    private const string HistorySortNewestFirst = "Nyeste f\u00f8rst";
    private const string HistorySortOldestFirst = "\u00c6ldste f\u00f8rst";
    private const string HistorySortBestSg = "Bedste SG";
    private const string HistorySortWorstSg = "V\u00e6rste SG";

    private readonly IRoundRepository repository;
    private readonly List<RoundListItemViewModel> allRoundItems = [];
    private bool showRecoveredFromBackupWarning;
    private bool isBusy;
    private string errorMessage = string.Empty;
    private string selectedHistoryType = HistoryTypeAll;
    private string selectedHistoryPeriod = HistoryPeriodAll;
    private string selectedHistorySort = HistorySortNewestFirst;

    public RoundHistoryViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public ObservableCollection<RoundListItemViewModel> Rounds { get; } = [];

    public IReadOnlyList<string> HistoryTypeOptions { get; } =
        [HistoryTypeAll, HistoryTypeRounds, HistoryTypePuttingGames, HistoryTypeBenchmarks];

    public IReadOnlyList<string> HistoryPeriodOptions { get; } =
        [HistoryPeriodAll, HistoryPeriodLast7Days, HistoryPeriodLast30Days, HistoryPeriodSeasonToDate];

    public IReadOnlyList<string> HistorySortOptions { get; } =
        [HistorySortNewestFirst, HistorySortOldestFirst, HistorySortBestSg, HistorySortWorstSg];

    public string SelectedHistoryType
    {
        get => selectedHistoryType;
        set
        {
            if (SetProperty(ref selectedHistoryType, value))
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
            if (SetProperty(ref selectedHistorySort, value))
            {
                ApplyHistoryFilters();
            }
        }
    }

    public int TotalHistoryCount => allRoundItems.Count;

    public int FilteredHistoryCount => Rounds.Count;

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
                .Select(round => new RoundListItemViewModel(round))
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
        var filtered = allRoundItems
            .Where(MatchesSelectedHistoryType)
            .Where(MatchesSelectedHistoryPeriod);

        var sorted = SortHistory(filtered).ToList();

        Rounds.Clear();
        foreach (var round in sorted)
        {
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

    private IEnumerable<RoundListItemViewModel> SortHistory(IEnumerable<RoundListItemViewModel> items)
    {
        return SelectedHistorySort switch
        {
            HistorySortOldestFirst => items
                .OrderBy(item => item.Round.Date)
                .ThenBy(item => item.Id, StringComparer.Ordinal),
            HistorySortBestSg => items
                .OrderByDescending(item => item.TotalSg)
                .ThenByDescending(item => item.Round.Date)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal),
            HistorySortWorstSg => items
                .OrderBy(item => item.TotalSg)
                .ThenByDescending(item => item.Round.Date)
                .ThenByDescending(item => item.Id, StringComparer.Ordinal),
            _ => SortNewestFirst(items)
        };
    }

    private static IOrderedEnumerable<RoundListItemViewModel> SortNewestFirst(IEnumerable<RoundListItemViewModel> items) =>
        items
            .OrderByDescending(item => item.Round.Date)
            .ThenByDescending(item => item.Id, StringComparer.Ordinal);

    private void OnHistoryCountsChanged()
    {
        OnPropertyChanged(nameof(TotalHistoryCount));
        OnPropertyChanged(nameof(FilteredHistoryCount));
        OnPropertyChanged(nameof(HistorySummaryText));
        OnPropertyChanged(nameof(HistoryEmptyText));
    }
}