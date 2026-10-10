namespace GolfSG.Api.Models;

public sealed class SavedRound
{
    public string Id { get; set; } = string.Empty;
    public Guid Version { get; set; }
    public string? Payload { get; set; }
    public DateTime UpdatedAt { get; set; }
}
