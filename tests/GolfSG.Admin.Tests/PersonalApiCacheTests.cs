using GolfSG.Admin;

namespace GolfSG.Admin.Tests;

[TestClass]
public sealed class PersonalApiCacheTests
{
    [TestMethod]
    public async Task ConcurrentReadsShareFetchAndRefreshReplacesCachedData()
    {
        using var cache = new PersonalApiCache();
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<(string?, int)> Load() { Interlocked.Increment(ref calls); await release.Task; return ("data", 4); }
        var key = PersonalApiCache.Key("alice", "session1", "performance");
        var reads = Enumerable.Range(0, 12).Select(_ => cache.GetAsync(key, Load, false, CancellationToken.None)).ToArray();
        release.SetResult();
        await Task.WhenAll(reads);
        Assert.AreEqual(1, calls);
        var refreshed = await cache.GetAsync(key, () => Task.FromResult<(string?, int)>(("updated", 7)), true, CancellationToken.None);
        Assert.AreEqual("updated", refreshed);
        Assert.AreEqual("updated", await cache.GetAsync(key, Load, false, CancellationToken.None));
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task FailedReadsAreRetriedAndAccountsSessionsAndResourcesAreIsolated()
    {
        using var cache = new PersonalApiCache();
        var key = PersonalApiCache.Key("alice", "token", "performance");
        var missing = await cache.GetAsync<string>(key, () => Task.FromResult<(string?, int)>((null, 0)), false, CancellationToken.None);
        Assert.IsNull(missing);
        Assert.AreEqual("ok", await cache.GetAsync(key, () => Task.FromResult<(string?, int)>(("ok", 2)), false, CancellationToken.None));
        foreach (var other in new[] { PersonalApiCache.Key("bob", "token", "performance"), PersonalApiCache.Key("alice", "new-token", "performance"), PersonalApiCache.Key("alice", "token", "round/1") })
            Assert.AreEqual("other", await cache.GetAsync(other, () => Task.FromResult<(string?, int)>(("other", 5)), false, CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GetAsync<string>(key, () => throw new HttpRequestException(), true, CancellationToken.None));
        Assert.AreEqual("retry", await cache.GetAsync(key, () => Task.FromResult<(string?, int)>(("retry", 5)), false, CancellationToken.None));
    }
}
