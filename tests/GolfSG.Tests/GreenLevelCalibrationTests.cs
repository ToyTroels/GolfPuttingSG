using GolfSG.Application.Services;

namespace GolfSG.Tests;

[TestClass]
public sealed class GreenLevelCalibrationTests
{
    [TestMethod]
    public void RejectsInvalidSavedOffsets()
    {
        Assert.IsFalse(new GreenLevelCalibration(double.NaN, 0).IsValid);
        Assert.IsFalse(new GreenLevelCalibration(0, double.PositiveInfinity).IsValid);
        Assert.IsFalse(new GreenLevelCalibration(0.201, 0).IsValid);
        Assert.IsTrue(new GreenLevelCalibration(0, 0).IsValid);
    }

    [TestMethod]
    public void CalibrationFromReadingRemovesBothAxisBiases()
    {
        var reading = new GreenLevelReading(0.02, -0.03, 0.999);
        var calibration = GreenLevelCalibration.FromReading(reading);
        var corrected = reading.ToSlope(calibration.RightRadians, calibration.ForwardRadians);
        Assert.AreEqual(0, corrected.RightPercent, 0.000001);
        Assert.AreEqual(0, corrected.ForwardPercent, 0.000001);
    }

    [TestMethod]
    public void CannotCalibrateAnUpsideDownPhoneOrPassInvalidOffsetsIntoSlopeMath()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GreenLevelCalibration.FromReading(new(0, 0, -1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GreenSlope.FromGravity(0, 0, 1, double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GreenSlope.FromGravity(0, 0, 1, 0, 1));
    }
}
