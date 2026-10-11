using Microsoft.Extensions.Configuration;

namespace GolfSG.Database.Setup;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<Program>(optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();
            await DatabaseSetup.UpdateAsync(configuration, cancellation.Token);
            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Console.Error.WriteLine("Database setup cancelled. Uncommitted changes were rolled back.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Database setup failed: {ex.Message}");
            return 1;
        }
    }
}
