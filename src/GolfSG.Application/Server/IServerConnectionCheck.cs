namespace GolfSG.Application.Server;

/// <summary>Checks whether the server can reach the account database without exposing connection details.</summary>
public interface IServerConnectionCheck
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
