using GolfSG.Application.Accounts;
using GolfSG.Application.Products;
using GolfSG.Application.ViewModels;
using GolfSG.Tests.Infrastructure.TestDoubles;

namespace GolfSG.Tests.Application;

[TestClass]
public sealed class AccountViewModelTests
{
    [TestMethod]
    public async Task SuccessfulLoginClearsPasswordAndLogoutRetainsServerSetting()
    {
        var settings = new Settings();
        var accounts = new TestApiAccountService(settings) { SignedOut = true };
        var viewModel = new AccountViewModel(accounts, settings) { Username = "tester", Password = "secret" };
        await viewModel.SignInAsync();
        Assert.IsTrue(viewModel.IsSignedIn);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.Contains("tester", viewModel.CurrentIdentity);
        await viewModel.SignOutAsync();
        Assert.IsFalse(viewModel.IsSignedIn);
        Assert.AreEqual("https://localhost/", settings.BaseAddress);
        Assert.Contains("stadig gemt", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task FailedLoginAndCanceledAttemptClearPassword()
    {
        var viewModel = new AccountViewModel(new TestApiAccountService(), new Settings())
            { Username = "tester", Password = "secret", ConfirmPassword = "confirmation secret", ServerAddress = "http://localhost/" };
        await viewModel.SignInAsync();
        Assert.IsTrue(viewModel.HasError);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
        viewModel.Password = "another secret";
        viewModel.ConfirmPassword = "another confirmation";
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await viewModel.SignInAsync(canceled.Token);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
        Assert.IsFalse(viewModel.IsBusy);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LoadingSavedAccountDoesNotCheckServerOrDatabase(bool signedOut)
    {
        var accounts = new TestApiAccountService { SignedOut = signedOut };
        var viewModel = new AccountViewModel(accounts, new Settings());
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);

        await viewModel.LoadAsync();

        Assert.AreEqual(!signedOut, viewModel.IsSignedIn);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
        Assert.HasCount(0, accounts.CheckedServers);
        Assert.AreEqual(string.Empty, viewModel.RequestWaitMessage);
        Assert.IsFalse(viewModel.CanCancelRequest);
        Assert.IsTrue(viewModel.CanAct);
    }

    [TestMethod]
    public async Task AccountRestoreFailureDoesNotTriggerAutomaticConnectionCheck()
    {
        var accounts = new TestApiAccountService
            { LoadAccountFailure = new ApiAccountException("Log ind med den oprindelige konto.") };
        var viewModel = new AccountViewModel(accounts, new Settings());

        await viewModel.LoadAsync();

        Assert.IsFalse(viewModel.IsSignedIn);
        Assert.AreEqual("Log ind med den oprindelige konto.", viewModel.ErrorMessage);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
        Assert.HasCount(0, accounts.CheckedServers);
    }

    [TestMethod]
    public async Task DatabaseUnavailableStatusPersistsThroughSignIn()
    {
        var accounts = new TestApiAccountService { SignedOut = true, ConnectionStatus = new(false) };
        var viewModel = new AccountViewModel(accounts, new Settings()) { Username = "tester", Password = "secret" };

        await viewModel.CheckConnectionAsync();
        Assert.AreEqual("Der er forbindelse til serveren, men databasen er ikke klar.", viewModel.ConnectionStatusMessage);
        await viewModel.SignInAsync();

        Assert.IsTrue(viewModel.IsSignedIn);
        Assert.AreEqual("Der er forbindelse til serveren, men databasen er ikke klar.", viewModel.ConnectionStatusMessage);
        Assert.AreEqual(string.Empty, viewModel.Password);
    }

    [TestMethod]
    public async Task SavingServerNormalizesAddressWithoutCheckingServerOrDatabase()
    {
        var settings = new Settings();
        var accounts = new TestApiAccountService { SignedOut = true };
        var viewModel = new AccountViewModel(accounts, settings) { ServerAddress = "https://other.example" };

        await viewModel.SaveServerAddressAsync();

        Assert.AreEqual("https://other.example/", settings.BaseAddress);
        Assert.HasCount(0, accounts.CheckedServers);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
        Assert.Contains("gemt", viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task FailedAccountRestoreWhileSavingDoesNotCheckEnteredServer()
    {
        var settings = new Settings();
        var accounts = new TestApiAccountService
            { LoadAccountFailure = new ApiAccountException("Denne installation er knyttet til en anden server.") };
        var viewModel = new AccountViewModel(accounts, settings) { ServerAddress = "https://other.example/" };

        await viewModel.SaveServerAddressAsync();

        Assert.AreEqual("https://localhost/", settings.BaseAddress, "A failed account restore must retain the saved address.");
        Assert.HasCount(0, accounts.CheckedServers);
        Assert.IsTrue(viewModel.HasError);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
    }

    [TestMethod]
    public async Task LoadingAndSavingSameAddressClearEarlierHealthyStatusWithoutMoreHealthCalls()
    {
        var accounts = new TestApiAccountService();
        var viewModel = new AccountViewModel(accounts, new Settings());
        await viewModel.CheckConnectionAsync();
        Assert.AreEqual("Der er forbindelse til server og database.", viewModel.ConnectionStatusMessage);
        await viewModel.LoadAsync();
        Assert.HasCount(1, accounts.CheckedServers);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);

        await viewModel.CheckConnectionAsync();
        Assert.AreEqual("Der er forbindelse til server og database.", viewModel.ConnectionStatusMessage);
        await viewModel.SaveServerAddressAsync();
        Assert.HasCount(2, accounts.CheckedServers);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
    }

    [TestMethod]
    public async Task LocalLoadSaveAndLogoutNeverPublishServerWakeMessage()
    {
        var viewModel = new AccountViewModel(new TestApiAccountService(), new Settings());
        var waitMessages = new List<string>();
        var cancellableStates = new List<bool>();
        viewModel.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(AccountViewModel.RequestWaitMessage)) waitMessages.Add(viewModel.RequestWaitMessage);
            if (change.PropertyName == nameof(AccountViewModel.CanCancelRequest)) cancellableStates.Add(viewModel.CanCancelRequest);
        };

        await viewModel.LoadAsync();
        await viewModel.SaveServerAddressAsync();
        await viewModel.SignOutAsync();

        Assert.IsNotEmpty(waitMessages);
        Assert.IsTrue(waitMessages.All(string.IsNullOrEmpty));
        Assert.IsTrue(cancellableStates.All(value => !value));
        Assert.IsFalse(viewModel.HasRequestWaitMessage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PendingAuthenticationShowsCancellableWakeHelpAndClearsItAfterCancellation(bool register)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var accounts = new RecordingAccounts
        {
            AuthenticationRequest = async token =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            }
        };
        var viewModel = new AccountViewModel(accounts, new Settings())
            { Username = "tester", Password = "secret", ConfirmPassword = "secret" };
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);
        using var cancellation = new CancellationTokenSource();
        var pending = register ? viewModel.RegisterAsync(cancellation.Token) : viewModel.SignInAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(viewModel.CanCancelRequest);
        Assert.IsTrue(viewModel.HasRequestWaitMessage);
        Assert.Contains("kan være i dvale", viewModel.RequestWaitMessage);
        Assert.Contains("et minut", viewModel.RequestWaitMessage);
        cancellation.Cancel();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsFalse(viewModel.CanCancelRequest);
        Assert.IsFalse(viewModel.HasRequestWaitMessage);
        Assert.AreEqual(string.Empty, viewModel.RequestWaitMessage);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
        Assert.IsFalse(viewModel.HasError);
        foreach (var property in new[] { nameof(AccountViewModel.RequestWaitMessage), nameof(AccountViewModel.HasRequestWaitMessage), nameof(AccountViewModel.CanCancelRequest) })
            Assert.Contains(property, notifications);
    }

