# Products API connection

Current endpoint policy: users cannot configure connections, including in Debug builds. The deployment owner selects staging addresses at build time; see [super-admin setup](SUPER_ADMIN.md). Server-editor instructions below describe the previous development workflow and no longer apply.

Products remains a temporary API connection-test feature during local development.
The API returns response DTOs; PostgreSQL entities and queries belong to
`GolfSG.Server.Infrastructure`. Database updates are run separately with
`GolfSG.Database.Setup`.

Configure the server under **Indstillinger → Konto og server** and select
**Gem serveradresse**. Open **Konto og login** on the start screen, or
**Indstillinger → Log ind / Opret konto**, to sign in. For signup, select
**Opret en ny konto**, enter and repeat the password, then select **Opret konto**.
Signup requires a 3–50 character username using ASCII letters, digits,
`_`, `.` or `-`. The Debug app accepts a simple password such as `test` (1–128
characters, not blank) when the API uses Debug or explicitly enables local password
testing. Release registration defaults to 12–128 characters with uppercase,
lowercase, digit and symbol. See
[Round sync setup](ROUND_SYNC_SETUP.md) for password rules, local IIS publishing
and account binding.

Open **Settings → Products** (in the Danish UI: **Indstillinger → Produkter**) after signing in.
The page loads products automatically, supports pull-to-refresh and an **Opdater**
button, and shows loading, empty and error states. **Gem og hent** saves a new
server address on the device and loads from it. Use the Account page for the normal
connection settings and the dedicated login screen to sign in. Products are shared diagnostic records among signed-in
development users; per-account ownership applies to round history. Products are read from the API;
the app's golf rounds and practice sessions continue to use their existing local storage.
Saved history also supports manual round sync using this same server address; see
[Round sync setup](ROUND_SYNC_SETUP.md).

## Run locally

Prepare or update PostgreSQL using [Local database setup](LOCAL_DATABASE_SETUP.md)
before starting the API. The versioned command supports existing databases as well
as fresh installations.

1. Start `GolfSG.Api` in Visual Studio using the **https** profile, or run:
   ```powershell
   dotnet run --project GolfSG.Api/GolfSG.Api.csproj --launch-profile https
   ```
2. Confirm the API's User Secrets contain a valid `ConnectionStrings:Default`.
   The API uses the restricted runtime connection. Owner credentials are consumed
   by the separate setup command; database credentials never belong in the MAUI app.
3. Run the MAUI app in Debug, open **Konto og login**, then create an account or sign in.
4. Choose **Tjek forbindelse** on the login screen to check the API and
   account database before signing in. Opening the app or saving the account server address
   makes no server call. Open
   Products after sign-in. Anonymous Products and Rounds requests
   return 401; that is expected until a bearer token is supplied.

Explicit API requests allow two minutes each for a server that needs about one
minute to wake. Login and connection checks display a wait message and **Annuller**.
Saved login is restored locally on startup, and no polling or automatic retries
keep the server or database awake. Products loads only when its page is opened or
refreshed. The current in-process IIS configuration needs no request timeout change
for a one-minute API startup. If the database itself also sleeps, check its connection
timeout separately; extending the HTTP timeout does not extend database query timeouts.

Debug defaults:

| Device | Server address | Additional setup |
| --- | --- | --- |
| Android emulator | `https://10.0.2.2:7151/` | None |
| Android phone over USB | `https://localhost:7151/` | Run `adb reverse tcp:7151 tcp:7151` for the connected phone |
| Windows | `https://localhost:7151/` | None |

### USB forwarding for an Android phone

With the phone connected over USB and USB debugging enabled, run:

```powershell
adb reverse tcp:7151 tcp:7151
```

This connects port 7151 on the phone to port 7151 on the development computer.
Use `https://localhost:7151/` as the server address in **Indstillinger → Konto og server**
and choose **Gem serveradresse**, then open **Log ind / Opret konto**. Keep the API running with its HTTPS profile.

Run the forwarding command once per USB/ADB connection. Repeat it after
unplugging and reconnecting the phone, restarting the phone, or restarting ADB.
Rebuilding or restarting the app, or restarting the API, does not require it.

Check the current forwarding with:

```powershell
adb reverse --list
```

Look for `tcp:7151 tcp:7151`. If that mapping is missing, run the forwarding
command again. If the API uses a different HTTPS port, use that port in both
`tcp:` arguments and in the app's server address. The Android emulator uses
`10.0.2.2` and does not need this USB forwarding step.

### Local HTTPS certificates

Debug builds accept a valid, self-signed ASP.NET development certificate only
for local addresses (`localhost`, `127.0.0.1`, `::1`, `10.0.2.2`) and the development
PC's Wi-Fi address (`192.168.50.189`). Other LAN addresses are not accepted. The certificate
must identify `localhost` and contain the ASP.NET development certificate extension.
This exception is not configured in Release builds. HTTP URLs and addresses with
embedded credentials are rejected. Redirects are not followed.

The emulator alias is documented in Microsoft's
[local web services guide](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/local-web-services?view=net-maui-10.0).

### Android phone over Wi-Fi with local IIS

