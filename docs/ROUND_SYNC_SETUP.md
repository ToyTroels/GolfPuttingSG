# Saved round sync

## One-time database setup

Follow [Local database setup](LOCAL_DATABASE_SETUP.md) to configure the owner
connection and run the versioned database update command. This works with a fresh
database or the existing **MauiAppDB**, preserving products and rounds. The older
`001_saved_rounds.sql` script is retained for compatibility; use the versioned
command in `GolfSG.Database.Setup` for future updates. The retained script now
lives in `src/GolfSG.Database.Setup/Sql`. The API has no schema-update command
and does not run migrations at startup.

## Try it from the app

1. Run the database setup command, rebuild the app and API, then start the API using its HTTPS profile.
2. Signed-out users see the login/signup screen first on a fresh launch. Open
   **Konto og serverindstillinger**, enter the server address and select **Gem serveradresse**.
   You can also configure it under **Indstillinger → Konto og server**. For a USB phone,
   use the forwarding described in [Products API setup](PRODUCTS_API_SETUP.md).
   Choose **Tjek forbindelse** to check the API and account database without requiring login.
   Opening the page and saving the address do not contact the server. The status distinguishes a ready server/database
   from an unavailable database or an address/API error.
3. Return to the login screen. It is also available via **Konto og login** on the main page,
   or **Indstillinger → Log ind / Opret konto**.
   Enter **Brugernavn** and **Adgangskode** to sign in. For a new account, select
   **Opret en ny konto**, repeat the password and select **Opret konto**. Successful
   registration also signs in. **Tjek forbindelse** is available before sign-in.
4. Startup login/signup opens the main page automatically. A remembered account
   is restored locally and continues immediately on later launches, even while the API is asleep. **Fortsæt uden login** opens the local
   app immediately without needing a server connection. Open **Historik** on the
   main page, or select **Åbn historik** when using the in-app login screen.
   Save a round or putting game as usual,
   or select **Importer runder fra fil** and choose a GolfSG export (`rounds.json`).
   Select **Synkroniser** to upload the local history to your account.
5. The status shows how many records were sent, downloaded or deleted. Check
   `SELECT user_id, id, version, payload IS NULL AS deleted FROM public.saved_rounds;` in pgAdmin.
6. Stop the API, save another round and try sync. The saved history remains available.
   Start the API again and press **Synkroniser** to retry.

Import is available offline and merges the file into the device's history. Only
GolfSG round JSON exports are supported; arbitrary CSV or third-party formats
need a separate converter. Existing round IDs keep their saved local version,
so importing the same file again adds no duplicates. Export is available under
**Indstillinger → Lagring**. Importing a file does not upload it automatically.
When signed in, **Historik → Synkroniser** uploads imported and other local rounds
to the installation's linked account.

The API may take about one minute to wake. Explicit requests allow two minutes each;
login and connection checks show a wait message and **Annuller**. Sync also explains
the possible wait. Cancelling or leaving a page stops waiting, and local history
remains available. Retry manually after a timeout; the app does not poll, prewarm
the server or retry automatically. Cancellation cannot undo an account or round
already saved on the server.

App startup, local play, file import, returning from the background and saving
the account server address make no API or database calls. A manual connection
check performs a database read. Manual sync fetches the account's server history
once, then writes only changed or deleted rounds; unchanged rounds cause no writes.
Saved tokens are refreshed only when an explicit authenticated operation needs
them, with a two-minute expiry margin to allow the API to wake. The account identity
endpoint reads validated token claims without an additional database lookup.

Usernames are 3–50 ASCII letters, digits, underscores, dots or hyphens. In **Debug**,
registration accepts a simple password such as `test` (1–128 characters, not blank).
For local IIS, keep the API publish configuration in **Release** and explicitly set
`Auth:AllowSimplePasswordsForLocalTesting` to `true` in its private
`appsettings.Production.json`, while running the phone app in Debug. This local
testing option allows simple registration passwords on that API without changing
Windows security settings. Debug API builds also accept simple passwords, but
Windows Application Control can block their startup on this PC. Neither option
requires a database update.
In **Release**, registration requires a 12–128 character password containing an
uppercase letter, lowercase letter, digit and symbol unless the API's explicit
local testing option is enabled. Remove that option or
set it to `false` when ending local password testing. The ASP.NET environment name
does not select these registration rules.
Existing accounts can still sign in with their original password in either build.
Usernames are matched without case sensitivity. Password recovery and email
verification are not part of this local pilot.

