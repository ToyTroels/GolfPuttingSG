---
title: GolfSG Privacy Policy
---

# GolfSG Privacy Policy

Last updated: 2026-10-11

GolfSG is an offline-first golf training application. This policy describes the behavior of the current alpha version.

## Data stored by the app

GolfSG may store:

- Round and hole inputs.
- Putting-game and benchmark results.
- Dates, titles, scores, strokes-gained values, and summary statistics.
- App settings such as distance preferences and selected tracking options.
- Temporary active-round or active-session state.
- The configured server address, account ID/username, and the installation's sync-account binding.
- Access and refresh tokens for optional server sign-in, in platform secure storage.

Round data, settings and account binding are stored as JSON and preferences in the application's platform-managed local data directory. Authentication tokens use platform secure storage. Saved round history is also sent to the configured HTTPS server when you choose **Sync** in History. Saving a round does not automatically upload it.

## How data is used

GolfSG uses locally stored data to calculate scores and strokes-gained values, display round history, progress and benchmark results, resume an unfinished round, and remember your settings. These functions run on your device.

## Optional local usage diagnostics

Usage diagnostics are disabled by default and can be enabled in Settings. They store aggregate counters for button presses per completed hole, correction visits, distance input methods, configured categories, input flow, app version, and foreground sessions. No entered distances, scores, round identifiers, names, or location data are included in these summaries. Unexpected session endings may be crashes, force-stops, or operating-system termination and are not a confirmed crash rate.

Nothing is transmitted automatically. You can stop collection, export a summary through the system share sheet, or delete the local counters in Settings. Disabling collection preserves existing counters. Cached exported copies and copies you share must be deleted separately. These counters do not expire automatically.

## Manual round synchronization

The server address is configured under **Indstillinger → Konto og server**. Open **Konto og login** on the start screen to sign in or create an account. Creating an account sends a username and password over HTTPS to the configured server. The server stores an account ID, username, password hash, and Identity security/lockout metadata. It does not require an email address. The app does not persist your password and clears both password fields after sign-in/signup attempts or leaving the login screen. It stores the resulting access and refresh tokens in platform secure storage.

Choosing Sync in History while signed in sends saved rounds, hole and shot data, dates, scores, game and benchmark details, and completed course-practice results to that server. It also downloads that account's saved rounds and applies its deletions. Active unfinished sessions, course setup and image files, preferences and usage diagnostics are not uploaded. Rounds remain available offline, and interrupted sync can be retried.

The development API separates round history by the authenticated account. The first successful sign-in binds an installation's sync history to that account and server. Signing out removes saved tokens while retaining local history and that binding. Another account or server requires a separate installation. Previous unowned server records are preserved separately and are not automatically assigned to new accounts. The temporary Products diagnostic feature contains shared test records among signed-in users.

The server operator controls access to the database and its backups. Local HTTPS development certificates are accepted only for supported local addresses in Debug builds; Release builds require a normally trusted certificate. The current account system is a local development pilot, with no email verification or password recovery.

## Security

GolfSG stores its data in the application's platform-managed private data directory and uses operating-system preferences for settings. Access protection relies on the operating system's app isolation and device security. GolfSG does not add its own encryption to its JSON files or exported files. When you export data, protection of the resulting copy depends on the destination you choose.

## Data not collected by GolfSG

The current alpha does not include:

- Advertising.
- Hosted analytics or crash-reporting services.
- Sale or sharing of personal data.

The app's scoring and history features work without network access.

## Platform backups

GolfSG disables Android cloud backup for its application data. Operating systems, device manufacturers, enterprise administrators, or user-controlled device-transfer tools may still back up or transfer application data outside GolfSG's control. Those platform services are governed by the user's platform account and settings, not by GolfSG.

## Retention, export and deletion

An export is created only when the user requests it and may contain the complete round history. The destination and any later sharing are controlled by the user.

Saved rounds have no automatic expiry and remain in the active history until you delete or replace them. Settings remain until changed or the app's data is cleared.

Deleting an individual round removes it from the active history. The app keeps a previous version of the history as a local recovery backup, which may still contain deleted rounds until that backup is replaced. Preserved recovery files and cached exports may also contain older data and have no app-managed expiry. These local recovery copies are separate from Android cloud backup.

When you synchronize after deleting a round, its saved content is removed from the server's active round table. The server retains the owning account ID, round ID, version and deletion timestamp so another device does not restore the deleted record. Server backups can retain older content until the operator removes them.

Signing out removes the app's saved tokens and does not delete your account, local history or server history. Accounts and server records have no automatic expiry. This pilot has no account-deletion endpoint or password recovery. To remove server account data, contact the operator of your configured development server; server backups must be handled separately.

To remove all locally stored GolfSG data, including local recovery files and cached exports, use the platform's app-data reset or uninstall controls. This does not send deletions to the server. Exported copies outside the app, platform-managed backups and server backups must be deleted separately.

## Future changes

Public hosting, additional account features, telemetry, advertising, or third-party integrations require corresponding updates to this policy, the in-app notice, and relevant store declarations.

## Contact

Privacy questions and reproducible issues can be submitted through the public repository:
[GolfSG GitHub issues](https://github.com/ToyTroels/GolfPuttingSG/issues). Please do not include sensitive personal information in public issues.
