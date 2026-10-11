using System.ComponentModel.DataAnnotations;
using GolfSG.Api.Auth;
using GolfSG.Server.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GolfSG.Api.Tests;

[TestClass]
public sealed class IdentityPasswordPolicyTests
{
    [TestMethod]
    [DataRow("a")]
    [DataRow("1234")]
    [DataRow("ordinarypassword")]
    public async Task SimplePasswordsWorkOnlyInDebugBuilds(string password)
    {
        var result = await ValidatePassword(password);

#if DEBUG
        Assert.IsTrue(result.Succeeded, "Debug registration should accept a nonempty simple password.");
#else
        Assert.IsFalse(result.Succeeded, "Release registration must retain password length and complexity requirements.");
        Assert.IsTrue(result.Errors.Any(error => error.Code == "PasswordTooShort" ||
            error.Code.StartsWith("PasswordRequires", StringComparison.Ordinal)));
#endif
    }

    [TestMethod]
    [DataRow("GOLFTEST!12345", "PasswordRequiresLower")]
    [DataRow("golftest!12345", "PasswordRequiresUpper")]
    [DataRow("GolfTest!abcd", "PasswordRequiresDigit")]
    [DataRow("GolfTest12345", "PasswordRequiresNonAlphanumeric")]
    public async Task EachComplexityRequirementAppliesOnlyToRelease(string password, string missingRequirement)
    {
        var result = await ValidatePassword(password);

#if DEBUG
        Assert.IsTrue(result.Succeeded, $"Debug registration should not require {missingRequirement}.");
#else
        Assert.IsFalse(result.Succeeded);
        Assert.Contains(missingRequirement, result.Errors.Select(error => error.Code));
#endif
    }

    [TestMethod]
    [DataRow("a", true)]
    [DataRow("1", true)]
    [DataRow("a", false)]
    public async Task ExplicitLocalTestingOptInAllowsSimplePasswordsWhileFalsePreservesBuildDefault(
        string password, bool localTestingOptIn)
    {
        var result = await ValidatePassword(password, localTestingOptIn);

#if DEBUG
        Assert.IsTrue(result.Succeeded, "Debug passwords remain simple regardless of the local setting.");
#else
        Assert.AreEqual(localTestingOptIn, result.Succeeded,
            "Release requires an explicit true setting to accept a simple test password.");
#endif
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public async Task LocalTestingOptInStillRejectsEmptyAndWhitespacePasswords(string password)
    {
        Assert.IsFalse((await ValidatePassword(password, true)).Succeeded);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public async Task EmptyOrWhitespacePasswordsRemainInvalid(string password)
    {
        Assert.IsFalse((await ValidatePassword(password)).Succeeded);
        var request = new AccountCredentialsRequest { Username = "local-player", Password = password };
        Assert.IsFalse(Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    [TestMethod]
    public async Task StrongPasswordWorksInEveryBuild()
    {
        Assert.IsTrue((await ValidatePassword("GolfTest!12345")).Succeeded);
    }

    [TestMethod]
    [DataRow(128, true)]
    [DataRow(129, false)]
    public void RequestPasswordMaximumRemains128Characters(int length, bool accepted)
    {
        var request = new AccountCredentialsRequest
            { Username = "local-player", Password = "Aa1!" + new string('a', length - 4) };

        Assert.AreEqual(accepted,
            Validator.TryValidateObject(request, new ValidationContext(request), [], true));
    }

    private static async Task<IdentityResult> ValidatePassword(string password, bool? localTestingOptIn = null)
    {
        // Resolving the real Identity validators does not open a database connection.
        var configuration = new Dictionary<string, string?>
            { ["ConnectionStrings:Default"] = "Host=localhost;Database=golfsg_test_unused;Username=unused" };
        if (localTestingOptIn is { } enabled)
            configuration["Auth:AllowSimplePasswordsForLocalTesting"] = enabled ? "true" : "false";
        var settings = new ConfigurationBuilder().AddInMemoryCollection(configuration).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServerInfrastructure(settings);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.IsNotEmpty(users.PasswordValidators);
        var user = new IdentityUser { UserName = "local-player" };
        var errors = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, user, password);
            errors.AddRange(result.Errors);
        }
        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
    }
}
