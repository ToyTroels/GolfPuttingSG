using System.Reflection;
using GolfSG.Application.Accounts;
using GolfSG.Application.Products;
using GolfSG.Application.ViewModels;
using GolfSG.Views;
using Microsoft.Maui.Dispatching;

namespace GolfSG.Tests;

[TestClass]
public sealed class AccountPageTests
{
    private IDispatcherProvider? previousDispatcher;

    [TestInitialize]
    public void InstallHeadlessDispatcher()
    {
        previousDispatcher = DispatcherProvider.Current;
        DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    }

    [TestCleanup]
    public void RestoreDispatcher() => DispatcherProvider.SetCurrent(previousDispatcher!);

    [TestMethod]
    public async Task LeavingPageDuringSignInCancelsRequestAndClearsPassword()
    {
        var accounts = new PendingAccounts();
        var model = new AccountViewModel(accounts, new Settings());
        var page = new LoginPage(model);
        InvokeLifecycle(page, "OnAppearing");
        model.Username = "golfer";
        model.Password = "TestPassword12!";
        model.ConfirmPassword = "TestPassword12!";

        var password = Control<Entry>(page, UiAutomationIds.AccountPassword);
        Assert.IsTrue(password.IsPassword);
        Assert.AreEqual(model.Password, password.Text);

        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSubmit)).SendClicked();
        await accounts.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(model.IsBusy);
        InvokeLifecycle(page, "OnDisappearing");

        await accounts.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (model.IsBusy && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse(model.IsBusy, "A cancelled page action must release the view model's operation gate.");
        Assert.IsTrue(accounts.LoginToken.IsCancellationRequested);
        Assert.AreEqual(string.Empty, model.Password);
        Assert.AreEqual(string.Empty, password.Text);
        Assert.AreEqual(string.Empty, Control<Entry>(page, UiAutomationIds.AccountConfirmPassword).Text);
        Assert.IsFalse(model.IsSignedIn);
        Assert.IsFalse(model.HasError, "Leaving the page should not surface a cancelled request as an error.");
    }

    [TestMethod]
    public void LoginPageSwitchesFormsAndCreatesAnAccountWithMatchingPasswords()
    {
        var accounts = new PendingAccounts();
        var model = new AccountViewModel(accounts, new Settings());
        var page = new LoginPage(model, openHistory: () => new ContentPage());
        InvokeLifecycle(page, "OnAppearing");
        var submit = Control<Button>(page, UiAutomationIds.LoginSubmit);
        var confirmation = Control<Entry>(page, UiAutomationIds.AccountConfirmPassword);
        var history = Control<Button>(page, UiAutomationIds.LoginOpenHistory);
        Assert.AreEqual("Log ind", submit.Text);
        Assert.IsFalse(IsShown(confirmation));
        Assert.IsFalse(IsShown(history));
        Assert.IsTrue(confirmation.IsPassword);

        model.Password = "previous secret";
        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSwitchMode)).SendClicked();
        Assert.AreEqual("Opret konto", submit.Text);
        Assert.IsTrue(IsShown(confirmation));
        Assert.AreEqual(string.Empty, model.Password);
        Control<Entry>(page, UiAutomationIds.AccountUsername).Text = "golfer";
        Control<Entry>(page, UiAutomationIds.AccountPassword).Text = "test";
        confirmation.Text = "test";
        ((IButtonController)submit).SendClicked();

        Assert.AreEqual(1, accounts.Registrations);
        Assert.AreEqual(0, accounts.SignIns);
        Assert.AreEqual("golfer", accounts.RegisteredUsername);
        Assert.AreEqual("test", accounts.RegisteredPassword);
        Assert.IsTrue(model.IsSignedIn);
        Assert.IsFalse(IsShown(submit));
        Assert.IsTrue(IsShown(history));
        Assert.AreEqual(string.Empty, model.Password);
        Assert.AreEqual(string.Empty, confirmation.Text);
        Assert.Contains("golfer", Control<Label>(page, UiAutomationIds.AccountIdentity).Text);
    }

    [TestMethod]
    public void SignupPageReportsConfirmationMismatchWithoutCallingServer()
    {
        var accounts = new PendingAccounts();
        var model = new AccountViewModel(accounts, new Settings());
        var page = new LoginPage(model);
        InvokeLifecycle(page, "OnAppearing");
        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSwitchMode)).SendClicked();
        model.Username = "golfer";
        model.Password = "test";
        model.ConfirmPassword = "different";
        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSubmit)).SendClicked();

        Assert.AreEqual(0, accounts.Registrations);
        Assert.AreEqual(0, accounts.SignIns);
        var error = Control<Label>(page, UiAutomationIds.AccountError);
        Assert.IsTrue(IsShown(error));
        Assert.Contains("ikke ens", error.Text);
        Assert.AreEqual(string.Empty, Control<Entry>(page, UiAutomationIds.AccountPassword).Text);
        Assert.AreEqual(string.Empty, Control<Entry>(page, UiAutomationIds.AccountConfirmPassword).Text);

        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSwitchMode)).SendClicked();
        Assert.AreEqual("Log ind", Control<Button>(page, UiAutomationIds.LoginSubmit).Text);
        Assert.IsFalse(IsShown(error));
        Assert.IsFalse(IsShown(Control<Entry>(page, UiAutomationIds.AccountConfirmPassword)));
    }

    [TestMethod]
    public void SignedOutPageOnlyChecksTheServerWhenRequested()
    {
        var accounts = new PendingAccounts { ConnectionStatus = new(false) };
        var model = new AccountViewModel(accounts, new Settings());
        var page = new AccountPage(model);
        var status = Control<Label>(page, UiAutomationIds.AccountConnectionStatus);
        var check = Control<Button>(page, UiAutomationIds.AccountCheckConnection);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", status.Text);
        Assert.IsTrue(check.IsEnabled, "Checking the server must be available without a login.");

        InvokeLifecycle(page, "OnAppearing");

        Assert.AreEqual(0, accounts.ConnectionChecks);
        Assert.AreEqual("Serverforbindelsen er ikke testet.", status.Text);
        Assert.IsFalse(model.IsSignedIn);
        ((IButtonController)check).SendClicked();
        Assert.AreEqual(1, accounts.ConnectionChecks);
        Assert.AreEqual("Der er forbindelse til serveren, men databasen er ikke klar.", status.Text);
        accounts.ConnectionStatus = new(true);
        ((IButtonController)check).SendClicked();
        Assert.AreEqual(2, accounts.ConnectionChecks);
        Assert.AreEqual("Der er forbindelse til server og database.", status.Text);
        Assert.IsTrue(check.IsEnabled);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AppearingOnAccountOrLoginPageRestoresOnlyLocalSession(bool loginPage)
    {
        var accounts = new PendingAccounts { PauseConnectionCheck = true };
        var model = new AccountViewModel(accounts, new Settings());
        ContentPage page = loginPage ? new LoginPage(model) : new AccountPage(model);

        InvokeLifecycle(page, "OnAppearing");

        Assert.AreEqual(1, accounts.Loads);
        Assert.AreEqual(0, accounts.ConnectionChecks, "Opening a page must not wake or query the database.");
        Assert.AreEqual(0, accounts.SignIns);
        Assert.AreEqual(0, accounts.Registrations);
        Assert.IsTrue(model.CanAct);
        Assert.AreEqual("Serverforbindelsen er ikke testet.",
            Control<Label>(page, UiAutomationIds.AccountConnectionStatus).Text);
    }

    [TestMethod]
    public void SavingServerAddressOnAccountPageDoesNotProbeTheDatabase()
    {
        var accounts = new PendingAccounts { PauseConnectionCheck = true };
        var settings = new Settings();
        var model = new AccountViewModel(accounts, settings);
        var page = new AccountPage(model, confirmServerSwitch: _ => Task.FromResult(true));
        InvokeLifecycle(page, "OnAppearing");
        Control<Entry>(page, UiAutomationIds.AccountServer).Text = "https://other.example/";

        ((IButtonController)Control<Button>(page, UiAutomationIds.AccountSaveServer)).SendClicked();

        Assert.AreEqual("https://other.example/", settings.BaseAddress);
        Assert.AreEqual(0, accounts.ConnectionChecks);
        Assert.IsTrue(model.CanAct);
        Assert.Contains("skiftet", model.StatusMessage);
    }

    [TestMethod]
    public void CancellingServerSwitchLeavesAddressAndSessionUnchanged()
    {
        var accounts = new PendingAccounts();
        var settings = new Settings();
        var model = new AccountViewModel(accounts, settings);
        var page = new AccountPage(model, confirmServerSwitch: _ => Task.FromResult(false));
        InvokeLifecycle(page, "OnAppearing");
        var identity = model.CurrentIdentity;
        Control<Entry>(page, UiAutomationIds.AccountServer).Text = "https://other.example/";

        ((IButtonController)Control<Button>(page, UiAutomationIds.AccountSaveServer)).SendClicked();

        Assert.AreEqual("https://localhost/", settings.BaseAddress);
        Assert.AreEqual(identity, model.CurrentIdentity);
        Assert.AreEqual(0, accounts.ConnectionChecks);
        Assert.AreEqual(string.Empty, model.StatusMessage);
    }

    [TestMethod]
    public async Task LeavingPageDuringConnectionCheckCancelsAndDoesNotClaimSuccess()
    {
        var accounts = new PendingAccounts { PauseConnectionCheck = true };
        var model = new AccountViewModel(accounts, new Settings());
        var page = new AccountPage(model);
        InvokeLifecycle(page, "OnAppearing");
        ((IButtonController)Control<Button>(page, UiAutomationIds.AccountCheckConnection)).SendClicked();
        await accounts.CheckStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(model.IsBusy);

        InvokeLifecycle(page, "OnDisappearing");

        await accounts.CheckCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (model.IsBusy && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse(model.IsBusy);
        Assert.AreEqual("Serverforbindelsen er ikke testet.",
            Control<Label>(page, UiAutomationIds.AccountConnectionStatus).Text);
        Assert.IsFalse(model.HasError);
        Assert.IsFalse(model.IsSignedIn);
    }

    [TestMethod]
    public void StartupRestoresExistingLoginAndEntersTheApp()
    {
        var accounts = new PendingAccounts { StoredSession = Session(), PauseConnectionCheck = true };
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        var page = new LoginPage(model, enterApp: () => { entered++; return Task.CompletedTask; });

        InvokeLifecycle(page, "OnAppearing");

        Assert.IsTrue(model.IsSignedIn);
        Assert.AreEqual(1, entered);
        Assert.AreEqual(0, accounts.SignIns);
        Assert.AreEqual(0, accounts.Registrations);
        Assert.AreEqual(0, accounts.ConnectionChecks, "A stored local session must enter immediately without waking the server.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StartupSuccessfulLoginOrSignupEntersAppAndClearsCredentials(bool signup)
    {
        var accounts = new PendingAccounts { ImmediateSignIn = true };
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        var page = new LoginPage(model, enterApp: () => { entered++; return Task.CompletedTask; });
        InvokeLifecycle(page, "OnAppearing");
        if (signup) ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSwitchMode)).SendClicked();
        Control<Entry>(page, UiAutomationIds.AccountUsername).Text = "golfer";
        Control<Entry>(page, UiAutomationIds.AccountPassword).Text = "test";
        Control<Entry>(page, UiAutomationIds.AccountConfirmPassword).Text = "test";

        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSubmit)).SendClicked();

        Assert.AreEqual(1, entered);
        Assert.IsTrue(model.IsSignedIn);
        Assert.AreEqual(signup ? 1 : 0, accounts.Registrations);
        Assert.AreEqual(signup ? 0 : 1, accounts.SignIns);
        Assert.AreEqual(string.Empty, Control<Entry>(page, UiAutomationIds.AccountPassword).Text);
        Assert.AreEqual(string.Empty, Control<Entry>(page, UiAutomationIds.AccountConfirmPassword).Text);
    }

    [TestMethod]
    public void StartupFailedLoginStaysOnLoginPageWithVisibleError()
    {
        var accounts = new PendingAccounts { SignInFailure = new ApiAccountException("Brugernavn eller adgangskode er forkert.") };
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        var page = new LoginPage(model, enterApp: () => { entered++; return Task.CompletedTask; });
        InvokeLifecycle(page, "OnAppearing");
        model.Username = "golfer";
        model.Password = "wrong";

        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSubmit)).SendClicked();

        Assert.AreEqual(0, entered);
        Assert.IsTrue(model.IsSignedOut);
        Assert.IsTrue(IsShown(Control<Button>(page, UiAutomationIds.LoginSubmit)));
        Assert.IsTrue(IsShown(Control<Label>(page, UiAutomationIds.AccountError)));
        Assert.Contains("forkert", Control<Label>(page, UiAutomationIds.AccountError).Text);
        Assert.AreEqual(string.Empty, model.Password);
    }

    [TestMethod]
    public void StartupConfirmationMismatchStaysOnSignupPageWithoutContactingServer()
    {
        var accounts = new PendingAccounts();
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        var page = new LoginPage(model, enterApp: () => { entered++; return Task.CompletedTask; });
        InvokeLifecycle(page, "OnAppearing");
        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSwitchMode)).SendClicked();
        model.Username = "golfer";
        model.Password = "test";
        model.ConfirmPassword = "different";

        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSubmit)).SendClicked();

        Assert.AreEqual(0, entered);
        Assert.AreEqual(0, accounts.Registrations);
        Assert.AreEqual(0, accounts.SignIns);
        Assert.IsTrue(model.IsCreatingAccount);
        Assert.IsTrue(IsShown(Control<Entry>(page, UiAutomationIds.AccountConfirmPassword)));
        Assert.IsTrue(IsShown(Control<Button>(page, UiAutomationIds.LoginSubmit)));
        Assert.Contains("ikke ens", Control<Label>(page, UiAutomationIds.AccountError).Text);
    }

    [TestMethod]
    public async Task StartupCanContinueOfflineDuringExplicitSlowServerCheckAndCancelsPageWork()
    {
        var accounts = new PendingAccounts { PauseConnectionCheck = true };
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        LoginPage? page = null;
        page = new LoginPage(model, enterApp: () =>
        {
            entered++;
            InvokeLifecycle(page!, "OnDisappearing");
            return Task.CompletedTask;
        });
        InvokeLifecycle(page, "OnAppearing");
        Assert.AreEqual(0, accounts.ConnectionChecks);
        ((IButtonController)Control<Button>(page, UiAutomationIds.AccountCheckConnection)).SendClicked();
        await accounts.CheckStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        model.Password = "remaining secret";
        model.ConfirmPassword = "remaining confirmation";
        var offline = Control<Button>(page, UiAutomationIds.LoginContinueOffline);
        Assert.IsTrue(model.IsBusy);
        Assert.IsTrue(IsShown(offline));
        Assert.IsTrue(offline.IsEnabled, "Offline access must not wait for the network.");

        ((IButtonController)offline).SendClicked();

        await accounts.CheckCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForIdle(model);
        Assert.AreEqual(1, entered);
        Assert.IsTrue(model.IsSignedOut);
        Assert.AreEqual(string.Empty, model.Password);
        Assert.AreEqual(string.Empty, model.ConfirmPassword);
        Assert.IsFalse(model.HasError);
    }

    [TestMethod]
    public async Task StartupOfflineNavigationIgnoresOverlappingTaps()
    {
        var navigationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var navigationFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var page = new LoginPage(new AccountViewModel(new PendingAccounts(), new Settings()), enterApp: async () =>
        {
            entered++;
            navigationStarted.TrySetResult(true);
            await navigationFinished.Task;
        });
        InvokeLifecycle(page, "OnAppearing");
        var offline = Control<Button>(page, UiAutomationIds.LoginContinueOffline);

        ((IButtonController)offline).SendClicked();
        await navigationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ((IButtonController)offline).SendClicked();

        Assert.AreEqual(1, entered);
        navigationFinished.SetResult(true);
        InvokeLifecycle(page, "OnDisappearing");
    }

    [TestMethod]
    public async Task InactiveStartupPageCannotNavigateAfterLateSuccessfulLogin()
    {
        var completion = new TaskCompletionSource<ApiSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accounts = new PendingAccounts { SignInCompletion = completion };
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        var page = new LoginPage(model, enterApp: () => { entered++; return Task.CompletedTask; });
        InvokeLifecycle(page, "OnAppearing");
        model.Username = "golfer";
        model.Password = "test";
        ((IButtonController)Control<Button>(page, UiAutomationIds.LoginSubmit)).SendClicked();
        await accounts.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        InvokeLifecycle(page, "OnDisappearing");
        completion.SetResult(Session());
        await WaitForIdle(model);

        Assert.IsTrue(accounts.LoginToken.IsCancellationRequested);
        Assert.AreEqual(0, entered, "A response that ignores cancellation must not reopen an inactive page.");
        Assert.AreEqual(string.Empty, model.Password);
        Assert.AreEqual(string.Empty, model.ConfirmPassword);
    }

    [TestMethod]
    public async Task InactiveStartupPageCannotNavigateAfterLateLocalSessionRestore()
    {
        var completion = new TaskCompletionSource<ApiSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accounts = new PendingAccounts { LoadCompletion = completion };
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        var page = new LoginPage(model, enterApp: () => { entered++; return Task.CompletedTask; });
        InvokeLifecycle(page, "OnAppearing");
        await accounts.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        InvokeLifecycle(page, "OnDisappearing");
        completion.SetResult(Session());
        await WaitForIdle(model);

        Assert.AreEqual(0, entered);
        Assert.IsFalse(model.HasError);
        Assert.AreEqual(0, accounts.ConnectionChecks);
    }

    [TestMethod]
    public async Task UserCanCancelWaitingLoginAndRetryWithoutLeavingThePage()
    {
        var accounts = new PendingAccounts();
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        var page = new LoginPage(model, enterApp: () => { entered++; return Task.CompletedTask; });
        InvokeLifecycle(page, "OnAppearing");
        var submit = Control<Button>(page, UiAutomationIds.LoginSubmit);
        var cancel = Control<Button>(page, UiAutomationIds.AccountCancelRequest);
        var hint = Control<Label>(page, UiAutomationIds.AccountRequestWait);
        Assert.IsFalse(IsShown(hint));
        model.Username = "golfer";
        model.Password = "test";
        model.ConfirmPassword = "test";

        ((IButtonController)submit).SendClicked();
        await accounts.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(IsShown(hint));
        Assert.IsFalse(string.IsNullOrWhiteSpace(hint.Text));
        Assert.IsTrue(IsShown(cancel));
        Assert.IsTrue(cancel.IsEnabled);
        Assert.IsFalse(submit.IsEnabled);
        ((IButtonController)submit).SendClicked();
        Assert.AreEqual(1, accounts.SignIns, "A duplicate clicked event must not start another pending login.");
        ((IButtonController)cancel).SendClicked();
        await accounts.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForIdle(model);
        Assert.AreEqual(0, entered);
        Assert.IsTrue(accounts.LoginToken.IsCancellationRequested);
        Assert.IsFalse(model.HasError);
        Assert.AreEqual(string.Empty, model.Password);
        Assert.AreEqual(string.Empty, model.ConfirmPassword);
        Assert.IsFalse(IsShown(hint));
        Assert.IsTrue(!IsShown(cancel) || !cancel.IsEnabled);
        Assert.IsTrue(submit.IsEnabled);

        accounts.ImmediateSignIn = true;
        model.Password = "test";
        ((IButtonController)submit).SendClicked();

        Assert.AreEqual(2, accounts.SignIns);
        Assert.AreEqual(1, entered);
        Assert.IsTrue(model.IsSignedIn);
        Assert.AreEqual(0, accounts.ConnectionChecks, "Login must not add a separate database health request.");
        Assert.IsFalse(IsShown(hint));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task UserCanCancelExplicitServerCheckAndRetryOnEitherPage(bool loginPage)
    {
        var accounts = new PendingAccounts { PauseConnectionCheck = true };
        var model = new AccountViewModel(accounts, new Settings());
        ContentPage page = loginPage ? new LoginPage(model) : new AccountPage(model);
        InvokeLifecycle(page, "OnAppearing");
        var check = Control<Button>(page, UiAutomationIds.AccountCheckConnection);
        var cancel = Control<Button>(page, UiAutomationIds.AccountCancelRequest);
        var hint = Control<Label>(page, UiAutomationIds.AccountRequestWait);
        Assert.AreEqual(0, accounts.ConnectionChecks);

        ((IButtonController)check).SendClicked();
        await accounts.CheckStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(IsShown(hint));
        Assert.IsFalse(check.IsEnabled);
        Assert.IsTrue(IsShown(cancel));
        Assert.IsTrue(cancel.IsEnabled);
        ((IButtonController)check).SendClicked();
        Assert.AreEqual(1, accounts.ConnectionChecks, "A duplicate clicked event must not add another pending health request.");
        ((IButtonController)cancel).SendClicked();
        await accounts.CheckCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitForIdle(model);
        Assert.AreEqual("Serverforbindelsen er ikke testet.",
            Control<Label>(page, UiAutomationIds.AccountConnectionStatus).Text);
        Assert.IsFalse(model.HasError);
        Assert.IsFalse(IsShown(hint));
        Assert.IsTrue(check.IsEnabled);

        accounts.PauseConnectionCheck = false;
        ((IButtonController)check).SendClicked();

        Assert.AreEqual(2, accounts.ConnectionChecks);
        Assert.AreEqual("Der er forbindelse til server og database.",
            Control<Label>(page, UiAutomationIds.AccountConnectionStatus).Text);
        Assert.IsFalse(IsShown(hint));
        Assert.IsTrue(model.IsSignedOut);
    }

    [TestMethod]
    public async Task LocalStartupRestoreDoesNotShowServerWakeHintOrCallHealth()
    {
        var restored = new TaskCompletionSource<ApiSession?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accounts = new PendingAccounts { LoadCompletion = restored };
        var model = new AccountViewModel(accounts, new Settings());
        var entered = 0;
        LoginPage? page = null;
        page = new LoginPage(model, enterApp: () =>
        {
            entered++;
            InvokeLifecycle(page!, "OnDisappearing");
            return Task.CompletedTask;
        });
        InvokeLifecycle(page, "OnAppearing");
        await accounts.LoadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsTrue(model.IsBusy);
        Assert.IsFalse(IsShown(Control<Label>(page, UiAutomationIds.AccountRequestWait)));
        Assert.AreEqual(0, accounts.ConnectionChecks);
        var offline = Control<Button>(page, UiAutomationIds.LoginContinueOffline);
        Assert.IsTrue(offline.IsEnabled);
        ((IButtonController)offline).SendClicked();
        restored.SetResult(null);
        await WaitForIdle(model);

        Assert.AreEqual(1, entered);
        Assert.AreEqual(0, accounts.ConnectionChecks);
    }

    [TestMethod]
    public void SettingsLoginPageDoesNotOfferStartupOfflineNavigation()
    {
        var page = new LoginPage(new AccountViewModel(new PendingAccounts(), new Settings()));
        var offline = Descendants(page).OfType<Button>()
            .SingleOrDefault(button => button.AutomationId == UiAutomationIds.LoginContinueOffline);

        Assert.IsTrue(offline is null || !IsShown(offline));
    }

    private static ApiSession Session() => new(Guid.NewGuid(), "stored-account", "golfer", "https://localhost/",
        "test-access-token", "test-refresh-token", DateTimeOffset.UtcNow.AddHours(1));

    private static async Task WaitForIdle(AccountViewModel model)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (model.IsBusy && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.IsFalse(model.IsBusy, "Page work must release the view model's gate.");
        // Let the page's awaiting event handler finish its navigation/cancellation decision.
        await Task.Yield();
    }

    private static void InvokeLifecycle(ContentPage page, string method) =>
        page.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(page, null);

    private static T Control<T>(ContentPage page, string automationId) where T : VisualElement =>
        Descendants(page).OfType<T>().Single(element => element.AutomationId == automationId);

    private static bool IsShown(VisualElement element)
    {
        for (Element? current = element; current is not null; current = current.Parent)
            if (current is VisualElement visual && !visual.IsVisible) return false;
        return true;
    }

    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement element)
    {
        yield return element;
        foreach (var child in element.GetVisualChildren())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }

    private sealed class Settings : IProductsApiSettings
    {
        public string BaseAddress { get; set; } = "https://localhost/";
    }

    private sealed class ImmediateDispatcherProvider : IDispatcherProvider
    {
        private readonly IDispatcher dispatcher = new ImmediateDispatcher();
        public IDispatcher GetForCurrentThread() => dispatcher;
    }

    private sealed class ImmediateDispatcher : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { action(); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) => throw new NotSupportedException();
        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }

    private sealed class PendingAccounts : IApiAccountService
    {
        public Task SwitchServerAsync(Uri server, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken LoginToken { get; private set; }
        public int Registrations { get; private set; }
        public int SignIns { get; private set; }
        public ApiSession? StoredSession { get; set; }
        public int Loads { get; private set; }
        public TaskCompletionSource<bool> LoadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<ApiSession?>? LoadCompletion { get; set; }
        public bool ImmediateSignIn { get; set; }
        public Exception? SignInFailure { get; set; }
        public TaskCompletionSource<ApiSession>? SignInCompletion { get; set; }
        public TaskCompletionSource<ApiConnectionStatus>? CheckCompletion { get; set; }
        public string? RegisteredUsername { get; private set; }
        public string? RegisteredPassword { get; private set; }
        public ApiConnectionStatus ConnectionStatus { get; set; } = new(true);
        public int ConnectionChecks { get; private set; }
        public bool PauseConnectionCheck { get; set; }
        public TaskCompletionSource<bool> CheckStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> CheckCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ApiSession?> LoadAccountAsync(Uri server, CancellationToken cancellationToken = default)
        {
            Loads++;
            LoadStarted.TrySetResult(true);
            return LoadCompletion is null ? StoredSession : await LoadCompletion.Task;
        }

        public async Task<ApiConnectionStatus> CheckConnectionAsync(Uri server, CancellationToken cancellationToken = default)
        {
            ConnectionChecks++;
            CheckStarted.TrySetResult(true);
            if (CheckCompletion is not null) return await CheckCompletion.Task;
            if (PauseConnectionCheck)
            {
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { CheckCancelled.TrySetResult(true); throw; }
            }
            return ConnectionStatus;
        }

        public async Task<ApiSession> SignInAsync(Uri server, string username, string password,
            CancellationToken cancellationToken = default)
        {
            SignIns++;
            LoginToken = cancellationToken;
            Started.TrySetResult(true);
            if (SignInFailure is not null) throw SignInFailure;
            if (ImmediateSignIn) return Session() with { Username = username, ServerAddress = server.AbsoluteUri };
            if (SignInCompletion is not null) return await SignInCompletion.Task;
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException)
            {
                Cancelled.TrySetResult(true);
                throw;
            }
            throw new AssertFailedException("The pending request must end through page cancellation.");
        }

        public Task<ApiSession> RegisterAsync(Uri server, string username, string password,
            CancellationToken cancellationToken = default)
        {
            Registrations++;
            RegisteredUsername = username;
            RegisteredPassword = password;
            return Task.FromResult(new ApiSession(Guid.NewGuid(), "registered-account", username,
                server.AbsoluteUri, "test-access-token", "test-refresh-token", DateTimeOffset.UtcNow.AddHours(1)));
        }
        public Task SignOutAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ApiSession> GetSessionAsync(Uri server, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<T> WithSessionAsync<T>(ApiSession session, Func<Task<T>> action,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
