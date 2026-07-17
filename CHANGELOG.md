# Changelog

All notable changes to GolfSG are documented here.

The project follows semantic versioning where practical. Alpha releases may contain breaking changes to workflows, saved-data schemas, and UI behavior.

## [Unreleased]

- Continue hardening the alpha release and resolving known workflow issues.
- Improve public documentation, product discovery notes, and release procedures.

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