Manual sync requires sign-in. The first successful sign-in binds that installation's
sync history to the account ID and server. Signing out with **Log ud** clears saved
tokens while leaving local history and the binding intact. Sign back into the same
account to continue. To change servers, enter the new HTTPS address in **Konto og server**,
choose **Gem serveradresse**, and confirm **Skift server**. This signs out, releases
the old account binding, and resets sync tracking, including old deletion records.
Local rounds remain available. Sign in or register on the new server and manually
sync to upload them to that account. Nothing is uploaded during the switch; old
server data is unchanged. Changing accounts on the same server remains restricted.
Export local history before any app-data reset. Passwords are not persisted; tokens use the platform's
secure storage. Expired sessions require sign-in again when token refresh cannot succeed.

Saving rounds, games and benchmarks stays local. Sync is explicit, without polling
or uploads after each putt. Existing rounds are included on the first sync. The full
round JSON includes tracking options, hole/shot data, game/benchmark metadata and
completed course-practice results. Active unfinished sessions, course setup files,
image files and diagnostics are not uploaded. A course image used on another device
must also be available on that device.

## Retry, deletion and conflicts

Round IDs stay the same across retries. PostgreSQL stores one row per account and ID. Writes
check the version read from the server, returning **409 Conflict** if another write
happened in between. Retry reads the current server state first.

The app keeps synchronization hashes in `round-sync.json`, scoped to the signed-in
account and server. `account-binding.json` retains the installation's account/server
binding across sign-out. Local round changes are compared with the last acknowledged content. A failed
or interrupted upload remains eligible for the next sync, including after restarting
the app. A lost response cannot create duplicate rounds.

Deleting a local round records its ID durably before removing it from history.
The next sync replaces its server payload with a retained deletion (`payload = NULL`).
Server deletions are applied to unchanged local copies. A local round with unsynced
changes takes precedence, including when both copies changed. Downloaded changes are
applied only if the local copy has not changed while the request was running. When
restoring an exported round with the same ID, the restored local copy can be uploaded
again. Explicit import clears the restored round's pending deletion and old sync
hashes; other deleted rounds remain deleted. Clearing app data or uninstalling is
a local reset and does not delete server data.

Do not delete `round-sync.json` to fix a sync error: it contains offline deletion
records. An unreadable sync file blocks sync and deletion while preserving local
reading and saving. Back up the local history before repairing storage.

## API contract

| Request | Result |
| --- | --- |
| `GET /api/rounds` | This account's records, including retained deletions |
| `GET /api/rounds/{id}` | This account's record, or 404 |
| `PUT /api/rounds/{id}` | Create/update/delete with a version precondition, or 409 |

A write body contains `version` (a new UUID), `expectedVersion` (the current UUID,
or null for creation), and `round` (the complete round, or null to delete). The result
contains `id`, `version` and `round`. Repeating an identical version/body is idempotent.
Invalid payloads return 400; request bodies are limited to 2 MiB. Date and round detail
fields stay inside JSONB rather than being converted into a different round model.
All requests require `Authorization: Bearer <accessToken>`; missing or invalid tokens
return 401. An ID owned by another account returns 404 for reads. Writes use the
authenticated identifier claim; adding an owner to the JSON cannot select another
account. Different accounts can save independent rounds with the same ID.

Controllers handle HTTP and call `ServerRoundService` in Application. Server storage
interfaces are database-independent; `GolfSG.Server.Infrastructure` implements them
with PostgreSQL, including the atomic version check. This component separation
preserves the response bodies, conflict behavior, and deletion records while applying
the authenticated account scope to every read and atomic write.

Migration 002 preserves older unowned server rows and deletions under `__legacy__`.
They remain inaccessible through signed-in requests and are not assigned to the
first account. Locally saved rounds upload as that account's owned history on its
first sync. A legacy deletion cannot delete an owned account record. This remains
a local development pilot; hosted rollout and a full account recovery/deletion
lifecycle require separate work.
