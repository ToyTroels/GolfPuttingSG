using System.Data.Common;
using GolfSG.Application.Server;
using Microsoft.EntityFrameworkCore;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresConnectionCheck(AppDbContext db) : IServerConnectionCheck
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A readable Identity table verifies both connectivity and the account schema/grants.
            _ = await db.Users.AsNoTracking().AnyAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is DbException or TimeoutException ||
            exception is InvalidOperationException { InnerException: DbException or TimeoutException })
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }
}
