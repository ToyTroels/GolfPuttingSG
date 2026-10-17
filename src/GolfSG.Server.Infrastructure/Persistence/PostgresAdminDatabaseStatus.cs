using GolfSG.Application.Server;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace GolfSG.Server.Infrastructure.Persistence;

public sealed class PostgresAdminDatabaseStatus(AppDbContext database) : IAdminDatabaseStatus
{
    public async Task<AdminDatabaseStatus> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = database.Database.GetDbConnection().CreateCommand();
                command.CommandTimeout = 10;
                command.CommandText = "SELECT version FROM public.golfsg_database_release WHERE singleton = TRUE";
                return new(true, await command.ExecuteScalarAsync(cancellationToken) as string);
            }
            finally { await database.Database.CloseConnectionAsync(); }
        }
        catch (DbException) { return new(false, null); }
        catch (TimeoutException) { return new(false, null); }
    }
}
