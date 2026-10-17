using System.Security.Claims;
using GolfSG.Api.Auth;
using Microsoft.Extensions.Configuration;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class SuperAdminAccessTests
{
    [TestMethod]
    public void OnlyAuthenticatedProvisionedAccountIdsReceiveAccess()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Auth:SuperAdminAccountIds:0"] = "owner-id" }).Build();
        Assert.IsTrue(SuperAdminAccess.IsAllowed(Principal("owner-id", true), configuration));
        Assert.IsFalse(SuperAdminAccess.IsAllowed(Principal("owner-id", true, false), configuration));
        Assert.IsFalse(SuperAdminAccess.IsAllowed(Principal("owner-id", false), configuration));
        Assert.IsFalse(SuperAdminAccess.IsAllowed(Principal("other-id", true), configuration));
        Assert.IsFalse(SuperAdminAccess.IsAllowed(Principal("OWNER-ID", true), configuration));
        Assert.IsFalse(SuperAdminAccess.IsAllowed(Principal("owner-id", true), new ConfigurationBuilder().Build()));
        var roleOnly = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "SuperAdmin"), new Claim(ClaimTypes.Name, "owner-id")], "test"));
        Assert.IsFalse(SuperAdminAccess.IsAllowed(roleOnly, configuration));
    }

    [TestMethod]
    [DataRow("short", false)]
    [DataRow("LongButNoNumbers!", false)]
    [DataRow("longbutnouppercase1!", false)]
    [DataRow("LONGWITHNOLOWERCASE1!", false)]
    [DataRow("NoSpecialCharacter123", false)]
    [DataRow("SpacesAreNotSymbols1 ", false)]
    [DataRow("StrongAdmin!12345", true)]
    public void AdminPasswordsAlwaysRequireLengthAndComplexity(string password, bool expected) =>
        Assert.AreEqual(expected, SuperAdminAccess.HasStrongPassword(password));

    private static ClaimsPrincipal Principal(string id, bool authenticated, bool adminLogin = true)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], authenticated ? "test" : null);
        if (adminLogin) identity.AddClaim(new Claim(SuperAdminAccess.AuthenticationClaim, "true"));
        return new(identity);
    }
}
