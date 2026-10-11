using System.ComponentModel.DataAnnotations;
using System.Reflection;
using GolfSG.Api.Auth;
using GolfSG.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class AuthContractTests
{
    [TestMethod]
    public void AccountIdentityRequiresBearerWhileCredentialEndpointsAllowAnonymousAccess()
    {
        var controller = typeof(AuthController);
        var me = controller.GetMethod(nameof(AuthController.Me))!;
        Assert.AreEqual(IdentityConstants.BearerScheme,
            me.GetCustomAttribute<AuthorizeAttribute>()!.AuthenticationSchemes);
        Assert.IsNull(me.GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var name in new[] { nameof(AuthController.Register), nameof(AuthController.Login), nameof(AuthController.Refresh) })
            Assert.IsNotNull(controller.GetMethod(name)!.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [TestMethod]
    [DataRow("", "GolfTest!12345")]
    [DataRow(" ", "GolfTest!12345")]
    [DataRow("local-player", "")]
    public void MissingCredentialsFailRequestValidation(string username, string password)
    {
        var credentials = new AccountCredentialsRequest { Username = username, Password = password };

        Assert.IsFalse(Validator.TryValidateObject(credentials, new ValidationContext(credentials), [], true));
    }

    [TestMethod]
    public void OversizedCredentialsAndRefreshTokensFailRequestValidation()
    {
        var credentials = new AccountCredentialsRequest { Username = new string('a', 51), Password = "GolfTest!12345" };
        Assert.IsFalse(Validator.TryValidateObject(credentials, new ValidationContext(credentials), [], true));
        credentials = new AccountCredentialsRequest { Username = "local-player", Password = new string('a', 129) };
        Assert.IsFalse(Validator.TryValidateObject(credentials, new ValidationContext(credentials), [], true));
        var refresh = new RefreshTokenRequest { RefreshToken = new string('a', 16385) };
        Assert.IsFalse(Validator.TryValidateObject(refresh, new ValidationContext(refresh), [], true));
    }

    [TestMethod]
    public void AccountResponsesExcludePasswordAndAuthenticationPayloadsHaveBoundedBodySize()
    {
        CollectionAssert.AreEquivalent(new[] { "Id", "Username" },
            typeof(AccountIdentityDto).GetProperties().Select(property => property.Name).ToArray());
        var limit = (IRequestSizeLimitMetadata)typeof(AuthController).GetCustomAttribute<RequestSizeLimitAttribute>()!;
        Assert.AreEqual(32L * 1024, limit.MaxRequestBodySize);
    }
}
