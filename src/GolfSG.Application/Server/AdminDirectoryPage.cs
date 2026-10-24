namespace GolfSG.Application.Server;

public sealed record AdminDirectoryRow(string Id, string Name, string Owner, string Status,
    int? Positions = null, int? Attempts = null, int? Participants = null, int? Submitted = null,
    string? ScoringVersion = null, DateTimeOffset? UpdatedAt = null);

public sealed record AdminDirectoryPage(string Section, IReadOnlyList<AdminDirectoryRow> Items,
    bool HasMore, int Offset, int Limit, string Search);

public interface IAdminDirectory
{
    Task<AdminDirectoryPage> ReadAsync(string section, string search, int offset, int limit, CancellationToken cancellationToken);
}
