# Products API connection

Open **Settings → Products** (in the Danish UI: **Indstillinger → Produkter**).
The page loads products automatically, supports pull-to-refresh and an **Opdater**
button, and shows loading, empty and error states. **Gem og hent** saves a new
server address on the device and loads from it. Products are read from the API;
the app's golf rounds and practice sessions continue to use their existing storage.

## Run locally

1. Start `GolfSG.Api` in Visual Studio using the **https** profile, or run:
   ```powershell
   dotnet run --project GolfSG.Api/GolfSG.Api.csproj --launch-profile https
   ```
2. Confirm the API's User Secrets contain a valid `ConnectionStrings:Default`.
   Database credentials belong only in the API, never in the MAUI app.
3. Run the MAUI app in Debug and open the Products page.

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
Use `https://localhost:7151/` as the server address in **Indstillinger → Produkter**
and choose **Gem og hent**. Keep the API running with its HTTPS profile.

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
for local addresses (`localhost`, `127.0.0.1`, `::1`, `10.0.2.2`). The certificate
must identify `localhost` and contain the ASP.NET development certificate extension.
This exception is not configured in Release builds. HTTP URLs and addresses with
embedded credentials are rejected. Redirects are not followed.

The emulator alias is documented in Microsoft's
[local web services guide](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/local-web-services?view=net-maui-10.0).

## Hosted API

Release builds have no local default server address. Enter the deployed API's
HTTPS address on the Products page and choose **Gem og hent**. The hosted API
must present a normally trusted certificate. The Android Internet permission is
explicitly included in both Debug and Release builds.

Moving PostgreSQL to Neon only changes the API's connection string. The app
continues to communicate with the API through HTTPS and JSON.
