using System.Collections.ObjectModel;
using GolfPuttingSG.Services;

namespace GolfPuttingSG.ViewModels;

public sealed class StartViewModel : ViewModelBase
{
    private readonly IRoundRepository repository;

    public StartViewModel(IRoundRepository repository)
    {
        this.repository = repository;
    }

    public ObservableCollection<RoundListItemViewModel> Rounds { get; } = [];

    public async Task LoadAsync()
    {
        var rounds = await repository.GetRoundsAsync();
        Rounds.Clear();
        foreach (var round in rounds)
        {
            Rounds.Add(new RoundListItemViewModel(round));
        }
    }
}
