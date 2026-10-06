---
title: GolfSG Privacy Policy
---

# GolfSG Privacy Policy

Last updated: 2026-10-05

GolfSG is an offline-first golf training application. This policy describes the behavior of the current alpha version.

## Data stored by the app

GolfSG may store:

- Round and hole inputs.
- Putting-game and benchmark results.
- Dates, titles, scores, strokes-gained values, and summary statistics.
- App settings such as distance preferences and selected tracking options.
- Temporary active-round or active-session state.

This data is stored as JSON and preferences in the application's platform-managed local data directory. It is not uploaded to a GolfSG service.

## How data is used

GolfSG uses locally stored data to calculate scores and strokes-gained values, display round history, progress and benchmark results, resume an unfinished round, and remember your settings. These functions run on your device.

## Optional local usage diagnostics

Usage diagnostics are disabled by default and can be enabled in Settings. They store aggregate counters for button presses per completed hole, correction visits, distance input methods, configured categories, input flow, app version, and foreground sessions. No entered distances, scores, round identifiers, names, or location data are included in these summaries. Unexpected session endings may be crashes, force-stops, or operating-system termination and are not a confirmed crash rate.

Nothing is transmitted automatically. You can stop collection, export a summary through the system share sheet, or delete the local counters in Settings. Disabling collection preserves existing counters. Cached exported copies and copies you share must be deleted separately. These counters do not expire automatically.

## Security

GolfSG stores its data in the application's platform-managed private data directory and uses operating-system preferences for settings. Access protection relies on the operating system's app isolation and device security. GolfSG does not add its own encryption to its JSON files or exported files. When you export data, protection of the resulting copy depends on the destination you choose.

## Data not collected by GolfSG

The current alpha does not include:

- User accounts or sign-in.
- GolfSG cloud synchronization.
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

To remove all locally stored GolfSG data, including local recovery files and cached exports, use the platform's app-data reset or uninstall controls. Exported copies outside the app and platform-managed backups must be deleted separately. GolfSG has no user accounts or server-side round storage.

## Future changes

Cloud synchronization, accounts, telemetry, advertising, or third-party integrations must not be introduced without updating this policy, the in-app notice, and the relevant store declarations.

## Contact

Privacy questions and reproducible issues can be submitted through the public repository:
[GolfSG GitHub issues](https://github.com/ToyTroels/GolfPuttingSG/issues). Please do not include sensitive personal information in public issues.
