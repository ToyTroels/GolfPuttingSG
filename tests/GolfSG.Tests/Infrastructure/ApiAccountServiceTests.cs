using System.Net;
using System.Text;
using System.Text.Json;
using GolfSG.Application.Accounts;
using GolfSG.Application.Products;
using GolfSG.Infrastructure.Accounts;

namespace GolfSG.Tests.Infrastructure;

[TestClass]
public sealed class ApiAccountServiceTests
{
    private static readonly Uri Server = new("https://localhost/backend/");
    private const string Password = "MyStrongPassword123!";

    [TestMethod]
    public async Task SignInStoresVerifiedIdentityAndOpaqueTokensButNeverPassword()
    {
        using var setup = new Setup();
        var session = await setup.Service.SignInAsync(Server, "typed_name", Password);
        Assert.AreEqual("owner-a", session.AccountId);
        Assert.AreEqual("verified_name", session.Username);
        Assert.AreEqual(Server.AbsoluteUri, session.ServerAddress);
        Assert.IsNotNull(setup.Store.Json);
        Assert.Contains("access-a", setup.Store.Json);
        Assert.DoesNotContain(Password, setup.Store.Json);
        var binding = await File.ReadAllTextAsync(setup.Service.BindingPath);
        Assert.Contains("owner-a", binding);
        Assert.DoesNotContain("access-a", binding);
        Assert.DoesNotContain(Password, binding);
        Assert.DoesNotContain("access-a", session.ToString());
        Assert.IsNull(setup.Client.DefaultRequestHeaders.Authorization);
        Assert.AreEqual("Bearer access-a", setup.Authorization.Single());
        Assert.AreEqual(session, await setup.Service.LoadAccountAsync(Server));
    }

