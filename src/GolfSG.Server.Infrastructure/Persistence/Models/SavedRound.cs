namespace GolfSG.Server.Infrastructure.Persistence.Models;

public sealed class SavedRound
{
    public string UserId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public Guid Version { get; set; }
    public string? Payload { get; set; }
    public DateTime UpdatedAt { get; set; }
}
