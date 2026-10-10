using GolfSG.Application.Courses;
using GolfSG.Core.Models;

namespace GolfSG.Tests;

[TestClass]
public sealed class CourseShotPositioningTests
{
    private static CoursePracticeShot Shot() => new("shot", "Bunker to green", new(.2, .8), new(.7, .3),
        ShotLie.Sand, PracticeShotCategory.AroundGreen, 35);

    private static PracticeCourse Course(CoursePracticeShot shot) => new("course", "Facility", "sample.jpg", .5,
        100, "Known scale", [shot]);

    [TestMethod]
    public void MovingStartClampsToImageAndKeepsShotDetails()
    {
        var original = Shot();
        var moved = CourseShotPositioning.MoveEndpoint(original, true, new(-.1, 1.2));

        Assert.AreEqual(new CoursePoint(0, 1), moved.From);
        Assert.AreEqual(original.Target, moved.Target);
        Assert.AreEqual(original.Id, moved.Id);
        Assert.AreEqual(original.Name, moved.Name);
        Assert.AreEqual(original.Lie, moved.Lie);
        Assert.AreEqual(original.Category, moved.Category);
        Assert.IsNull(moved.MeasuredDistanceMeters);
        Assert.AreEqual(35, original.MeasuredDistanceMeters);
    }

    [TestMethod]
    public void MovingTargetUsesNewGeometryInsteadOfOldMeasuredDistance()
    {
        var original = Shot();
        var moved = CourseShotPositioning.MoveEndpoint(original, false, new(.2, .3));

        Assert.AreEqual(original.From, moved.From);
        Assert.AreEqual(new CoursePoint(.2, .3), moved.Target);
        Assert.AreEqual(100, moved.DistanceMeters(Course(moved)), .000001);
        Assert.AreEqual(35, original.DistanceMeters(Course(original)));
    }

    [TestMethod]
    public void UnchangedEndpointPreservesMeasuredDistanceAndInstance()
    {
        var original = Shot();
        Assert.AreSame(original, CourseShotPositioning.MoveEndpoint(original, true, new(.2, .8)));

        var edge = original with { Target = new(1, 0) };
        Assert.AreSame(edge, CourseShotPositioning.MoveEndpoint(edge, false, new(1.2, -.3)));
    }

    [TestMethod]
    [DataRow(2d, 2d, .3d, .2d)]
    [DataRow(-2d, -2d, -.2d, -.3d)]
    [DataRow(.1d, -.1d, .1d, -.1d)]
    public void TranslatingShotStopsAtEdgesWithoutChangingItsLength(double requestedX, double requestedY,
        double appliedX, double appliedY)
    {
        var original = Shot() with { MeasuredDistanceMeters = null };
        var moved = CourseShotPositioning.Translate(original, requestedX, requestedY);

        Assert.AreEqual(original.From.X + appliedX, moved.From.X, .000001);
        Assert.AreEqual(original.From.Y + appliedY, moved.From.Y, .000001);
        Assert.AreEqual(original.Target.X + appliedX, moved.Target.X, .000001);
        Assert.AreEqual(original.Target.Y + appliedY, moved.Target.Y, .000001);
        Assert.IsTrue(moved.From.IsValid);
        Assert.IsTrue(moved.Target.IsValid);
        Assert.AreEqual(original.DistanceMeters(Course(original)), moved.DistanceMeters(Course(moved)), .000001);
        Assert.AreEqual(original.Target.X - original.From.X, moved.Target.X - moved.From.X, .000001);
        Assert.AreEqual(original.Target.Y - original.From.Y, moved.Target.Y - moved.From.Y, .000001);
    }

    [TestMethod]
    public void TranslatingPreservesMeasuredDistanceAndShotDetails()
    {
        var original = Shot();
        var moved = CourseShotPositioning.Translate(original, .1, -.1);

        Assert.AreEqual(original with { From = moved.From, Target = moved.Target }, moved);
        Assert.AreEqual(35, moved.DistanceMeters(Course(moved)));
        Assert.AreSame(original, CourseShotPositioning.Translate(original, 0, 0));

        var atEdge = original with { From = new(0, .8), Target = new(.7, 1) };
        Assert.AreSame(atEdge, CourseShotPositioning.Translate(atEdge, -1, 1));
    }

    [TestMethod]
    public void InvalidPointerCoordinatesCannotProduceInvalidShotPositions()
    {
        var original = Shot();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CourseShotPositioning.MoveEndpoint(original, true, new(double.NaN, .5)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CourseShotPositioning.Translate(original, double.PositiveInfinity, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CourseShotPositioning.Translate(original, 0, double.NaN));
    }
}
