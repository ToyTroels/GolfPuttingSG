using GolfSG.Application.Services;

namespace GolfSG.Tests;

[TestClass]
public sealed class PuttDistanceEstimateTests
{
    [TestMethod]
    [DataRow(20d, true)]
    [DataRow(150d, true)]
    [DataRow(19.9, false)]
    [DataRow(150.1, false)]
    [DataRow(double.NaN, false)]
    [DataRow(double.PositiveInfinity, false)]
    public void StepLengthValidationRejectsInvalidAndNonFiniteValues(double length, bool valid) =>
        Assert.AreEqual(valid, PuttDistanceEstimate.IsValidStepLength(length));

    [TestMethod]
    public void DistanceConversionUsesCentimetersAndRoundsToOneDecimal()
    {
        Assert.AreEqual(1.5, PuttDistanceEstimate.Meters(2, 75));
        Assert.AreEqual(2.2, PuttDistanceEstimate.Meters(3, 74));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PuttDistanceEstimate.Meters(-1, 75));
    }

    [TestMethod]
    public void ApplyRangeMatchesFirstPuttInputLimit()
    {
        Assert.IsFalse(PuttDistanceEstimate.CanApply(0));
        Assert.IsTrue(PuttDistanceEstimate.CanApply(30));
        Assert.IsFalse(PuttDistanceEstimate.CanApply(30.1));
        Assert.IsFalse(PuttDistanceEstimate.CanApply(double.NaN));
    }
}
