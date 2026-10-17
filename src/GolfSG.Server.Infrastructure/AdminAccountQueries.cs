using GolfSG.Application.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure;

public static class AdminAccountQueries
{
    public static async Task<IReadOnlyList<AdminAccountSummary>> ListAccountsAsync(
        this UserManager<IdentityUser> users, int offset, int limit, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await users.Users.AsNoTracking().OrderBy(u => u.Id).Skip(offset).Take(limit)
            .Select(u => new AdminAccountSummary(u.Id, u.UserName,
                u.LockoutEnabled && u.LockoutEnd > now, u.LockoutEnd))
            .ToListAsync(cancellationToken);
    }
}
