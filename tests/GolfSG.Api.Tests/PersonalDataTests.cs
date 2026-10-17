using System.Security.Claims;
using GolfSG.Api.Controllers;
using GolfSG.Application.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class PersonalDataTests
{
    [TestMethod]
    public async Task UsesAuthenticatedAccountAndRejectsInvalidQueries()
    {
        var store = new Capture();
        var controller = new PersonalDataController(store) { ControllerContext = new() { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "alice")], "Identity.Bearer")) } } };
        Assert.IsInstanceOfType<OkObjectResult>(await controller.Get("rounds"));
        Assert.AreEqual("alice", store.Account);
        Assert.AreEqual("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.IsInstanceOfType<NotFoundResult>(await controller.Get("users"));
        Assert.IsInstanceOfType<BadRequestResult>(await controller.Get("rounds", -1));
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsInstanceOfType<UnauthorizedResult>(await controller.Get("rounds"));
    }
    private sealed class Capture : IPersonalData
    {
        public string? Account { get; private set; }
        public Task<PersonalDataPage> ReadAsync(string accountId, string section, int offset, CancellationToken cancellationToken)
        { Account = accountId; return Task.FromResult(new PersonalDataPage(section, [], offset, false)); }
    }
}
