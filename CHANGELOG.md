# Changelog

All notable changes to GolfSG are documented here.

The project follows semantic versioning where practical. Alpha releases may contain breaking changes to workflows, saved-data schemas, and UI behavior.

## [Unreleased]

### Changed

- Android build number increased to 4 for the internal-testing startup fix.

### Fixed

- Disable Android Release marshal-method generation to avoid a missing native MAUI startup callback (`MauiApplication.n_onCreate`) observed in the Google Play build.

## [0.3.0-alpha] - 2026-10-06

### Added

- Collapsible hole-input sections with per-category scores and scrolling to the selected header.
- Centered approach distances with 1 m and 5 m adjustments and a popup for exact input.
- Optional local usage summaries with export and deletion controls.
- Per-category running scores in round status and 9/18-hole shortcuts in setup.

### Changed

- Approach and Around Green configuration is available through beta features.
- Approach distances support up to 300 metres; distance popup input is selected for replacement on Android.
- Approach controls use neutral buttons; green finish distances retain decimal precision.
- Around-green distances use matching exact-input controls, and configured starts collapse to an editable summary.
- Confirming a completed finish distance opens the next visible pane after a 350 ms pause with animated scrolling.
- Opening another hole expands its first visible input pane and scrolls to the top.
- Around Green is hidden when an approach finishes on Green or in the hole.

### Fixed

- Crash when an approach finishes on Kortklippet beyond the around-green range, including 41 m.
- Oversized and non-finite distance input handling and unstable finish-slider range updates.
- Tracking selection cannot deselect the final category.
- Removed duplicate final-hole overview buttons and improved tracking-switch visibility.
- Distance fields release focus on Done, and tapping outside dismisses the keyboard.
- Changing an approach finish lie refreshes distance and units together.

## [0.2.0-alpha] - Unreleased

### Fixed

- Starting a round immediately with approach and around-the-green tracking enabled.
- Default History visibility for saved rounds, games, and benchmarks.

### Added

- Stable accessibility and UI-automation identifiers for critical journeys.
- An in-app privacy statement and aligned platform privacy configuration.
- Reference-data scope, limitations, and a documented provenance/rights release gate.
- Locked dependency graphs, pinned SDK, cross-platform CI, Android AAB/APK validation, Windows MSIX packaging, and release/smoke checklists.

### Changed

- Replaced official-tour wording with general reference-baseline wording throughout the product.
- Disabled Android application backup and declared Apple preferences/encryption use.

### Release gates

- Production signing credentials and store identities are not stored in the repository.
- Public distribution requires reference-data provenance approval and physical-device/store acceptance.

## [0.1.0-alpha] - 2026-07-17

This is the first documented alpha release line. The corresponding GitHub Release should be created from the `v0.1.0-alpha` tag after the release build has been verified.

### Added

- Local putting rounds with configurable hole counts and early completion.
- Hole-level and round-level putting strokes-gained calculations.
- Guided hole entry with quick putt actions and editable distances.
- Putting training games and benchmark sessions, including ladder benchmarks.
- Local JSON history with editing, filtering, sorting, recovery, and import/export flows.
- Round insights and comparison-oriented history views.
- Core, application, infrastructure, and view-model test coverage for the main workflows.
- Centralized version and build-number management.

### Known limitations

- The application is alpha software and the UI and saved-data behavior may change.
- Approach and around-the-green workflows are still being expanded.
- Distance-unit personalization is not complete across every screen.
- Cloud synchronization and multi-device accounts are not available.
- The reference-data provenance and calculation methodology need fuller public documentation.
- UI automation and visual regression testing are planned but not yet complete.
