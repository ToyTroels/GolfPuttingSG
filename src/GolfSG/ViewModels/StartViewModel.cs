using System.Collections.ObjectModel;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class StartViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;
    private bool showRecoveredFromBackupWarning;

    public StartViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public ObservableCollection<RoundListItemViewModel> Rounds { get; } = [];

    public ObservableCollection<RoundListItemViewModel> RecentRounds { get; } = [];

    public bool ShowRecoveredFromBackupWarning
    {
        get => showRecoveredFromBackupWarning;
        private set => SetProperty(ref showRecoveredFromBackupWarning, value);
    }

    public string RecoveredFromBackupWarningText { get; } =
        "Gemte runder blev gendannet fra backup. Tjek gerne historikken, før du fortsætter.";

    public async Task LoadAsync()
    {
        var rounds = await repository.GetRoundsAsync();
        ShowRecoveredFromBackupWarning = repository.WasLastReadRecoveredFromBackup;
        var sortedRounds = rounds
            .OrderByDescending(round => round.Date)
            .ThenByDescending(round => round.Id, StringComparer.Ordinal)
            .Select(round => new RoundListItemViewModel(round))
            .ToList();

        Rounds.Clear();
        foreach (var round in sortedRounds)
        {
            Rounds.Add(round);
        }

        RecentRounds.Clear();
        foreach (var round in sortedRounds.Take(5))
        {
            RecentRounds.Add(round);
        }
    }

    public async Task DeleteRoundAsync(RoundListItemViewModel round)
    {
        await repository.DeleteRoundAsync(round.Id);
        Rounds.Remove(round);
        RecentRounds.Remove(round);
    }
}
