namespace GolfSG.Application.Common;

public readonly record struct AsyncActionResult<T>(bool Executed, T? Value);

public sealed class AsyncActionGate
{
    private int isRunning;

    public bool IsRunning => Volatile.Read(ref isRunning) == 1;

    public async Task<bool> RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            await action();
            return true;
        }
        finally
        {
            Volatile.Write(ref isRunning, 0);
        }
    }

    public async Task<AsyncActionResult<T>> RunAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            return new AsyncActionResult<T>(false, default);
        }

        try
        {
            return new AsyncActionResult<T>(true, await action());
        }
        finally
        {
            Volatile.Write(ref isRunning, 0);
        }
    }
}
