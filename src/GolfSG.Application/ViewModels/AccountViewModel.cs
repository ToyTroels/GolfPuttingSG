using GolfSG.Application.Accounts;
using GolfSG.Application.Products;

namespace GolfSG.Application.ViewModels;

public sealed class AccountViewModel(IApiAccountService accounts, IProductsApiSettings settings) : ViewModelBase
{
    private const string ConnectionNotChecked = "Serverforbindelsen er ikke testet.";
    private const string AccountUpdateFailed = "Kontoen kunne ikke opdateres. Dine lokale runder er bevaret. Prøv igen.";
    private string serverAddress = settings.BaseAddress;
    private string username = string.Empty;
    private string password = string.Empty;
    private string confirmPassword = string.Empty;
    private string errorMessage = string.Empty;
    private string statusMessage = string.Empty;
    private string connectionStatusMessage = ConnectionNotChecked;
    private long serverAddressRevision;
    private ApiSession? session;
    private bool isBusy;
    private bool networkOperationActive;
    private bool isCreatingAccount;
    private readonly SemaphoreSlim operationGate = new(1, 1);

    public string ServerAddress
    {
        get => serverAddress;
        set
        {
            if (!SetProperty(ref serverAddress, value ?? string.Empty)) return;
            serverAddressRevision++;
            ConnectionStatusMessage = ConnectionNotChecked;
        }
    }
    public string Username { get => username; set => SetProperty(ref username, value ?? string.Empty); }
    public string Password { get => password; set => SetProperty(ref password, value ?? string.Empty); }
    public string ConfirmPassword { get => confirmPassword; set => SetProperty(ref confirmPassword, value ?? string.Empty); }
    public string ErrorMessage { get => errorMessage; private set { if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public string ConnectionStatusMessage { get => connectionStatusMessage; private set => SetProperty(ref connectionStatusMessage, value); }
    public bool HasError => ErrorMessage.Length > 0;
    public bool IsSignedIn => session is not null;
    public bool IsSignedOut => !IsSignedIn;
    public bool IsCreatingAccount => isCreatingAccount;
    public string AuthenticationTitle => IsSignedIn ? "Din konto" : AuthenticationActionText;
    public string AuthenticationActionText => IsCreatingAccount ? "Opret konto" : "Log ind";
    public string AuthenticationSwitchText => IsCreatingAccount ? "Jeg har allerede en konto" : "Opret en ny konto";
    public bool CanConfigureServer => settings.CanConfigureEndpoint;
    public string CurrentIdentity => session is null ? "Du er ikke logget ind." :
        CanConfigureServer ? $"Logget ind som {session.Username}\n{session.ServerAddress}" : $"Logget ind som {session.Username}";
    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (!SetProperty(ref isBusy, value)) return;
            OnPropertyChanged(nameof(CanAct));
            OnPropertyChanged(nameof(CanSignOut));
            OnPropertyChanged(nameof(CanCancelRequest));
            OnPropertyChanged(nameof(RequestWaitMessage));
            OnPropertyChanged(nameof(HasRequestWaitMessage));
        }
    }
    public bool CanAct => !IsBusy;
    public bool CanSignOut => !IsBusy && IsSignedIn;
    public bool CanCancelRequest => IsBusy && networkOperationActive;
    public string RequestWaitMessage => CanCancelRequest
        ? "Serveren kan være i dvale og bruge omkring et minut på at starte."
        : string.Empty;
    public bool HasRequestWaitMessage => !string.IsNullOrEmpty(RequestWaitMessage);

