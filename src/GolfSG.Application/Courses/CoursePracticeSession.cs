using System.Text.Json.Serialization;
using GolfSG.Core.Models;

namespace GolfSG.Application.Courses;

public sealed record CourseResultInput(string Distance = "", ShotLie Lie = ShotLie.Green, int Penalties = 0, bool Holed = false);
public sealed record CoursePracticeSession(string Id, DateTime Date, PracticeCourse Course,
    IReadOnlyList<CourseShotResult> Results, CourseResultInput Input, IReadOnlyList<string>? ShotOrder = null)
{
    public bool IsComplete => Results.Count == TotalAttempts;

    [JsonIgnore]
    public int TotalAttempts => Course.Shots.Sum(shot => shot.Attempts);

    [JsonIgnore]
    public IReadOnlyList<CoursePracticeShot> OrderedShots
    {
        get
        {
            if (ShotOrder is null) return Course.Shots;
            var shots = Course.Shots.ToDictionary(shot => shot.Id, StringComparer.Ordinal);
            return ShotOrder.Select(id => shots[id]).ToArray();
        }
    }

    [JsonIgnore]
    public IReadOnlyList<CoursePracticeShot> PlayedShots => new CoursePracticeDetails(Course with { Shots = OrderedShots }, Results).PlayedShots;

    public int CompletedAttempts(string shotId) => PlayedShots.Count(shot => shot.Id == shotId);

    [JsonIgnore]
    public CoursePracticeShot? CurrentShot
    {
        get
        {
            if (IsComplete) return null;
            var counts = PlayedShots.GroupBy(shot => shot.Id).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            return OrderedShots.First(shot => counts.GetValueOrDefault(shot.Id) < shot.Attempts);
        }
    }

    [JsonIgnore]
    public int CurrentAttempt => CurrentShot is { } shot ? CompletedAttempts(shot.Id) + 1 : 0;
}
public sealed record CoursePracticeDocument(IReadOnlyList<PracticeCourse> Courses, CoursePracticeSession? Active)
{
    public static CoursePracticeDocument Empty => new([], null);
}

public interface ICoursePracticeRepository
{
    Task<CoursePracticeDocument> LoadAsync();
    Task SaveAsync(CoursePracticeDocument document);
}
