using System.Collections.ObjectModel;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Queries;
using GolfSG.Application.Services;

namespace GolfSG.Application.ViewModels;

public sealed class BenchmarkHistoryViewModel : ViewModelBase
{
    private readonly IBenchmarkHistoryQueryService queryService;
    private readonly IDistanceUnitSettings distanceUnitSettings;
    private bool isBusy;
    private string errorMessage = string.Empty;

    public BenchmarkHistoryViewModel(IRoundRepository repository, IDistanceUnitSettings? distanceUnitSettings = null)
    {
        queryService = new BenchmarkHistoryQueryService(repository);
        this.distanceUnitSettings = distanceUnitSettings ?? FixedDistanceUnitSettings.Meters;
    }

    public ObservableCollection<BenchmarkHistoryItemViewModel> Benchmarks { get; } = [];

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

    public bool HasBenchmarks => Benchmarks.Count > 0;

    public string EmptyStateText => "Ingen benchmark-fors\u00f8g endnu.";

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
            var summaries = await queryService.LoadAsync();
            Benchmarks.Clear();
            foreach (var summary in summaries)
            {
                Benchmarks.Add(new BenchmarkHistoryItemViewModel(summary, distanceUnitSettings.PuttingDistanceUnit));
            }

            OnPropertyChanged(nameof(HasBenchmarks));
        }
        catch (Exception)
        {
            ErrorMessage = "Benchmark-historik kunne ikke indl\u00e6ses. Pr\u00f8v igen, eller tjek lagring under Indstillinger.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class BenchmarkHistoryItemViewModel
{
    public BenchmarkHistoryItemViewModel(
        BenchmarkHistorySummary summary,
        PuttingDistanceUnitPreference puttingDistanceUnit = PuttingDistanceUnitPreference.Meters)
    {
        Title = summary.DisplayName;
        TypeText = FormatType(summary);
        AttemptsText = summary.Attempts == 1 ? "1 fors\u00f8g" : $"{summary.Attempts} fors\u00f8g";
        LatestScoreText = UiFormat.Sg(summary.LatestAttempt.Score);
        BestScoreText = UiFormat.Sg(summary.BestAttempt.Score);
        AverageLastThreeText = UiFormat.Sg(summary.AverageLastThreeScore);
        ImprovementText = UiFormat.Sg(summary.ImprovementFromFirstToLatest);
        LatestDateText = UiFormat.Date(summary.LatestAttempt.Date);
        DetailText = FormatDetail(summary, puttingDistanceUnit);
    }

    public string Title { get; }

    public string TypeText { get; }

    public string AttemptsText { get; }

    public string LatestScoreText { get; }

    public string BestScoreText { get; }

    public string AverageLastThreeText { get; }

    public string ImprovementText { get; }

    public string LatestDateText { get; }

    public string DetailText { get; }

    private static string FormatType(BenchmarkHistorySummary summary) => summary.BenchmarkType switch
    {
        PuttingBenchmarkType.Ladder => "Ladder benchmark",
        PuttingBenchmarkType.BellCurve => "Bell-curve benchmark",
        _ => "Benchmark"
    };

    private static string FormatDetail(BenchmarkHistorySummary summary, PuttingDistanceUnitPreference puttingDistanceUnit)
    {
        if (summary.BenchmarkType != PuttingBenchmarkType.Ladder ||
            summary.StartDistanceMeters is null ||
            summary.EndDistanceMeters is null ||
            summary.PuttsPerDistance is null)
        {
            return $"{summary.AttemptCount} putts";
        }

        var stepText = summary.DistanceStepMeters is null
            ? summary.DistanceStepDescription
            : $"{UiFormat.PuttingDistance(summary.DistanceStepMeters.Value, puttingDistanceUnit)} trin";

        var parts = new List<string>
        {
            $"{UiFormat.PuttingDistance(summary.StartDistanceMeters.Value, puttingDistanceUnit)}-{UiFormat.PuttingDistance(summary.EndDistanceMeters.Value, puttingDistanceUnit)}"
        };
        if (!string.IsNullOrWhiteSpace(stepText))
        {
            parts.Add(stepText);
        }

        parts.Add($"{summary.PuttsPerDistance} pr. afstand");
        return string.Join(", ", parts);
    }
}
