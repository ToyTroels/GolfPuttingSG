using GolfSG.Core;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class ExpectedBirdiesSummaryTests
{
    [TestMethod]
    public void TotalsCompareOnlyCompletedGirHoles()
    {
        var result = ExpectedBirdiesSummary.Calculate(
        [
            new(1, 3, 1, 1.7, 0.7, GreenInRegulation: true),
            new(2, 8, 3, 2.2, -0.8, GreenInRegulation: true),
            new(3, 3, 1, 1.7, 0.7, GreenInRegulation: false),
            new(4, 3, 1, 1.7, 0.7),
            new(5, 0, 2, 2, 0, GreenInRegulation: true),
            new(6, 3, 0, 1.7, 0, GreenInRegulation: true),
            new(7, double.NaN, 2, 1.7, 0, GreenInRegulation: true)
        ]);
        Assert.AreEqual(2, result.GirHoleCount);
        var expected = PuttingMakeProbability.FromMeters(3) + PuttingMakeProbability.FromMeters(8);
        Assert.AreEqual(expected, result.ExpectedBirdies, 0.000001);
        Assert.AreEqual(1, result.ActualBirdies);
        Assert.AreEqual(1 - expected, result.Difference, 0.000001);
    }

    [TestMethod]
    [DataRow(1.0, 1.0)]
    [DataRow(3.0, 0.994)]
    [DataRow(8.0, 0.529)]
    [DataRow(10.0, 0.413)]
    [DataRow(15.0, 0.301)]
    [DataRow(20.0, 0.183)]
    [DataRow(25.0, 0.1247)]
    [DataRow(50.0, 0.0545)]
    public void ExpectedBirdiesUseMakeProbabilityRegardlessOfExpectedPutts(double feet, double expectedBirdies)
    {
        var result = ExpectedBirdiesSummary.Calculate(
            [new HolePuttingData(1, DistanceConversions.FeetToMeters(feet), 2, double.NaN, 0, GreenInRegulation: true)]);
        Assert.AreEqual(expectedBirdies, result.ExpectedBirdies, 0.000001);
        Assert.AreEqual(-expectedBirdies, result.Difference, 0.000001);
    }

    [TestMethod]
    public void InitialThirteenHoleExampleMatchesApproximateExpectation()
    {
        double[] distances = [0.9, 15.5, 12, 0.3, 12, 3, 6.5, 6.5, 2.3, 2.3, 6.5, 5.5, 4];
        var holes = distances.Select((distance, index) =>
            new HolePuttingData(index + 1, distance, index < 4 ? 1 : 2, 0, 0, GreenInRegulation: true));
        var result = ExpectedBirdiesSummary.Calculate(holes);
        Assert.AreEqual(4.580332283464567, result.ExpectedBirdies, 0.000001);
        Assert.AreEqual(4, result.ActualBirdies);
        Assert.AreEqual(-0.580332283464567, result.Difference, 0.000001);
    }

    [TestMethod]
    public void EmptyGirDataHasNoComparison()
    {
        Assert.AreEqual(new ExpectedBirdiesSummary(0, 0, 0), ExpectedBirdiesSummary.Calculate([]));
    }
}
