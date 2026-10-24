using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace GolfSG.Admin;

// Browser cookies contain only an opaque protected session handle. OAuth tokens
// stay in this process; restarting it signs everyone out. Single instance only.
public sealed class ServerTicketStore(IMemoryCache cache) : ITicketStore
{
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = "admin-ticket-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return Store(key, ticket);
    }
    private async Task<string> Store(string key, AuthenticationTicket ticket)
    {
        await RenewAsync(key, ticket);
        return key;
    }
    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        cache.Set(key, ticket, ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddMinutes(15));
        return Task.CompletedTask;
    }
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Task.FromResult(cache.Get<AuthenticationTicket>(key));
    public Task RemoveAsync(string key) { cache.Remove(key); return Task.CompletedTask; }
}
