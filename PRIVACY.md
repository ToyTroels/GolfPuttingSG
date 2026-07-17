# Privacy

Last updated: 2026-07-17

GolfSG is an offline-first golf training application. This document describes the behavior of the current alpha version.

## Data stored by the app

GolfSG may store:

- Round and hole inputs.
- Putting-game and benchmark results.
- Dates, titles, scores, strokes-gained values, and summary statistics.
- App settings such as distance preferences and selected tracking options.
- Temporary active-round or active-session state when those workflows use it.

This data is stored in the application's local data directory using JSON files. The exact directory is selected by the platform and is not intended to be a shared public folder.

## Data not currently collected

The current alpha does not include:

- User accounts or sign-in.
- Cloud synchronization.
- Advertising.
- A hosted analytics service.
- Sale or sharing of personal data.

The app's core scoring and history features are designed to work without network access.

## Export and deletion

Where the app exposes import/export, exported files are created by the user and are under the user's control. Exported files may contain complete round history and should be handled like any other personal data.

To remove locally stored GolfSG data, use the platform's app-data reset/uninstall controls. This permanently removes local history unless the user has exported or backed it up elsewhere.

## Future changes

Cloud synchronization, accounts, telemetry, or third-party integrations would change this policy and should not be introduced without updating this document and providing an appropriate user-facing notice.

## Contact

For privacy questions, open an issue in the repository or contact the project maintainer through the repository's published contact channel.
