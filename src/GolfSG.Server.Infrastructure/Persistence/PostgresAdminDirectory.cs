using GolfSG.Application.Server;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresAdminDirectory(AppDbContext database) : IAdminDirectory
{
    public async Task<AdminDirectoryPage> ReadAsync(string section, string search, int offset, int limit, CancellationToken cancellationToken)
    {
        if (section is not ("users" or "courses" or "games") || search.Length > 100 || offset is < 0 or > 100000 || limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(section), "Invalid directory query.");
        var query = search.ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;
        IQueryable<AdminDirectoryRow> rows = section switch
        {
            "users" => database.Users.AsNoTracking()
                .Where(u => query == "" || u.Id == search || (u.UserName != null && u.UserName.ToLower().Contains(query)))
                .OrderBy(u => u.UserName).ThenBy(u => u.Id)
                .Select(u => new AdminDirectoryRow(u.Id, u.UserName ?? "Unnamed", "—",
                    u.LockoutEnabled && u.LockoutEnd > now ? "Locked" : "Active", null, null, null, null, null, null)),
            "courses" => database.SharedCourses.AsNoTracking()
                .Where(c => query == "" || c.Name.ToLower().Contains(query) || c.Creator.ToLower().Contains(query))
                .OrderBy(c => c.Name).ThenBy(c => c.Id)
                .Select(c => new AdminDirectoryRow(c.Id.ToString(), c.Name, c.Creator, c.Published ? "Published" : "Unpublished",
                    c.Positions, c.Attempts, null, null, null, c.UpdatedAt)),
            _ => database.CourseGames.AsNoTracking()
                .Where(g => query == "" || g.Name.ToLower().Contains(query) || g.HostName.ToLower().Contains(query))
                .OrderBy(g => g.Name).ThenBy(g => g.Id)
                .Select(g => new AdminDirectoryRow(g.Id.ToString(), g.Name, g.HostName, g.Closed ? "Closed" : "Open", null, null,
                    database.GameParticipants.Count(p => p.GameId == g.Id),
                    database.GameParticipants.Count(p => p.GameId == g.Id && p.ResultsJson != null), g.ScoringVersion, g.CreatedAt))
        };
        var items = await rows.Skip(offset).Take(limit + 1).ToListAsync(cancellationToken);
        return new(section, items.Take(limit).ToArray(), items.Count > limit, offset, limit, search);
    }
}
