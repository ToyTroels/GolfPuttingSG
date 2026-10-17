using GolfSG.Api.Controllers;
using Microsoft.AspNetCore.Http;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class AdminPageTests
{
    [TestMethod]
    public void AdminPageUsesUniqueNonceAndNeverPersistsTokens()
    {
        var controller = new AdminPageController { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var first = controller.Index().Content!;
        var policy = controller.Response.Headers["Content-Security-Policy"].ToString();
        Assert.Contains("script-src 'nonce-", policy);
        Assert.DoesNotContain("unsafe-inline", policy);
        Assert.DoesNotContain("ADMIN_NONCE", first);
        Assert.Contains("/api/auth/admin-login", first);
        Assert.DoesNotContain("localStorage", first);
        Assert.DoesNotContain("sessionStorage", first);
        Assert.AreEqual("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.AreNotEqual(first, controller.Index().Content);
    }
}
