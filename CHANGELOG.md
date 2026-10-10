# Changelog

All notable changes to GolfSG are documented here.

The project follows semantic versioning where practical. Alpha releases may contain breaking changes to workflows, saved-data schemas, and UI behavior.

## [Unreleased]

### Added

- Shot overview when starting or resuming a beta course round, with all positions numbered on the course picture and selected-shot details for names, whole-metre distances, starting lies, categories and played status. Choose remaining shots in any order; selected shots, SG, undo, resume and archived outcomes remain correctly matched.
- Beta course practice rounds using the supplied offline facility picture, draggable start/target markers, reference-distance calibration, measured distance overrides, saved course layouts and per-shot lies. Set 1–100 independent attempts per position, record each finishing distance/lie, holed outcome and penalties, undo, save/resume, and archive the completed round in separately labelled history. Partly played positions can be continued in any order; existing courses default to one attempt per position.
- Course-practice results remain separate from normal-round statistics and insights; saved result metadata survives round export/import.
- Undo the latest submitted practice putt and review/edit earlier results in training games, tour rounds, and benchmarks. Corrections retain shot order and update scores; edits from the completion screen update the existing saved result.
- Automatic local saving and resume for unfinished training games, tour rounds, and benchmarks, including the original distance order, recorded results, and current putt count.
- Continue-game actions on the start page and putting-games menu, with explicit discard before replacing an unfinished game.

### Changed

- Beta course score entry has a fixed Save and Next Shot action. Players can play the configured shots from one position, then enter results one by one; saving clears the fields and advances the attempt counter while keeping the form in place. Completing that position reveals the next remaining position. The final attempt has Save Last Shot, followed by a fixed Save Round Result action; keyboard safe areas and nearby errors keep these controls reachable. Overview and active-round progress count all attempts, and the play page previews the latest ten results.
- The shot overview lets players adjust attempt counts for the active round before recording a result at that position. Saved course defaults remain available for later rounds.
- Course maps show a compact whole-metre distance summary below the picture, distinguishing estimates from measured distances and updating during shot placement, selection and dragging without covering the image.
- Course setup opens directly on the picture after naming a course, ready to place the first shot. The name appears in the title and remains editable near Save Course, alongside the beta information.
- Course setup has one Save Shot and Continue action: it saves or updates the shot, dismisses the keyboard and returns to the picture, ready to place the next start point. Invalid input keeps the pending shot intact.
- Planned course-shot distances display as the nearest whole metre in setup, play and saved results, with half metres rounded up. Stored measurements, map geometry and SG calculations retain their precision.
- Course screens use larger dark text, outlined white fields and bold buttons with readable disabled states. Save Shot and Continue is a primary action; compact map controls and separate saved-shot details improve readability on narrow screens.
- Opening picture-based course rounds now shows a menu for playing saved courses or setting up a new one, with resume for unfinished rounds. Saved courses can be edited from the course list; saving setup returns to the menu or list.
- Creating a picture-based course asks for its name before opening setup, with required-name validation and cancellation.
- New picture-based courses use the bundled facility picture; custom picture selection and upload are unavailable.
- Saved beta course shots remain visible and can be dragged directly on the picture. Drag an endpoint to adjust distance or the line to move the whole shot; Android keeps the drag on the map while the page is scrollable elsewhere.
- Leaving a putting game offers save-and-close or confirmed discard instead of losing progress.
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