    [TestMethod]
    public async Task LogoutPreservesBindingAndRefusesDifferentOwnerBeforeSavingTokens()
    {
        using var setup = new Setup();
        var session = await setup.Service.SignInAsync(Server, "tester", Password);
        var binding = await File.ReadAllTextAsync(setup.Service.BindingPath);
        await setup.Service.SignOutAsync();
        Assert.IsNull(setup.Store.Json);
        Assert.AreEqual(binding, await File.ReadAllTextAsync(setup.Service.BindingPath));
        setup.Owner = "owner-b";
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.SignInAsync(Server, "other", Password));
        Assert.IsNull(setup.Store.Json);
        Assert.AreEqual(binding, await File.ReadAllTextAsync(setup.Service.BindingPath));
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.WithSessionAsync(session, () => Task.FromResult(true)));
        setup.Owner = "owner-a";
        Assert.AreEqual("owner-a", (await setup.Service.SignInAsync(Server, "tester", Password)).AccountId);
    }

    [TestMethod]
    public async Task ChangedServerCannotReceiveExistingTokensOrAnotherPassword()
    {
        using var setup = new Setup();
        await setup.Service.SignInAsync(Server, "tester", Password);
        var previousRequests = setup.Requests.Count;
        setup.Settings.BaseAddress = "https://other.example/";
        var other = new Uri(setup.Settings.BaseAddress);
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.GetSessionAsync(other));
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.SignInAsync(other, "tester", Password));
        Assert.HasCount(previousRequests, setup.Requests);
    }

    [TestMethod]
    public async Task LogoutDuringLoginDiscardsResponseAndCreatesNoBinding()
    {
        using var setup = new Setup();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.BeforeLogin = async () => { entered.SetResult(); await release.Task; };
        var login = setup.Service.SignInAsync(Server, "tester", Password);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await setup.Service.SignOutAsync();
        release.SetResult();
        await Assert.ThrowsAsync<ApiAccountException>(() => login);
        Assert.IsNull(setup.Store.Json);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
    }

    [TestMethod]
    public async Task ServerChangeDuringLoginNeverStoresCandidateTokensOrBindsHistory()
    {
        using var setup = new Setup();
        setup.BeforeLogin = () => { setup.Settings.BaseAddress = "https://other.example/"; return Task.CompletedTask; };
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.SignInAsync(Server, "tester", Password));
        Assert.IsNull(setup.Store.Json);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
        Assert.IsTrue(setup.Requests.All(r => r.StartsWith(Server.AbsoluteUri, StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConcurrentExpiredSessionsRefreshOnceAndPersistBeforeReturning()
    {
        using var setup = new Setup();
        var session = await setup.Service.SignInAsync(Server, "tester", Password);
        setup.Store.Set(session with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        var refreshed = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => setup.Service.GetSessionAsync(Server)));
        Assert.AreEqual(1, setup.Refreshes);
        Assert.IsTrue(refreshed.All(s => s.SessionId == session.SessionId && s.AccessToken == "refreshed-access"));
        Assert.Contains("refreshed-access", setup.Store.Json!);
        Assert.Contains("refreshed-refresh", setup.Store.Json!);
    }

    [TestMethod]
    public async Task TokenWithNinetySecondsRemainingRefreshesBeforePotentialApiWakeDelay()
    {
        using var setup = new Setup();
        var session = await setup.Service.SignInAsync(Server, "tester", Password);
        setup.Store.Set(session with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(90) });

        var refreshed = await setup.Service.GetSessionAsync(Server);

        Assert.AreEqual(1, setup.Refreshes);
        Assert.AreEqual(session.SessionId, refreshed.SessionId);
        Assert.AreEqual(session.AccountId, refreshed.AccountId);
        Assert.AreEqual("refreshed-access", refreshed.AccessToken);
        Assert.Contains("refreshed-access", setup.Store.Json!);
    }

    [TestMethod]
    public async Task HealthyTokenReturnsFromLocalStorageWithoutAnotherNetworkRequest()
    {
        using var setup = new Setup();
        var healthy = (await setup.Service.SignInAsync(Server, "tester", Password)) with
            { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) };
        setup.Store.Set(healthy);
        var previousRequests = setup.Requests.Count;

        Assert.AreEqual(healthy, await setup.Service.GetSessionAsync(Server));

        Assert.AreEqual(0, setup.Refreshes);
        Assert.HasCount(previousRequests, setup.Requests);
    }

    [TestMethod]
    public async Task LoadingNearExpirySavedAccountRemainsLocalAndDoesNotRefresh()
    {
        using var setup = new Setup();
        var saved = (await setup.Service.SignInAsync(Server, "tester", Password)) with
            { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(90) };
        setup.Store.Set(saved);
        var previousRequests = setup.Requests.Count;

        Assert.AreEqual(saved, await setup.Service.LoadAccountAsync(Server));

        Assert.AreEqual(0, setup.Refreshes);
        Assert.HasCount(previousRequests, setup.Requests);
    }

    [TestMethod]
    public async Task LogoutDuringRefreshDoesNotResurrectSession()
    {
        using var setup = new Setup();
        var session = await setup.Service.SignInAsync(Server, "tester", Password);
        setup.Store.Set(session with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.BeforeRefresh = async () => { entered.SetResult(); await release.Task; };
        var refresh = setup.Service.GetSessionAsync(Server);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await setup.Service.SignOutAsync();
        release.SetResult();
        await Assert.ThrowsAsync<ApiAccountException>(() => refresh);
        Assert.IsNull(setup.Store.Json);
        Assert.IsTrue(File.Exists(setup.Service.BindingPath));
    }

    [TestMethod]
    public async Task FailedSecureSaveRetainsBindingAndAllowsOnlyOriginalOwner()
    {
        using var setup = new Setup();
        setup.Store.FailSave = true;
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.SignInAsync(Server, "tester", Password));
        Assert.IsTrue(File.Exists(setup.Service.BindingPath));
        Assert.IsNull(setup.Store.Json);
        setup.Store.FailSave = false;
        setup.Owner = "owner-b";
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.SignInAsync(Server, "other", Password));
        setup.Owner = "owner-a";
        Assert.AreEqual("owner-a", (await setup.Service.SignInAsync(Server, "tester", Password)).AccountId);
    }

    [TestMethod]
    public async Task CorruptBindingFailsClosedAndDoesNotSendPassword()
    {
        using var setup = new Setup();
        await File.WriteAllTextAsync(setup.Service.BindingPath, "{}");
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.SignInAsync(Server, "tester", Password));
        Assert.HasCount(0, setup.Requests);
        Assert.AreEqual("{}", await File.ReadAllTextAsync(setup.Service.BindingPath));
    }

    [TestMethod]
    public async Task ExpiredRefreshReturnsReadableSignInErrorWithoutServerDetails()
    {
        using var setup = new Setup();
        var session = await setup.Service.SignInAsync(Server, "tester", Password);
        setup.Store.Set(session with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        setup.RefreshStatus = HttpStatusCode.Unauthorized;
        var error = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.GetSessionAsync(Server));
        Assert.Contains("Log ind", error.Message);
        Assert.DoesNotContain("server secret", error.Message);
    }

    [TestMethod]
    public async Task RegistrationSignsInAndExistingBindingPreventsExtraRegistration()
    {
        using var setup = new Setup();
        var session = await setup.Service.RegisterAsync(Server, "tester", Password);
        Assert.AreEqual("owner-a", session.AccountId);
        CollectionAssert.AreEqual(new[] { "api/auth/register", "api/auth/login", "api/auth/me" },
            setup.Requests.Select(address => address[Server.AbsoluteUri.Length..]).ToArray());
        await setup.Service.SignOutAsync();
        var count = setup.Requests.Count;
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.RegisterAsync(Server, "other", Password));
        Assert.HasCount(count, setup.Requests);
    }

    [TestMethod]
    public async Task SimpleRegistrationFollowsBuildConfiguration()
    {
        using var setup = new Setup { ExpectedPassword = "a" };
#if DEBUG
        var session = await setup.Service.RegisterAsync(Server, "tester", "a");
        Assert.AreEqual("owner-a", session.AccountId);
        Assert.HasCount(3, setup.Requests);
#else
        var error = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.RegisterAsync(Server, "tester", "a"));
        Assert.Contains("12–128", error.Message);
        Assert.HasCount(0, setup.Requests);
        Assert.IsNull(setup.Store.Json);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
