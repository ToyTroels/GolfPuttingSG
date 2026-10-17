using GolfSG.Application.Server;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresPersonalData(AppDbContext database) : IPersonalData
{
    public async Task<PersonalDataPage> ReadAsync(string accountId, string section, int offset, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        if (section is not ("rounds" or "courses" or "games") || offset is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(section));
        IQueryable<PersonalDataItem> query = section switch
        {
            "rounds" => database.SavedRounds.AsNoTracking().Where(r => r.UserId == accountId && r.Payload != null)
                .OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id)
                .Select(r => new PersonalDataItem(r.Id, "Saved round", "Synced", r.UpdatedAt, "Your saved round history", null)),
            "courses" => database.SharedCourses.AsNoTracking().Where(c => c.OwnerId == accountId)
                .OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id)
                .Select(c => new PersonalDataItem(c.Id.ToString(), c.Name, c.Published ? "Published" : "Unpublished", c.UpdatedAt,
                    c.Positions + " positions · " + c.Attempts + " attempts", null)),
            _ => database.CourseGames.AsNoTracking()
                .Where(g => g.HostId == accountId || database.GameParticipants.Any(p => p.GameId == g.Id && p.AccountId == accountId))
                .OrderByDescending(g => g.CreatedAt).ThenBy(g => g.Id)
                .Select(g => new PersonalDataItem(g.Id.ToString(), g.Name, g.Closed ? "Closed" : "Open", g.CreatedAt,
                    g.HostId == accountId ? "You host this game" : "You joined this game",
                    database.GameParticipants.Where(p => p.GameId == g.Id && p.AccountId == accountId).Select(p => p.TotalStrokesGained).FirstOrDefault()))
        };
        var items = await query.Skip(offset).Take(26).ToListAsync(cancellationToken);
        return new(section, items.Take(25).ToArray(), offset, items.Count > 25);
    }
}
