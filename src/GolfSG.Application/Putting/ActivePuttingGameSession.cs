using GolfSG.Core.Models;

namespace GolfSG.Application.Putting;

public sealed record ActivePuttingGameSession(
    string RoundId,
    string Mode,
    PuttingGameDefinition Definition,
    IReadOnlyList<double> Distances,
    bool DistancesAreMeters,
    IReadOnlyList<HolePuttingData> CompletedPutts,
    int PuttsUsed,
    DateTimeOffset UpdatedAtUtc)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(RoundId)
        && !string.IsNullOrWhiteSpace(Mode)
        && Definition is not null
        && Distances is { Count: > 0 }
        && Distances.All(distance => double.IsFinite(distance) && distance > 0)
        && CompletedPutts is not null
        && CompletedPutts.Count < Distances.Count
        && CompletedPutts.Select((putt, index) => putt is not null && putt.HoleNumber == index + 1 && putt.Putts > 0).All(valid => valid)
        && PuttsUsed is >= 1 and <= 5;
}
