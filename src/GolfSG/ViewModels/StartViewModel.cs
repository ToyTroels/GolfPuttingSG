using System.Collections.ObjectModel;
using GolfSG.Services;

namespace GolfSG.ViewModels;

public sealed class StartViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;
    private bool showRecoveredFromBackupWarning;
    private bool isBusy;
    private string errorMessage = string.Empty;

    public StartViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public ObservableCollection<RoundListItemViewModel> Rounds { get; } = [];

    public ObservableCollection<RoundListItemViewModel> RecentRounds { get; } = [];

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
        "Gemte runder blev gendannet fra backup. Tjek gerne historikken, før du fortsætter.";

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
            RecentRounds.Remove(round);
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
}
