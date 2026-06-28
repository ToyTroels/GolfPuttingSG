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
        Rounds.Clear();
        foreach (var round in rounds)
        {
            Rounds.Add(new RoundListItemViewModel(round));
        }
    }

    public async Task DeleteRoundAsync(RoundListItemViewModel round)
    {
        await repository.DeleteRoundAsync(round.Id);
        Rounds.Remove(round);
    }
}
