using GolfSG.Application.Services;

namespace GolfSG.Tests;

[TestClass]
public class GreenLevelCaptureTests
{
    [TestMethod]
    public void StableReadingCompletesEarlyOnceEnoughSamplesHaveSettled()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 0, 900);
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromMilliseconds(900)));
        capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(950));
        Assert.IsTrue(capture.IsComplete(TimeSpan.FromMilliseconds(950)));
    }

    [TestMethod]
    public void MissingOrStaleSensorDataDoesNotComplete()
    {
        var capture = new GreenLevelCapture();
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromSeconds(5)));
        Feed(capture, 0, 1000);
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void MovingPhoneMustSettleAgainBeforeCompletion()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 0, 1500);
        Assert.IsTrue(capture.IsComplete(TimeSpan.FromMilliseconds(1500)));
        capture.Add(0, 0, -1, TimeSpan.FromMilliseconds(1600));
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromMilliseconds(1600)));
        Feed(capture, 1700, 2000);
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromMilliseconds(2000)));
        Feed(capture, 2050, 2800);
        Assert.IsTrue(capture.IsComplete(TimeSpan.FromMilliseconds(2800)));
    }

    [TestMethod]
    public void CannotAcceptAReadingAfterThreeSecondDeadline()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 0, 3050);
        Assert.AreEqual(TimeSpan.FromSeconds(3), GreenLevelCapture.MaxDuration);
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromMilliseconds(3050)));
    }

    [TestMethod]
    public void TooFewReadingsDoNotCompleteEvenIfEnoughTimeHasPassed()
    {
        var capture = new GreenLevelCapture();
        capture.Add(0, 0, 1, TimeSpan.Zero);
        capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(900));
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromMilliseconds(900)));
    }

    [TestMethod]
    public void SensorGapRequiresFreshStableSamples()
    {
        var capture = new GreenLevelCapture();
        Feed(capture, 0, 1000);
        capture.Add(0, 0, 1, TimeSpan.FromSeconds(2));
        Assert.IsFalse(capture.IsComplete(TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void NewCaptureDoesNotReusePreviousResult()
    {
        var previous = new GreenLevelCapture();
        Feed(previous, 0, 2000);
        Assert.IsTrue(previous.IsComplete(TimeSpan.FromSeconds(2)));
        var next = new GreenLevelCapture();
        Assert.IsFalse(next.Filter.HasReading);
        Assert.IsFalse(next.IsComplete(TimeSpan.FromSeconds(2)));
    }

    private static void Feed(GreenLevelCapture capture, int from, int through)
    {
        for (int ms = from; ms <= through; ms += 50)
            capture.Add(0, 0, 1, TimeSpan.FromMilliseconds(ms));
    }
}