    public Task LoadAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        ConnectionStatusMessage = ConnectionNotChecked;
        ServerAddress = settings.BaseAddress;
        try
        {
            SetSession(await accounts.LoadAccountAsync(Address(), token));
            if (session is not null) Username = session.Username;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ApiAccountException ex) { SetSession(null); ErrorMessage = ex.Message; }
        catch (Exception) { SetSession(null); ErrorMessage = AccountUpdateFailed; }
    }, token, waitForBusy: true);

    public Task SaveServerAddressAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        ConnectionStatusMessage = ConnectionNotChecked;
        await SaveAddressAsync(token);
        StatusMessage = "Serveradressen er gemt.";
    }, token);

    public bool RequiresServerSwitch => ProductsApiAddress.TryNormalize(ServerAddress, out var target) &&
        (!ProductsApiAddress.TryNormalize(settings.BaseAddress, out var current) || target != current);

    public Task SwitchServerAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        if (!CanConfigureServer) throw new ApiAccountException("Appen bruger GolfSG-serveren automatisk.");
        var address = Address();
        try { await accounts.SwitchServerAsync(address, token); }
        finally { SetSession(null); ClearCredentials(); }
        settings.BaseAddress = address.AbsoluteUri;
        Username = string.Empty;
        ServerAddress = settings.BaseAddress;
        ConnectionStatusMessage = ConnectionNotChecked;
        StatusMessage = "Serveren er skiftet. Log ind eller opret en konto. Dine lokale runder er bevaret og kan synkroniseres manuelt til den nye konto.";
    }, token, clearPassword: true);

    public Task CheckConnectionAsync(CancellationToken token = default) =>
        RunAsync(() => CheckConnectionCoreAsync(token), token, networkOperation: true);

    public Task SignInAsync(CancellationToken token = default) => AuthenticateAsync(false, token);
    public Task RegisterAsync(CancellationToken token = default) => AuthenticateAsync(true, token);
    public Task SubmitCredentialsAsync(CancellationToken token = default) => AuthenticateAsync(IsCreatingAccount, token);

    public void SwitchAuthenticationMode()
    {
        if (!CanAct) return;
        SetProperty(ref isCreatingAccount, !isCreatingAccount, nameof(IsCreatingAccount));
        OnPropertyChanged(nameof(AuthenticationTitle));
        OnPropertyChanged(nameof(AuthenticationActionText));
        OnPropertyChanged(nameof(AuthenticationSwitchText));
        ClearCredentials();
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
    }

    public void ClearCredentials()
    {
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    private Task AuthenticateAsync(bool register, CancellationToken token) => RunAsync(async () =>
    {
        if (register && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            throw new ApiAccountException("Adgangskoderne er ikke ens. Indtast den samme adgangskode i begge felter.");
        var address = Address();
        var previous = settings.BaseAddress;
        settings.BaseAddress = address.AbsoluteUri;
        try
        {
            SetSession(register ? await accounts.RegisterAsync(address, Username, Password, token)
                : await accounts.SignInAsync(address, Username, Password, token));
        }
        catch
        {
            if (settings.BaseAddress == address.AbsoluteUri) settings.BaseAddress = previous;
            throw;
        }
        ServerAddress = address.AbsoluteUri;
        Username = session!.Username;
        StatusMessage = register ? "Kontoen er oprettet. Synkroniser dine runder i Historik." : "Du er logget ind. Synkroniser dine runder i Historik.";
    }, token, clearPassword: true, networkOperation: true);

    public Task SignOutAsync(CancellationToken token = default) => RunAsync(async () =>
    {
        await accounts.SignOutAsync(token);
        SetSession(null);
        ClearCredentials();
        StatusMessage = "Du er logget ud. Dine runder er stadig gemt på enheden.";
    }, token, clearPassword: true);

    private Uri Address() => ProductsApiAddress.TryNormalize(CanConfigureServer ? ServerAddress : settings.BaseAddress, out var address) ? address!
        : throw new ApiAccountException("Angiv en gyldig HTTPS-adresse, f.eks. https://localhost/.");

    private async Task CheckConnectionCoreAsync(CancellationToken token)
    {
        var revision = serverAddressRevision;
        Uri? checkedAddress = null;
        try
        {
            token.ThrowIfCancellationRequested();
            checkedAddress = Address();
            ConnectionStatusMessage = "Tjekker forbindelsen til server og database…";
            var result = await accounts.CheckConnectionAsync(checkedAddress, token);
            token.ThrowIfCancellationRequested();
            if (IsCurrentConnectionCheck(revision, checkedAddress))
                ConnectionStatusMessage = result.DatabaseAvailable
                    ? "Der er forbindelse til server og database."
                    : "Der er forbindelse til serveren, men databasen er ikke klar.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (IsCurrentConnectionCheck(revision, checkedAddress)) ConnectionStatusMessage = ConnectionNotChecked;
        }
        catch (ApiAccountException ex)
        {
            if (IsCurrentConnectionCheck(revision, checkedAddress)) ConnectionStatusMessage = ex.Message;
        }
        catch (Exception)
        {
            if (IsCurrentConnectionCheck(revision, checkedAddress))
                ConnectionStatusMessage = "Serverforbindelsen kunne ikke kontrolleres. Kontrollér serveradressen og prøv igen.";
        }
    }

    private bool IsCurrentConnectionCheck(long revision, Uri? checkedAddress) =>
        revision == serverAddressRevision && (checkedAddress is null ||
            ProductsApiAddress.TryNormalize(ServerAddress, out var currentAddress) &&
            currentAddress!.AbsoluteUri == checkedAddress.AbsoluteUri);

    private async Task SaveAddressAsync(CancellationToken token)
    {
        var address = Address();
        var previous = settings.BaseAddress;
        settings.BaseAddress = address.AbsoluteUri;
        try { SetSession(await accounts.LoadAccountAsync(address, token)); }
        catch { settings.BaseAddress = previous; throw; }
        ServerAddress = address.AbsoluteUri;
    }

    private void SetSession(ApiSession? value)
    {
        session = value;
        OnPropertyChanged(nameof(IsSignedIn));
        OnPropertyChanged(nameof(IsSignedOut));
        OnPropertyChanged(nameof(AuthenticationTitle));
        OnPropertyChanged(nameof(CurrentIdentity));
        OnPropertyChanged(nameof(CanSignOut));
    }

    private async Task RunAsync(Func<Task> action, CancellationToken token, bool clearPassword = false,
        bool waitForBusy = false, bool networkOperation = false)
    {
        if (waitForBusy)
        {
            try { await operationGate.WaitAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { if (clearPassword) ClearCredentials(); return; }
        }
        else
        {
            try { if (!await operationGate.WaitAsync(0, token)) return; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { if (clearPassword) ClearCredentials(); return; }
        }
        networkOperationActive = networkOperation;
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try { await action(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (ApiAccountException ex) { ErrorMessage = ex.Message; }
        catch (Exception) { ErrorMessage = AccountUpdateFailed; }
        finally
        {
            if (clearPassword) ClearCredentials();
            IsBusy = false;
            networkOperationActive = false;
            operationGate.Release();
        }
    }
}
