using GolfSG.Core.Models;

namespace GolfSG.Application.Courses;

public static class CourseShotPositioning
{
    public static CoursePracticeShot MoveEndpoint(CoursePracticeShot shot, bool from, CoursePoint position)
    {
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
            throw new ArgumentOutOfRangeException(nameof(position), "The position must be finite.");

        var clamped = new CoursePoint(Math.Clamp(position.X, 0, 1), Math.Clamp(position.Y, 0, 1));
        if ((from ? shot.From : shot.Target) == clamped) return shot;

        return from
            ? shot with { From = clamped, MeasuredDistanceMeters = null }
            : shot with { Target = clamped, MeasuredDistanceMeters = null };
    }

    public static CoursePracticeShot Translate(CoursePracticeShot shot, double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX)) throw new ArgumentOutOfRangeException(nameof(deltaX));
        if (!double.IsFinite(deltaY)) throw new ArgumentOutOfRangeException(nameof(deltaY));

        // Move both ends together, stopping at the first image edge the shot reaches.
        deltaX = Math.Clamp(deltaX, -Math.Min(shot.From.X, shot.Target.X), 1 - Math.Max(shot.From.X, shot.Target.X));
        deltaY = Math.Clamp(deltaY, -Math.Min(shot.From.Y, shot.Target.Y), 1 - Math.Max(shot.From.Y, shot.Target.Y));
        if (deltaX == 0 && deltaY == 0) return shot;

        return shot with
        {
            From = new(shot.From.X + deltaX, shot.From.Y + deltaY),
            Target = new(shot.Target.X + deltaX, shot.Target.Y + deltaY)
        };
    }
}