    [TestMethod]
    public async Task ConnectionFailureRemainsVisibleWithoutRequiringSignIn()
    {
        const string message = "Serveren kunne ikke nås. Kontrollér adressen og forbindelsen.";
        var accounts = new TestApiAccountService
        {
            SignedOut = true,
            ConnectionCheck = (_, _) => Task.FromException<ApiConnectionStatus>(new ApiAccountException(message))
        };
        var viewModel = new AccountViewModel(accounts, new Settings());

        await viewModel.CheckConnectionAsync();

        Assert.AreEqual(message, viewModel.ConnectionStatusMessage);
        Assert.IsFalse(viewModel.IsSignedIn);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsFalse(viewModel.HasError, "Connection details have their own persistent status in the server card.");
    }

    [TestMethod]
    public async Task CanceledConnectionCheckCannotApplyLateHealthyResult()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<ApiConnectionStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accounts = new TestApiAccountService
            { ConnectionCheck = (_, _) => { started.TrySetResult(true); return completed.Task; } };
        var viewModel = new AccountViewModel(accounts, new Settings());
        using var cancellation = new CancellationTokenSource();
        var check = viewModel.CheckConnectionAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        completed.SetResult(new(true));
        await check.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsFalse(viewModel.HasError);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ChangingServerDuringCheckIgnoresOldResponseEvenWhenChangedBack(bool changeBack)
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<ApiConnectionStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accounts = new TestApiAccountService
            { ConnectionCheck = (_, _) => { started.TrySetResult(true); return completed.Task; } };
        var viewModel = new AccountViewModel(accounts, new Settings());
        var check = viewModel.CheckConnectionAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        viewModel.ServerAddress = "https://other.example/";
        if (changeBack) viewModel.ServerAddress = "https://localhost/";
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
        completed.SetResult(new(true));
        await check.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("https://localhost/", accounts.CheckedServers.Single().AbsoluteUri);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);
        Assert.IsFalse(viewModel.IsBusy);
    }

    [TestMethod]
    public async Task ChangingServerClearsPreviouslyHealthyStatusAndInvalidAddressIsActionable()
    {
        var accounts = new TestApiAccountService { SignedOut = true };
        var viewModel = new AccountViewModel(accounts, new Settings());
        await viewModel.CheckConnectionAsync();
        viewModel.ServerAddress = "http://other.example/";
        Assert.AreEqual("Serverforbindelsen er ikke testet.", viewModel.ConnectionStatusMessage);

        await viewModel.CheckConnectionAsync();

        Assert.Contains("HTTPS", viewModel.ConnectionStatusMessage);
        Assert.HasCount(1, accounts.CheckedServers, "An invalid address must not send another request.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MismatchedSignupNeverCallsServiceOrChangesSavedSettings(bool directRegistration)
    {
        var settings = new Settings();
        var accounts = new RecordingAccounts();
        var viewModel = new AccountViewModel(accounts, settings);
        viewModel.SwitchAuthenticationMode();
        viewModel.Username = "tester";
        viewModel.Password = "first password";
        viewModel.ConfirmPassword = "different password";
        viewModel.ServerAddress = "https://other.example/";

        if (directRegistration) await viewModel.RegisterAsync();
        else await viewModel.SubmitCredentialsAsync();

        Assert.Contains("ikke ens", viewModel.ErrorMessage);
        Assert.AreEqual(0, accounts.Calls);
        Assert.AreEqual(0, settings.Changes);
        Assert.AreEqual("https://localhost/", settings.BaseAddress);
        Assert.IsTrue(viewModel.IsSignedOut);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
        Assert.IsTrue(viewModel.CanAct);
    }

    [TestMethod]
    public async Task MatchingSignupSubmitsRegistrationSignsInAndClearsBothPasswords()
    {
        var accounts = new RecordingAccounts();
        var viewModel = new AccountViewModel(accounts, new Settings());
        viewModel.SwitchAuthenticationMode();
        viewModel.Username = "tester";
        viewModel.Password = "matching password";
        viewModel.ConfirmPassword = "matching password";
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);

        await viewModel.SubmitCredentialsAsync();

        Assert.AreEqual(1, accounts.RegisterCalls);
        Assert.AreEqual(0, accounts.SignInCalls);
        Assert.IsTrue(viewModel.IsSignedIn);
        Assert.IsFalse(viewModel.IsSignedOut);
        Assert.Contains(nameof(AccountViewModel.IsSignedOut), notifications);
        Assert.Contains("oprettet", viewModel.StatusMessage);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
    }

    [TestMethod]
    public async Task DefaultSubmitUsesLoginAndLogoutClearsBothSecrets()
    {
        var accounts = new RecordingAccounts();
        var viewModel = new AccountViewModel(accounts, new Settings()) { Username = "tester", Password = "secret" };
        Assert.IsFalse(viewModel.IsCreatingAccount);
        Assert.AreEqual("Log ind", viewModel.AuthenticationTitle);
        Assert.AreEqual("Log ind", viewModel.AuthenticationActionText);
        Assert.AreEqual("Opret en ny konto", viewModel.AuthenticationSwitchText);

        await viewModel.SubmitCredentialsAsync();
        Assert.AreEqual(1, accounts.SignInCalls);
        Assert.AreEqual(0, accounts.RegisterCalls);
        viewModel.Password = "remaining secret";
        viewModel.ConfirmPassword = "remaining confirmation";
        await viewModel.SignOutAsync();

        Assert.IsTrue(viewModel.IsSignedOut);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SwitchingModeClearsSecretsAndPriorErrorOrStatus(bool previousSuccess)
    {
        var viewModel = new AccountViewModel(new RecordingAccounts(), new Settings()) { Username = "tester", Password = "secret" };
        if (previousSuccess)
        {
            await viewModel.SignInAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.StatusMessage));
        }
        else
        {
            await viewModel.RegisterAsync();
            Assert.IsTrue(viewModel.HasError);
        }
        viewModel.Password = "new secret";
        viewModel.ConfirmPassword = "new confirmation";
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);

        viewModel.SwitchAuthenticationMode();

        Assert.IsTrue(viewModel.IsCreatingAccount);
        Assert.AreEqual(previousSuccess ? "Din konto" : "Opret konto", viewModel.AuthenticationTitle);
        Assert.AreEqual("Opret konto", viewModel.AuthenticationActionText);
        Assert.AreEqual("Jeg har allerede en konto", viewModel.AuthenticationSwitchText);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
        Assert.AreEqual(string.Empty, viewModel.ErrorMessage);
        Assert.AreEqual(string.Empty, viewModel.StatusMessage);
        foreach (var property in new[] { nameof(AccountViewModel.IsCreatingAccount), nameof(AccountViewModel.AuthenticationTitle),
            nameof(AccountViewModel.AuthenticationActionText), nameof(AccountViewModel.AuthenticationSwitchText) })
            Assert.Contains(property, notifications);
    }

    [TestMethod]
    public async Task BusyConnectionCheckPreventsModeSwitchAndKeepsCurrentCredentials()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<ApiConnectionStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accounts = new TestApiAccountService
            { ConnectionCheck = (_, _) => { started.TrySetResult(true); return completed.Task; } };
        var viewModel = new AccountViewModel(accounts, new Settings()) { Password = "secret", ConfirmPassword = "confirmation" };
        var checking = viewModel.CheckConnectionAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        viewModel.SwitchAuthenticationMode();

        Assert.IsTrue(viewModel.IsBusy);
        Assert.IsTrue(viewModel.CanCancelRequest);
        Assert.IsTrue(viewModel.HasRequestWaitMessage);
        Assert.Contains("et minut", viewModel.RequestWaitMessage);
        Assert.IsFalse(viewModel.IsCreatingAccount);
        Assert.AreEqual("secret", viewModel.Password);
        Assert.AreEqual("confirmation", viewModel.ConfirmPassword);
        completed.SetResult(new(true));
        await checking.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(viewModel.CanAct);
        Assert.IsFalse(viewModel.CanCancelRequest);
        Assert.IsFalse(viewModel.HasRequestWaitMessage);
    }

    [TestMethod]
    public void PageExitCanExplicitlyClearBothCredentials()
    {
        var viewModel = new AccountViewModel(new RecordingAccounts(), new Settings())
            { Username = "tester", Password = "secret", ConfirmPassword = "confirmation" };

        viewModel.ClearCredentials();

        Assert.AreEqual("tester", viewModel.Username);
        Assert.AreEqual(string.Empty, viewModel.Password);
        Assert.AreEqual(string.Empty, viewModel.ConfirmPassword);
    }

    private sealed class RecordingAccounts : IApiAccountService
    {
        public Task SwitchServerAsync(Uri server, CancellationToken cancellationToken = default) => inner.SwitchServerAsync(server, cancellationToken);
        private readonly TestApiAccountService inner = new() { SignedOut = true };
        public int Calls { get; private set; }
        public int RegisterCalls { get; private set; }
        public int SignInCalls { get; private set; }
        public Func<CancellationToken, Task>? AuthenticationRequest { get; init; }
        public Task<ApiConnectionStatus> CheckConnectionAsync(Uri server, CancellationToken cancellationToken = default)
        { Calls++; return inner.CheckConnectionAsync(server, cancellationToken); }
        public Task<ApiSession?> LoadAccountAsync(Uri server, CancellationToken cancellationToken = default)
        { Calls++; return inner.LoadAccountAsync(server, cancellationToken); }
        public async Task<ApiSession> RegisterAsync(Uri server, string username, string password, CancellationToken cancellationToken = default)
        {
            Calls++; RegisterCalls++;
            if (AuthenticationRequest is not null) await AuthenticationRequest(cancellationToken);
            return await inner.RegisterAsync(server, username, password, cancellationToken);
        }
        public async Task<ApiSession> SignInAsync(Uri server, string username, string password, CancellationToken cancellationToken = default)
        {
            Calls++; SignInCalls++;
            if (AuthenticationRequest is not null) await AuthenticationRequest(cancellationToken);
            return await inner.SignInAsync(server, username, password, cancellationToken);
        }
        public Task SignOutAsync(CancellationToken cancellationToken = default)
        { Calls++; return inner.SignOutAsync(cancellationToken); }
        public Task<ApiSession> GetSessionAsync(Uri server, CancellationToken cancellationToken = default)
        { Calls++; return inner.GetSessionAsync(server, cancellationToken); }
        public Task<T> WithSessionAsync<T>(ApiSession session, Func<Task<T>> action, CancellationToken cancellationToken = default)
        { Calls++; return inner.WithSessionAsync(session, action, cancellationToken); }
    }

    private sealed class Settings : IProductsApiSettings
    {
        private string address = "https://localhost/";
        public int Changes { get; private set; }
        public string BaseAddress { get => address; set { address = value; Changes++; } }
    }
}