#endif
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(129)]
    public async Task RegistrationRejectsEmptyAndOverlongPasswordsBeforeSendingRequests(int length)
    {
        using var setup = new Setup();
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.RegisterAsync(Server, "tester", new string('a', length)));
        Assert.HasCount(0, setup.Requests);
        Assert.IsNull(setup.Store.Json);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
    }

    [TestMethod]
    [DataRow(" ")]
    [DataRow("\t")]
    public async Task RegistrationRejectsWhitespacePasswordsBeforeSendingRequests(string password)
    {
        using var setup = new Setup();
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.RegisterAsync(Server, "tester", password));
        Assert.HasCount(0, setup.Requests);
        Assert.IsNull(setup.Store.Json);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
    }

    [TestMethod]
    public async Task SignInAcceptsExistingSimplePasswordInEveryBuildConfiguration()
    {
        using var setup = new Setup { ExpectedPassword = "a" };
        var session = await setup.Service.SignInAsync(Server, "tester", "a");
        Assert.AreEqual("owner-a", session.AccountId);
        Assert.HasCount(2, setup.Requests);
    }

    [TestMethod]
    public async Task ConnectionProbeIsAnonymousBeforeLoginAndIgnoresBindingSettingsAndSecureStore()
    {
        using var setup = new Setup();
        setup.Settings.BaseAddress = "https://different.example/";
        await File.WriteAllTextAsync(setup.Service.BindingPath, "corrupt binding preserved");
        setup.Store.FailAccess = true;
        var status = await setup.Service.CheckConnectionAsync(Server);
        Assert.IsTrue(status.DatabaseAvailable);
        Assert.HasCount(1, setup.Requests);
        Assert.AreEqual(Server.AbsoluteUri + "api/health", setup.Requests.Single());
        Assert.HasCount(0, setup.Authorization);
        Assert.IsNull(setup.Store.Json);
        Assert.AreEqual("corrupt binding preserved", await File.ReadAllTextAsync(setup.Service.BindingPath));
    }

    [TestMethod]
    public async Task FreshConnectionProbeCreatesNoAccountBindingOrSecureSession()
    {
        using var setup = new Setup();
        Assert.IsTrue((await setup.Service.CheckConnectionAsync(Server)).DatabaseAvailable);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
        Assert.IsNull(setup.Store.Json);
    }

    [TestMethod]
    public async Task ConnectionProbeKeepsExistingSessionAndSendsNoBearerToken()
    {
        using var setup = new Setup();
        await setup.Service.SignInAsync(Server, "tester", Password);
        var session = setup.Store.Json;
        setup.Authorization.Clear();
        Assert.IsTrue((await setup.Service.CheckConnectionAsync(Server)).DatabaseAvailable);
        Assert.HasCount(0, setup.Authorization);
        Assert.AreEqual(session, setup.Store.Json);
    }

    [TestMethod]
    public async Task ConnectionProbeReportsAvailableApiWithUnavailableDatabase()
    {
        using var setup = new Setup
        {
            HealthStatus = HttpStatusCode.ServiceUnavailable,
            HealthPayload = "{\"service\":\"GolfSG.Api\",\"databaseAvailable\":false}"
        };
        Assert.IsFalse((await setup.Service.CheckConnectionAsync(Server)).DatabaseAvailable);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
    }

    [TestMethod]
    [DataRow("<html>server secret</html>")]
    [DataRow("{\"service\":\"OlderApi\",\"databaseAvailable\":true}")]
    [DataRow("{\"service\":\"GolfSG.Api\",\"databaseAvailable\":false}")]
    [DataRow("{\"service\":\"GolfSG.Api\"}")]
    [DataRow("{}")]
    public async Task ConnectionProbeRejectsWrongServerVersionAndMalformedStatusWithoutExposingBody(string payload)
    {
        using var setup = new Setup { HealthPayload = payload };
        var error = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.CheckConnectionAsync(Server));
        Assert.Contains("serveradressen", error.Message);
        Assert.Contains("GolfSG.Api", error.Message);
        Assert.DoesNotContain("server secret", error.Message);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
    }

    [TestMethod]
    public async Task ConnectionProbeRejectsHtmlEvenWhenItsBodyLooksLikeValidStatusJson()
    {
        using var setup = new Setup { HealthContentType = "text/html" };
        await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.CheckConnectionAsync(Server));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, "404")]
    [DataRow(HttpStatusCode.MethodNotAllowed, "405")]
    [DataRow(HttpStatusCode.InternalServerError, "500")]
    public async Task ConnectionProbeReturnsSafeActionableHttpErrors(HttpStatusCode status, string numericStatus)
    {
        using var setup = new Setup { HealthStatus = status, HealthPayload = "server secret" };
        var error = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.CheckConnectionAsync(Server));
        Assert.Contains(numericStatus, error.Message);
        Assert.DoesNotContain("server secret", error.Message);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, "404", "genudgiv")]
    [DataRow(HttpStatusCode.MethodNotAllowed, "405", "IIS")]
    [DataRow(HttpStatusCode.TooManyRequests, "429", "Vent")]
    [DataRow(HttpStatusCode.InternalServerError, "500", "IIS")]
    [DataRow(HttpStatusCode.BadGateway, "502", "IIS")]
    public async Task SignInHttpErrorsIdentifySafeNextActionWithoutExposingBody(HttpStatusCode status, string numericStatus, string action)
    {
        using var setup = new Setup { LoginStatus = status };
        var error = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.SignInAsync(Server, "tester", Password));
        Assert.Contains(numericStatus, error.Message);
        Assert.Contains(action, error.Message);
        Assert.DoesNotContain("server secret", error.Message);
        Assert.IsNull(setup.Store.Json);
        Assert.IsFalse(File.Exists(setup.Service.BindingPath));
    }

    [TestMethod]
    [DataRow("DuplicateUserName", "allerede i brug")]
    [DataRow("PasswordTooShort", "for kort")]
    [DataRow("PasswordRequiresNonAlphanumeric", "specialtegn")]
    [DataRow("PasswordRequiresDigit", "tal")]
    [DataRow("PasswordRequiresLower", "lille bogstav")]
    [DataRow("PasswordRequiresUpper", "stort bogstav")]
    public async Task RegistrationTranslatesKnownIdentityCodesAndIgnoresRawDescriptions(string code, string translation)
    {
        using var setup = new Setup
        {
            RegistrationStatus = HttpStatusCode.BadRequest,
            ErrorPayload = JsonSerializer.Serialize(new { title = "server secret", errors = new Dictionary<string, string[]> { [code] = ["server secret"] } })
        };
        var error = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.RegisterAsync(Server, "tester", Password));
        Assert.Contains(translation, error.Message);
        Assert.DoesNotContain("server secret", error.Message);
        Assert.IsNull(setup.Store.Json);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.BadRequest)]
    [DataRow(HttpStatusCode.Conflict)]
    public async Task UnknownRegistrationValidationOrHtmlUsesSafeFallback(HttpStatusCode status)
    {
        using var setup = new Setup { RegistrationStatus = status, ErrorPayload = "<html>server secret</html>" };
        var error = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.RegisterAsync(Server, "tester", Password));
        Assert.Contains("serverens krav", error.Message);
        Assert.DoesNotContain("server secret", error.Message);
    }

    [TestMethod]
    public async Task RegistrationHttpErrorAndRefreshServerFailureExplainIisAction()
    {
        using var setup = new Setup { RegistrationStatus = HttpStatusCode.NotFound };
        var registration = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.RegisterAsync(Server, "tester", Password));
        Assert.Contains("404", registration.Message);
        Assert.Contains("genudgiv", registration.Message);
        var session = await setup.Service.SignInAsync(Server, "tester", Password);
        setup.Store.Set(session with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        setup.RefreshStatus = HttpStatusCode.InternalServerError;
        var refresh = await Assert.ThrowsAsync<ApiAccountException>(() => setup.Service.GetSessionAsync(Server));
        Assert.Contains("500", refresh.Message);
        Assert.Contains("IIS", refresh.Message);
        Assert.DoesNotContain("server secret", refresh.Message);
    }

    private sealed class Setup : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "golfsg-account-" + Guid.NewGuid());
        public SecureStore Store { get; } = new();
        public Settings Settings { get; } = new();
        public HttpClient Client { get; }
        public ApiAccountService Service { get; }
        public string Owner { get; set; } = "owner-a";
        public string ExpectedPassword { get; set; } = Password;
        public List<string> Requests { get; } = [];
        public List<string> Authorization { get; } = [];
        public int Refreshes { get; private set; }
        public Func<Task>? BeforeLogin { get; set; }
        public Func<Task>? BeforeRefresh { get; set; }
        public HttpStatusCode RefreshStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode RegistrationStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode LoginStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode HealthStatus { get; set; } = HttpStatusCode.OK;
        public string HealthPayload { get; set; } = "{\"service\":\"GolfSG.Api\",\"databaseAvailable\":true}";
        public string HealthContentType { get; set; } = "application/json";
        public string ErrorPayload { get; set; } = "server secret";
        public Setup()
        {
            Directory.CreateDirectory(directory);
            Client = new(new Handler(async (request, token) =>
            {
                Requests.Add(request.RequestUri!.AbsoluteUri);
                if (request.Headers.Authorization is { } auth) Authorization.Add(auth.ToString());
                if (request.RequestUri.AbsolutePath.EndsWith("health"))
                {
                    Assert.AreEqual(HttpMethod.Get, request.Method);
                    Assert.IsNull(request.Headers.Authorization);
                    Assert.IsNull(request.Content);
                    return new HttpResponseMessage(HealthStatus)
                        { Content = new StringContent(HealthPayload, Encoding.UTF8, HealthContentType) };
                }
                if (request.RequestUri.AbsolutePath.EndsWith("register") || request.RequestUri.AbsolutePath.EndsWith("login"))
                {
                    using var credentials = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                    Assert.AreEqual(ExpectedPassword, credentials.RootElement.GetProperty("password").GetString());
                }
                if (request.RequestUri.AbsolutePath.EndsWith("register"))
                    return RegistrationStatus == HttpStatusCode.OK ? Json("{}") : Json(ErrorPayload, RegistrationStatus);
                if (request.RequestUri.AbsolutePath.EndsWith("login"))
                {
                    if (BeforeLogin is not null) await BeforeLogin();
                    if (LoginStatus != HttpStatusCode.OK) return Json(ErrorPayload, LoginStatus);
                    return Tokens("access-a", "refresh-a");
                }
                if (request.RequestUri.AbsolutePath.EndsWith("refresh"))
                {
                    Refreshes++;
                    Assert.Contains("refresh-a", await request.Content!.ReadAsStringAsync(token));
                    if (BeforeRefresh is not null) await BeforeRefresh();
                    return RefreshStatus == HttpStatusCode.OK ? Tokens("refreshed-access", "refreshed-refresh") : Json("server secret", RefreshStatus);
                }
                return Json(JsonSerializer.Serialize(new { id = Owner, username = "verified_name" }));
            }));
            Service = new(Client, Store, Settings, directory);
        }
        public void Dispose() { Client.Dispose(); Directory.Delete(directory, recursive: true); }
    }

    private sealed class SecureStore : ISecureApiSessionStore
    {
        public string? Json { get; private set; }
        public bool FailSave { get; set; }
        public bool FailAccess { get; set; }
        public Task<string?> ReadAsync() { if (FailAccess) throw new AssertFailedException("Unexpected secure-store read"); return Task.FromResult(Json); }
        public Task WriteAsync(string sessionJson) { if (FailAccess) throw new AssertFailedException("Unexpected secure-store write"); if (FailSave) throw new IOException(); Json = sessionJson; return Task.CompletedTask; }
        public Task DeleteAsync() { if (FailAccess) throw new AssertFailedException("Unexpected secure-store delete"); Json = null; return Task.CompletedTask; }
        public void Set(ApiSession session) => Json = JsonSerializer.Serialize(session,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
    private sealed class Settings : IProductsApiSettings { public string BaseAddress { get; set; } = Server.AbsoluteUri; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => action(request, cancellationToken); }
    private static HttpResponseMessage Tokens(string access, string refresh) => Json(JsonSerializer.Serialize(new
        { tokenType = "Bearer", accessToken = access, expiresIn = 3600, refreshToken = refresh }));
    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
