using GolfSG.Application.Services;

namespace GolfSG.Tests;

[TestClass]
public class GreenLevelCaptureTests
{
    [TestMethod]
    public void FreezesFirstStableResultBeforeThreeSeconds()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 0, 900);
        Assert.IsFalse(capture.TryGetResult(out _));
        capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(950));
        Assert.IsTrue(capture.TryGetResult(out var result));
        capture.Add(0.1, 0, 0.995, TimeSpan.FromMilliseconds(1000));
        Assert.IsTrue(capture.TryGetResult(out var unchanged));
        Assert.AreEqual(result, unchanged);
    }

    [TestMethod]
    public void MissingAndSparseSamplesDoNotComplete()
    {
        var capture = new GreenLevelCapture();
        Assert.IsFalse(capture.TryGetResult(out _));
        for (int ms = 0; ms <= 3000; ms += 300)
            capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(ms));
        Assert.IsFalse(capture.TryGetResult(out _));
    }

    [TestMethod]
    public void MovingPhoneMustSettleAgainBeforeCompletion()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 0, 800);
        capture.Add(0, 0, -1, TimeSpan.FromMilliseconds(850));
        Feed(capture, 900, 1800);
        Assert.IsFalse(capture.TryGetResult(out _));
        capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(1850));
        Assert.IsTrue(capture.TryGetResult(out _));
    }

    [TestMethod]
    public void IgnoresSamplesAfterDeadline()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 2500, 4000);
        Assert.AreEqual(TimeSpan.FromSeconds(3), GreenLevelCapture.MaxDuration);
        Assert.IsFalse(capture.TryGetResult(out _));
    }

    [TestMethod]
    public void CompletionAtDeadlineSurvivesDelayedUiPolling()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 2050, 3000);
        capture.Add(0, 0, -1, TimeSpan.FromSeconds(4));
        Assert.IsTrue(capture.TryGetResult(out _));
    }

    [TestMethod]
    public void SensorGapRequiresFreshStableSamples()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 0, 800);
        capture.Add(0, 0, 1, TimeSpan.FromSeconds(2));
        Assert.IsFalse(capture.TryGetResult(out _));
    }

    [TestMethod]
    public void DuplicateAndOutOfOrderTimestampsCannotManufactureStability()
    {
        var capture = new GreenLevelCapture();
        for (int i = 0; i < 100; i++)
            capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(50));
        capture.Add(0, 0, 1, TimeSpan.Zero);
        capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(550));
        Assert.IsFalse(capture.TryGetResult(out _));
    }

    [TestMethod]
    public void NewCaptureDoesNotReusePreviousResult()
    {
        var previous = new GreenLevelCapture();
        Feed(previous, 0, 1000);
        Assert.IsTrue(previous.TryGetResult(out _));
        Assert.IsFalse(new GreenLevelCapture().TryGetResult(out _));
    }

    private static void Feed(GreenLevelCapture capture, int from, int through)
    {
        for (int ms = from; ms <= through; ms += 50)
            capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(ms));
    }
}
