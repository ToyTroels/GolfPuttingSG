using GolfSG.Api.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class AdminUsersControllerTests
{
    [TestMethod]
    public async Task DisableProtectsAdministratorAndRevokesOrdinaryAccountAccess()
    {
        using var users = new Accounts();
        var controller = Controller(users);
        Assert.IsInstanceOfType<ConflictResult>(await controller.Disable("owner"));
        Assert.AreEqual(0, users.Stamps);
        Assert.IsInstanceOfType<NoContentResult>(await controller.Disable("player"));
        Assert.IsTrue(users.Account.LockoutEnabled);
        Assert.AreEqual(DateTimeOffset.MaxValue, users.Account.LockoutEnd);
        Assert.AreEqual(1, users.Stamps);
        Assert.IsInstanceOfType<NoContentResult>(await controller.Enable("player"));
        Assert.IsNull(users.Account.LockoutEnd);
        Assert.AreEqual(0, users.Account.AccessFailedCount);
        Assert.AreEqual(2, users.Stamps);
        Assert.IsInstanceOfType<NoContentResult>(await controller.RevokeSessions("player"));
        Assert.AreEqual(3, users.Stamps);
        Assert.IsInstanceOfType<NotFoundResult>(await controller.Disable("missing"));
    }

    [TestMethod]
    public async Task InvalidPaginationIsRejectedBeforeQueryingStorage()
    {
        using var users = new Accounts();
        var controller = Controller(users);
        Assert.IsInstanceOfType<BadRequestResult>(await controller.List(-1));
        Assert.IsInstanceOfType<BadRequestResult>(await controller.List(0, 101));
    }

    private static AdminUsersController Controller(Accounts users) => new(users,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Auth:SuperAdminAccountIds:0"] = "owner" }).Build(),
        NullLogger<AdminUsersController>.Instance)
        { ControllerContext = new() { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() } };

    private sealed class Accounts() : UserManager<IdentityUser>(new Store(), null!, null!, [], [], null!, null!, null!, null!)
    {
        public IdentityUser Account { get; } = new() { Id = "player", AccessFailedCount = 4 };
        public int Stamps { get; private set; }
        public override Task<IdentityUser?> FindByIdAsync(string id) => Task.FromResult(
            id == "owner" ? new IdentityUser { Id = "owner" } : id == "player" ? Account : null);
        public override Task<IdentityResult> SetLockoutEnabledAsync(IdentityUser user, bool enabled)
        { user.LockoutEnabled = enabled; return Task.FromResult(IdentityResult.Success); }
        public override Task<IdentityResult> SetLockoutEndDateAsync(IdentityUser user, DateTimeOffset? end)
        { user.LockoutEnd = end; return Task.FromResult(IdentityResult.Success); }
        public override Task<IdentityResult> ResetAccessFailedCountAsync(IdentityUser user)
        { user.AccessFailedCount = 0; return Task.FromResult(IdentityResult.Success); }
        public override Task<IdentityResult> UpdateSecurityStampAsync(IdentityUser user)
        { Stamps++; return Task.FromResult(IdentityResult.Success); }
    }

    private sealed class Store : IUserStore<IdentityUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(IdentityUser user, string? name, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(IdentityUser user, string? name, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityUser?> FindByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IdentityUser?> FindByNameAsync(string name, CancellationToken ct) => throw new NotSupportedException();
    }
}