Install a **Debug** build of the phone app containing the Wi-Fi certificate exception.
Publishing the API to IIS does not update the phone app. Keep the existing ASP.NET
development certificate on the IIS HTTPS binding (port 443), and ensure the binding
accepts the PC's LAN IP without requiring a host name. Allow inbound TCP 443 on the
Windows Firewall Private profile and connect both devices to the same home network.
In **Indstillinger → Konto og server**, save `https://192.168.50.189/`, then choose
**Tjek forbindelse**. No ADB forwarding is needed. If the API runs from Visual Studio
instead, listen on `https://0.0.0.0:7151`, allow TCP 7151 and save
`https://192.168.50.189:7151/`.

Reserve this IP for the PC in the router. If it changes, update the explicit Wi-Fi
address in `LocalDevelopmentCertificate.cs` and rebuild/install the Debug app.
Release builds continue to require a normally trusted HTTPS certificate.

## Local IIS

The local IIS deployment is published to `C:\Websites\Maui.Api` and has an
HTTPS binding on port 443, alongside its HTTP binding on port 81. Check the API
before login using `https://localhost/api/health`: 200 means the account database
is readable; 503 means the API is running but that database is unavailable or not
ready. The response exposes no database connection details. Use
`https://localhost/api/products` and `https://localhost/api/rounds` after sign-in.
These endpoints now return 401 to anonymous browser requests. The API does not
have a page at `/`. Register/sign in through the app or `/api/auth` and use the
returned bearer token for an authenticated API check.

Full IIS requires the **.NET 10 Hosting Bundle**, which installs the ASP.NET Core
IIS module. Having the SDK or IIS Express installed is not sufficient.

IIS does not use Visual Studio User Secrets. This deployment reads
`ConnectionStrings:Default` from
`C:\Websites\Maui.Api\appsettings.Production.json`. That private file contains
the existing local database connection and is readable by IIS. Keep it outside
the repository. The current folder publish profile uses
`DeleteExistingFiles=false` so subsequent publishes retain this server file;
if the deployment folder is cleared, restore the private settings as well.

Apply migration 002 with the standalone setup tool before republishing this API,
then rebuild/install the phone app so it supports account login. The instructions
here do not publish the API or update the running database automatically.

Persist the API's Data Protection keys in a private folder outside the publish
directory. Add this alongside the existing connection in the private IIS settings:

```json
{
  "Auth": {
    "DataProtectionKeysPath": "C:\\Websites\\GolfSG.DataProtection"
  }
}
```

Publish the local IIS API in **Release**. To use simple passwords from the Debug
phone app, add `"AllowSimplePasswordsForLocalTesting": true` inside that same
private `Auth` object. This is an explicit API setting for local testing; Release
registration keeps the stronger rules by default. Remove the setting or make it
`false` when local password testing ends. A database update is not needed.

If every API request returns **IIS 500.30**, the application failed to start.
On this PC, a Debug publish was blocked by Windows Application Control with
`0x800711C7` when loading `GolfSG.Application.dll`; this is a hosting failure rather
than an invalid username/password. Check **Event Viewer → Windows Logs → Application**
for the specific startup error. Publish in Release before testing login again.

Create that folder and give the IIS application-pool identity read/write access;
keep access restricted to the operator and pool identity. Enable **Load User Profile**
for the pool and retain the same identity. On Windows, the configured keys are
encrypted with DPAPI for that identity. The application name is fixed to `GolfSG.Api`.
Keep the directory out of Git and outside folder-publish cleanup, and back it up
securely. Losing the keys or changing identity can invalidate existing tokens;
users must sign in again. No JWT signing secret is required for these protected
opaque bearer tokens.

The IIS HTTPS binding uses a copy of the existing ASP.NET development certificate
in the Local Machine personal certificate store. This lets the Debug app use the
local certificate exception described above. The IIS Express certificate does
not have the ASP.NET development certificate extension and does not qualify for
that exception. Use a normally trusted certificate for a hosted Release app.

For the Android phone over USB, run:

```powershell
adb reverse tcp:443 tcp:443
```

Enter `https://localhost/` in **Indstillinger → Konto og server**, choose
**Gem serveradresse**, then open **Log ind / Opret konto**. This saved address also
applies to **Historik → Synkroniser**.
The emulator uses `https://10.0.2.2/`. Repeat USB forwarding after reconnecting
the phone or restarting ADB, as described above.

## Hosted API

Release builds have no local default server address. Enter the deployed API's
HTTPS address on the Account page, choose **Gem serveradresse**, and sign in. The hosted API
must present a normally trusted certificate. The Android Internet permission is
explicitly included in both Debug and Release builds.

Moving PostgreSQL to Neon only changes the API's connection string. The app
continues to communicate with the API through HTTPS and JSON.

## Account API

| Request | Body/result |
| --- | --- |
| `POST /api/auth/register` | `{ "username": "...", "password": "..." }`; returns account ID and username |
| `POST /api/auth/login` | Same credentials; returns `tokenType`, `accessToken`, `expiresIn`, `refreshToken` |
| `POST /api/auth/refresh` | `{ "refreshToken": "..." }`; returns renewed tokens |
| `GET /api/auth/me` | Bearer-authenticated account ID and username |

Registration validates the username/password and reports invalid input with 400.
Login failures return 401 without revealing whether a username exists. Five failed
password attempts temporarily lock an account for five minutes. Account endpoints
allow 20 requests per minute per IP before returning 429. This pilot has no email,
password recovery, account-deletion endpoint, or external identity provider. PostgreSQL
stores Identity password hashes; the app keeps access/refresh tokens in secure storage
and clears password fields after sign-in/signup attempts.
