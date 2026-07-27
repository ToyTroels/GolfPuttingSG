using GolfSG.Application.Common;
using GolfSG.Core;
using GolfSG.Core.Models;
using GolfSG.Application.Services;

namespace GolfSG.Application.Putting;

public sealed record PuttingGameSubmission(bool IsComplete, string? RoundId);

public interface IPuttingGameSessionService
{
    string RoundId { get; }
    string Mode { get; }
    PuttingGameDefinition Definition { get; }
    IReadOnlyList<double> Distances { get; }
    bool DistancesAreMeters { get; }
    IReadOnlyList<HolePuttingData> CompletedPutts { get; }
    int CurrentIndex { get; }
    bool IsComplete { get; }
    double CurrentDistanceMeters { get; }
    double CurrentExpectedPutts { get; }
    double TargetPutts { get; }

    void Start(string gameMode);
    void StartTraining(int puttCount, double minimumDistanceMeters, double maximumDistanceMeters, PuttingTrainingDistanceDistribution distribution);
    void StartBenchmark(string benchmark, PuttingDistanceOrder order);
    void StartLadderBenchmark(string benchmark);
    Task<PuttingGameSubmission> SubmitAsync(int puttsUsed);
}

public sealed class PuttingGameSessionService : IPuttingGameSessionService
{
    private readonly IRoundRepository repository;
    private readonly AsyncActionGate submitGate = new();
    private readonly List<HolePuttingData> completedPutts = [];
    private IReadOnlyList<double> distances = [];
    private bool distancesAreMeters;
    private int currentIndex;

    public PuttingGameSessionService(IRoundRepository repository)
    {
        this.repository = repository;
        RoundId = Guid.NewGuid().ToString("N");
        Start(PuttingGame.LadderMode);
    }

    public string RoundId { get; }
    public string Mode { get; private set; } = PuttingGame.LadderMode;
    public PuttingGameDefinition Definition { get; private set; } = PuttingGame.GetDefinition(PuttingGame.LadderMode);
    public IReadOnlyList<double> Distances => distances;
    public bool DistancesAreMeters => distancesAreMeters;
    public IReadOnlyList<HolePuttingData> CompletedPutts => completedPutts;
    public int CurrentIndex => currentIndex;
    public bool IsComplete => distances.Count > 0 && currentIndex >= distances.Count;
    public double TargetPutts => Definition.ScoringMode == PuttingGameScoringMode.NormalizedTargetTotal
        ? PuttingGame.TargetPutts
        : distances.Sum(PuttingGame.GetExpectedPutts);

    public double CurrentDistanceMeters
    {
        get
        {
            EnsureActive();
            return distancesAreMeters
                ? distances[currentIndex]
                : DistanceConversions.FeetToMeters(distances[currentIndex]);
        }
    }

    public double CurrentExpectedPutts
    {
        get
        {
            EnsureActive();
            return distancesAreMeters
                ? PuttingGame.GetExpectedPutts(CurrentDistanceMeters)
                : PuttingGame.GetExpectedPutts((int)distances[currentIndex], Mode);
        }
    }

    public void Start(string gameMode)
    {
        Mode = PuttingGame.NormalizeMode(gameMode);
        Definition = PuttingGame.GetDefinition(Mode);
        distances = PuttingGame.GetPresetDistances(Mode).Select(distance => (double)distance).ToList();
        distancesAreMeters = false;
        ResetProgress();
    }

    public void StartTraining(
        int puttCount,
        double minimumDistanceMeters,
        double maximumDistanceMeters,
        PuttingTrainingDistanceDistribution distribution)
    {
        var trainingDistances = PuttingGame.BuildTrainingDistancesMeters(
            puttCount,
            minimumDistanceMeters,
            maximumDistanceMeters,
            distribution);

        StartMeters(PuttingGame.CreateCustomDefinition(trainingDistances), trainingDistances);
    }

    public void StartBenchmark(string benchmark, PuttingDistanceOrder order)
    {
        var definition = PuttingGame.GetBenchmarkDefinition(benchmark);
        var orderedDistances = PuttingGame.OrderDistances(definition.DistancesMeters, order);
        StartMeters(definition with { DistancesMeters = orderedDistances }, orderedDistances);
    }

    public void StartLadderBenchmark(string benchmark)
    {
        var definition = PuttingGame.GetBenchmarkDefinition(benchmark);
        StartMeters(definition, definition.DistancesMeters);
    }

    public async Task<PuttingGameSubmission> SubmitAsync(int puttsUsed)
    {
        var execution = await submitGate.RunAsync(async () =>
        {
            if (IsComplete)
            {
                return new PuttingGameSubmission(true, RoundId);
            }

            EnsureActive();
            var putt = distancesAreMeters
                ? PuttingGame.BuildPutt(currentIndex + 1, CurrentDistanceMeters, puttsUsed)
                : PuttingGame.BuildPutt(currentIndex + 1, (int)distances[currentIndex], puttsUsed, Mode);

            completedPutts.Add(putt);
            currentIndex++;

            if (!IsComplete)
            {
                return new PuttingGameSubmission(false, null);
            }

            try
            {
                await repository.SaveRoundAsync(BuildRound());
                return new PuttingGameSubmission(true, RoundId);
            }
            catch
            {
                currentIndex--;
                completedPutts.RemoveAt(completedPutts.Count - 1);
                throw;
            }
        });

        return execution.Executed
            ? execution.Value!
            : new PuttingGameSubmission(IsComplete, IsComplete ? RoundId : null);
    }

    private Round BuildRound() =>
        new(
            RoundId,
            DateTime.Now,
            completedPutts.ToList(),
            new RoundTrackingOptions(true, false, false, true, Mode),
            completedPutts.Count,
            false,
            PuttingGame.CreateRoundGameInfo(Definition));

    private void StartMeters(PuttingGameDefinition definition, IReadOnlyList<double> puttingDistances)
    {
        Mode = PuttingGame.LadderMode;
        Definition = definition;
        distances = puttingDistances.ToList();
        distancesAreMeters = true;
        ResetProgress();
    }

    private void ResetProgress()
    {
        completedPutts.Clear();
        currentIndex = 0;
    }

    private void EnsureActive()
    {
        if (distances.Count == 0 || IsComplete)
        {
            throw new InvalidOperationException("The putting game does not have an active putt.");
        }
    }
}
