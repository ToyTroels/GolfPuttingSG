using GolfSG.Application.Services;

namespace GolfSG.Tests;

[TestClass]
public class GreenSlopeTests
{
    [TestMethod]
    [DataRow(GreenLevelHoleDirection.Top, 2d, 3d)]
    [DataRow(GreenLevelHoleDirection.Right, -3d, 2d)]
    [DataRow(GreenLevelHoleDirection.Bottom, -2d, -3d)]
    [DataRow(GreenLevelHoleDirection.Left, 3d, -2d)]
    public void HoleDirectionMapsBothAxesRelativeToPutt(GreenLevelHoleDirection direction, double right, double forward)
    {
        Assert.AreEqual(new GreenSlope(right, forward), new GreenSlope(2, 3).WithHoleDirection(direction));
        Assert.AreEqual(new GreenSlope(-right, -forward), new GreenSlope(-2, -3).WithHoleDirection(direction));
    }

    [TestMethod]
    public void FilterShowsFirstReadingBeforeItIsStable()
    {
        var filter = new GreenLevelFilter();
        Assert.IsTrue(filter.Add(0.02, 0, 0.97));
        Assert.IsTrue(filter.HasReading);
        Assert.IsFalse(filter.IsStable);
        Assert.AreEqual(100 * 0.02 / 0.97,
            GreenSlope.FromGravity(filter.X, filter.Y, filter.Z).RightPercent, 0.000001);
    }

    [TestMethod]
    public void OrdinaryNoiseDoesNotHideReadingOrPreventStability()
    {
        var filter = new GreenLevelFilter();
        for (int i = 0; i < 30; i++)
            Assert.IsTrue(filter.Add(0.02 + (i % 2 == 0 ? 0.004 : -0.004), 0, 0.97));
        Assert.IsTrue(filter.HasReading);
        Assert.IsTrue(filter.IsStable);
    }

    [TestMethod]
    public void FilterSettlesOnNewSlopeWithoutKeepingOldValue()
    {
        var filter = new GreenLevelFilter();
        for (int i = 0; i < 20; i++) filter.Add(0, 0, 1);
        filter.Add(0.1, 0, 0.995);
        Assert.IsFalse(filter.IsStable);
        Assert.IsTrue(filter.HasReading);
        for (int i = 0; i < 40; i++) filter.Add(0.1, 0, 0.995);
        Assert.IsTrue(filter.IsStable);
        Assert.AreEqual(100 * 0.1 / 0.995,
            GreenSlope.FromGravity(filter.X, filter.Y, filter.Z).RightPercent, 0.01);
    }

    [TestMethod]
    public void InvalidMotionClearsReadingAndNextGoodSampleRecovers()
    {
        var filter = new GreenLevelFilter();
        filter.Add(0, 0, 1);
        Assert.IsFalse(filter.Add(0, 0, -1));
        Assert.IsFalse(filter.HasReading);
        Assert.IsFalse(filter.Add(double.NaN, 0, 1));
        Assert.IsTrue(filter.Add(0, 0, 1));
        filter.Reset();
        Assert.IsFalse(filter.HasReading);
    }
    [TestMethod]
    public void FlatPhoneHasNoSlope()
    {
        Assert.AreEqual(new GreenSlope(0, 0), GreenSlope.FromGravity(0, 0, 1));
    }

    [TestMethod]
    public void KnownRiseAndFallHaveCorrectSignsAndPercentages()
    {
        double scale = Math.Sqrt(1 + 0.02 * 0.02 + 0.03 * 0.03);
        var result = GreenSlope.FromGravity(0.02 / scale, -0.03 / scale, 1 / scale);
        Assert.AreEqual(2, result.RightPercent, 0.000001);
        Assert.AreEqual(-3, result.ForwardPercent, 0.000001);
    }

    [TestMethod]
    public void CalibrationRemovesAngularBias()
    {
        const double bias = 0.01;
        var result = GreenSlope.FromGravity(Math.Sin(bias), 0, Math.Cos(bias), bias);
        Assert.AreEqual(0, result.RightPercent, 0.000001);
        var tilted = GreenSlope.FromGravity(Math.Sin(bias + Math.Atan(0.02)), 0,
            Math.Cos(bias + Math.Atan(0.02)), bias);
        Assert.AreEqual(2, tilted.RightPercent, 0.000001);
    }

    [TestMethod]
    public void RejectsUpsideDownAndInvalidReadings()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GreenSlope.FromGravity(0, 0, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GreenSlope.FromGravity(double.NaN, 0, 1));
    }
}
