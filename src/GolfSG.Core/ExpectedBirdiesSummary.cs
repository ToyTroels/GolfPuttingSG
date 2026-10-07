using GolfSG.Core.Models;

namespace GolfSG.Core;

public sealed record ExpectedBirdiesSummary(int GirHoleCount, double ExpectedBirdies, int ActualBirdies)
{
    public double Difference => ActualBirdies - ExpectedBirdies;

    public static ExpectedBirdiesSummary Calculate(IEnumerable<HolePuttingData> holes)
    {
        var included = holes.Where(hole => hole.GreenInRegulation == true && hole.IsCompleted &&
            double.IsFinite(hole.FirstPuttDistanceMeters)).ToList();
        return new(included.Count, included.Sum(hole => PuttingMakeProbability.FromMeters(hole.FirstPuttDistanceMeters)),
            included.Count(hole => hole.Putts == 1));
    }
}
