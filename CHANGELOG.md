# Changelog

All notable changes to GolfSG are documented here.

The project follows semantic versioning where practical. Alpha releases may contain breaking changes to workflows, saved-data schemas, and UI behavior.

## [Unreleased]

### Changed

- Continue development beyond the 0.2.0 alpha release candidate.

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
