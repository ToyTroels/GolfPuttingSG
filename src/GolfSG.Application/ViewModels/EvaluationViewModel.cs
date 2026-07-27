using System.Collections.ObjectModel;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Queries;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public sealed class EvaluationViewModel : ViewModelBase
{
    private const string Last7Days = "Sidste 7 dage";
    private const string Last30Days = "Sidste 30 dage";
    private const string SeasonToDate = "S\u00e6son til dato";
    private const string AllRounds = "Alle runder";
    private const string TotalCategory = "Total";
    private const string PuttingCategory = "Putting";
    private const string ApproachCategory = "Approach";
    private const string AroundGreenCategory = "Omkring green";

    private readonly IRoundStatisticsQueryService queryService;
    private IReadOnlyList<Round> rounds = [];
    private string selectedPeriod = Last30Days;
    private string selectedCategory = TotalCategory;
    private bool isBusy;
    private string errorMessage = string.Empty;
    private RoundStatisticsSummary summary = RoundStatisticsService.Summarize([], StrokesGainedCategory.Total);

    public EvaluationViewModel(IRoundRepository repository)
    {
        queryService = new RoundStatisticsQueryService(repository);
    }

    public IReadOnlyList<string> PeriodOptions { get; } = [Last7Days, Last30Days, SeasonToDate, AllRounds];

    public IReadOnlyList<string> CategoryOptions { get; } = [TotalCategory, PuttingCategory, ApproachCategory, AroundGreenCategory];

    public ObservableCollection<PuttingMadePercentageBucketItemViewModel> PuttingBuckets { get; } = [];

    public bool IsBusy
    {
        get => isBusy;
        private set => SetProperty(ref isBusy, value);
    }

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

    public string SelectedPeriod
    {
        get => selectedPeriod;
        set
        {
            if (SetProperty(ref selectedPeriod, value))
            {
                RefreshSummary();
            }
        }
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (SetProperty(ref selectedCategory, value))
            {
                RefreshSummary();
            }
        }
    }

    public string Title => "SG evaluering beta";

    public string IncludedRoundsText => summary.TrackedRoundCount == 1
        ? "1 runde inkluderet"
        : $"{summary.TrackedRoundCount} runder inkluderet";

    public string PeriodRoundsText => summary.PeriodRoundCount == 1
        ? "1 gemt runde i perioden"
        : $"{summary.PeriodRoundCount} gemte runder i perioden";

    public bool HasTrackedRounds => summary.TrackedRoundCount > 0;

    public string EmptyStateText => summary.PeriodRoundCount == 0
        ? "Ingen gemte runder i perioden."
        : "Ingen runder i perioden tracker den valgte SG-kategori.";

    public string TotalSgText => UiFormat.Sg(summary.TotalStrokesGained);

    public string AverageSgText => UiFormat.Sg(summary.AverageStrokesGainedPerRound);

    public string BestRoundText => FormatRoundScore(summary.BestRound);

    public string WorstRoundText => FormatRoundScore(summary.WorstRound);

    public bool ShowPuttingBuckets => summary.Category is StrokesGainedCategory.Total or StrokesGainedCategory.Putting;

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
            rounds = await queryService.LoadRoundsAsync();
            RefreshSummary();
        }
        catch (Exception)
        {
            ErrorMessage = "Evalueringen kunne ikke indl\u00e6ses. Pr\u00f8v igen, eller tjek lagring under Indstillinger.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshSummary()
    {
        var (startDate, endDate) = GetSelectedPeriod(DateTime.Today);
        summary = queryService.Summarize(
            rounds,
            GetSelectedCategory(),
            startDate,
            endDate);

        PuttingBuckets.Clear();
        foreach (var bucket in summary.PuttingMadePercentageBuckets.Where(bucket => bucket.Attempts > 0))
        {
            PuttingBuckets.Add(new PuttingMadePercentageBucketItemViewModel(bucket));
        }

        OnPropertyChanged(nameof(IncludedRoundsText));
        OnPropertyChanged(nameof(PeriodRoundsText));
        OnPropertyChanged(nameof(HasTrackedRounds));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(TotalSgText));
        OnPropertyChanged(nameof(AverageSgText));
        OnPropertyChanged(nameof(BestRoundText));
        OnPropertyChanged(nameof(WorstRoundText));
        OnPropertyChanged(nameof(ShowPuttingBuckets));
    }

    private StrokesGainedCategory GetSelectedCategory() => SelectedCategory switch
    {
        PuttingCategory => StrokesGainedCategory.Putting,
        ApproachCategory => StrokesGainedCategory.Approach,
        AroundGreenCategory => StrokesGainedCategory.AroundGreen,
        _ => StrokesGainedCategory.Total
    };

    private (DateTime? StartDate, DateTime? EndDate) GetSelectedPeriod(DateTime today)
    {
        return SelectedPeriod switch
        {
            Last7Days => (today.Date.AddDays(-6), today.Date),
            Last30Days => (today.Date.AddDays(-29), today.Date),
            SeasonToDate => (new DateTime(today.Year, 1, 1), today.Date),
            _ => (null, null)
        };
    }

    private string FormatRoundScore(Round? round)
    {
        if (round is null)
        {
            return "-";
        }

        return $"{UiFormat.Date(round.Date)} ({UiFormat.Sg(RoundStatisticsService.CalculateStrokesGained(round, summary.Category))})";
    }
}

public sealed class PuttingMadePercentageBucketItemViewModel
{
    public PuttingMadePercentageBucketItemViewModel(PuttingMadePercentageBucket bucket)
    {
        Name = bucket.Name;
        DetailText = $"{bucket.Attempts} fors\u00f8g | {bucket.MadePutts} i | {bucket.MadePercentage:0}%";
        AveragePuttsText = $"{bucket.AveragePutts:0.0} putts i snit";
        StrokesGainedText = UiFormat.Sg(bucket.TotalStrokesGained);
    }

    public string Name { get; }

    public string DetailText { get; }

    public string AveragePuttsText { get; }

    public string StrokesGainedText { get; }
}
