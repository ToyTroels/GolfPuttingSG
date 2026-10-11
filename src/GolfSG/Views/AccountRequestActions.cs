namespace GolfSG.Views;

internal sealed class AccountRequestActions
{
    private CancellationTokenSource? active;

    public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken pageToken)
    {
        if (active is not null || pageToken.IsCancellationRequested) return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(pageToken);
        active = request;
        try { await action(request.Token); }
        finally { active = null; }
    }

    public void Cancel() => active?.Cancel();
}
