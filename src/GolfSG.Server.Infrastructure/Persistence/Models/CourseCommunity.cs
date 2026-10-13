namespace GolfSG.Server.Infrastructure.Persistence.Models;

public sealed class SharedCourseImage
{
    public string Id { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
}
public sealed class SharedCourse
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Creator { get; set; } = "";
    public string SourceId { get; set; } = "";
    public Guid RevisionId { get; set; }
    public string Name { get; set; } = "";
    public int Positions { get; set; }
    public int Attempts { get; set; }
    public bool Published { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed class SharedCourseRevision
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string ContentHash { get; set; } = "";
    public string DefinitionJson { get; set; } = "";
    public string ImageId { get; set; } = "";
}
public sealed class SharedCourseGame
{
    public Guid Id { get; set; }
    public string HostId { get; set; } = "";
    public string HostName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string DefinitionJson { get; set; } = "";
    public string ImageId { get; set; } = "";
    public string ScoringVersion { get; set; } = "";
    public bool Closed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class SharedGameParticipant
{
    public Guid GameId { get; set; }
    public string AccountId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RoundId { get; set; } = "";
    public string? ResultsJson { get; set; }
    public double? TotalStrokesGained { get; set; }
}
