namespace GolfSG.Application.Server;

public sealed record AdminDatabaseStatus(bool Available, string? Version);

public interface IAdminDatabaseStatus
{
    Task<AdminDatabaseStatus> ReadAsync(CancellationToken cancellationToken);
}
