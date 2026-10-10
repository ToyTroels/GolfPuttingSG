using System.Text.Json.Serialization;

namespace GolfSG.Core.Models;

public sealed record CoursePoint(double X, double Y)
{
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && X is >= 0 and <= 1 && Y is >= 0 and <= 1;
}

public enum PracticeShotCategory { Putting, Approach, AroundGreen }

public sealed record CoursePracticeShot(string Id, string Name, CoursePoint From, CoursePoint Target,
    ShotLie Lie, PracticeShotCategory Category, double? MeasuredDistanceMeters = null, int Attempts = 1)
{
    public double DistanceMeters(PracticeCourse course) => MeasuredDistanceMeters ??
        Math.Sqrt(Math.Pow((From.X - Target.X) * course.MetresPerImageWidth, 2) +
                  Math.Pow((From.Y - Target.Y) * course.MetresPerImageWidth / course.ImageAspectRatio, 2));
}

public sealed record PracticeCourse(string Id, string Name, string ImageFile, double ImageAspectRatio,
    double MetresPerImageWidth, string ScaleNote, IReadOnlyList<CoursePracticeShot> Shots);

public sealed record CourseShotOutcome(double EndDistanceMeters, ShotLie EndLie, int PenaltyStrokes, bool Holed);
public sealed record CourseShotResult(CourseShotOutcome Outcome, double ExpectedStart, double ExpectedFinish, double StrokesGained,
    string? ShotId = null);
public sealed record CoursePracticeDetails(PracticeCourse Course, IReadOnlyList<CourseShotResult> Results)
{
    [JsonIgnore]
    public IReadOnlyList<CoursePracticeShot> PlayedShots
    {
        get
        {
            var shots = Course.Shots.ToDictionary(shot => shot.Id, StringComparer.Ordinal);
            return Results.Select((result, index) => result.ShotId is null ? Course.Shots[index] : shots[result.ShotId]).ToArray();
        }
    }

    [JsonIgnore]
    public IReadOnlyList<CoursePracticeShot> ResultShots => PlayedShots;
}
