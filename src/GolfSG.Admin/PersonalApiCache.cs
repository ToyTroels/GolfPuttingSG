using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace GolfSG.Admin;

// Separate size budget from authentication tickets. Never cache credentials or auth decisions.
public sealed class PersonalApiCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 32 * 1024 * 1024 });
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();
    public static string Key(string owner, string token, string path) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(owner + "\n" + token + "\n" + path)));

    public async Task<T?> GetAsync<T>(string key, Func<Task<(T? Value, int Bytes)>> load, bool refresh, CancellationToken cancellationToken) where T : class
    {
        if (!refresh && cache.TryGetValue(key, out T? hit)) return hit;
        var gate = gates[(int)(Convert.ToUInt32(key[..8], 16) % (uint)gates.Length)];
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (refresh) cache.Remove(key);
            else if (cache.TryGetValue(key, out T? cached)) return cached;
            var result = await load();
            if (result.Value is not null && result.Bytes is > 0 and <= 2 * 1024 * 1024)
                cache.Set(key, result.Value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30), Size = result.Bytes });
            return result.Value;
        }
        finally { gate.Release(); }
    }
    public void Invalidate() => cache.Compact(1);
    public void Dispose() { cache.Dispose(); foreach (var gate in gates) gate.Dispose(); }
}
