namespace GolfSG.Services;

public interface IPuttStepCounter : IDisposable
{
    event Action<int>? StepsChanged;
    event Action<string>? Failed;
    Task StartAsync(CancellationToken cancellationToken = default);
}
