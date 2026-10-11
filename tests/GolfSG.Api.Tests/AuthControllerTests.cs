using System.Security.Claims;
using GolfSG.Api.Auth;
using GolfSG.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class AuthControllerTests
{
    [TestMethod]
    public void MeReturnsAuthenticatedIdentityWithoutUserManagerLookup()
    {
        var controller = Controller(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "account-1"), new Claim(ClaimTypes.Name, "local-player")],
            IdentityConstants.BearerScheme));

        var response = controller.Me().Result;

        Assert.IsInstanceOfType<OkObjectResult>(response);
        var account = ((OkObjectResult)response).Value as AccountIdentityDto;
        Assert.IsNotNull(account);
        Assert.AreEqual("account-1", account.Id);
        Assert.AreEqual("local-player", account.Username);
    }

    [TestMethod]
    public void MeRejectsAnonymousIdentityEvenWhenItContainsAccountClaims()
    {
        var controller = Controller(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "account-1"), new Claim(ClaimTypes.Name, "local-player")]));

        Assert.IsInstanceOfType<UnauthorizedResult>(controller.Me().Result);
    }

    [TestMethod]
    public void MeRejectsPrincipalWithoutAnIdentity()
    {
        var controller = Controller(new ClaimsIdentity());
        controller.HttpContext.User = new ClaimsPrincipal();

        Assert.IsInstanceOfType<UnauthorizedResult>(controller.Me().Result);
    }

    [TestMethod]
    [DataRow(null, "local-player")]
    [DataRow("", "local-player")]
    [DataRow(" ", "local-player")]
    [DataRow("account-1", null)]
    [DataRow("account-1", "")]
    [DataRow("account-1", " ")]
    public void MeRejectsMissingOrBlankAccountClaims(string? userId, string? username)
    {
        var claims = new List<Claim>();
        if (userId is not null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (username is not null) claims.Add(new Claim(ClaimTypes.Name, username));
        var controller = Controller(new ClaimsIdentity(claims, IdentityConstants.BearerScheme));

        Assert.IsInstanceOfType<UnauthorizedResult>(controller.Me().Result);
    }

    [TestMethod]
    public void MeDoesNotCombineAuthenticatedIdentityWithAnonymousAccountClaims()
    {
        var controller = Controller(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "local-player")], IdentityConstants.BearerScheme));
        controller.User.AddIdentity(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "spoofed-account")]));

        Assert.IsInstanceOfType<UnauthorizedResult>(controller.Me().Result);
    }

    private static AuthController Controller(ClaimsIdentity identity) => new(null!, null!, null!, TimeProvider.System)
    {
        // Identity services are deliberately absent: this endpoint must resolve the validated token locally.
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        }
    };
}
