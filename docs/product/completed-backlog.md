# Completed backlog archive

Archived on 2026-10-10 from the checked items in TODO.md. These preserve the recorded completion history; they are not a new device acceptance audit. Remaining work is tracked in [TODO.md](../../TODO.md).


## User-perspective improvement backlog

- [x] Implement beta picture-based course practice setup and play: saved layouts, calibration and measured overrides, draggable positions, lies/categories, configurable attempts per position, individual finishing outcomes/penalties, undo, autosave/resume and separately labelled history. Device acceptance remains in the active backlog.
- [x] Let players undo the latest submitted putt while a game is active and review/edit any recorded putt in training games, tour rounds, and benchmarks, including the completion screen. Corrections recalculate scores and persist to the active draft or the existing history entry.
- [x] Add durable autosave/resume for training games, tour rounds, and benchmarks, preserving exact distances, completed results, and the current putt count; require explicit discard before replacement. Automated recovery checks and Windows compilation pass; physical-device acceptance remains a release check.
- [x] Fix new-round setup so selecting approach and around-the-green tracking is immediate and does not crash the app.
- [x] Make guided hole entry the default for new installs so on-course input starts in the faster flow.
- [x] Collapse carried putting distances into a compact putting-step summary with an option to edit the distance manually.
- [x] Let putt quick-action buttons complete the guided putting step in one tap.
- [x] Add a visible undo action for completed around-green shots during multi-shot entry.
- [x] Add a first-class Insights dashboard on the start page using recent history.
- [x] Autosave active rounds, show a resume action on the start page, and require explicit abandon/delete for in-progress rounds.
- [x] Improve history with filters for round/game/benchmark type, date period, and best/worst SG sorting.
- [x] Add SG-category-specific history filtering/sorting and round comparison views.
- [x] Fix the history tab so previously saved rounds are visible and not hidden by filters, loading errors, or round/game type grouping.
- [x] Let players open old rounds from the start screen to view detailed results and edit the saved round.

## Local API and saved-round synchronization

- [x] Add a PostgreSQL products API that uses the existing database and table without startup schema changes.
- [x] Connect the Android Debug app to the HTTPS API and document USB port forwarding.
- [x] Add manual saved-round synchronization with durable local sync state, version checks, retry handling, and deletion tombstones.
- [x] Configure local IIS publishing, private database settings, and the HTTPS certificate; verify product reads and saved-round synchronization with the physical Android phone.

## Architecture follow-up

- [x] Extract a plain .NET application layer for testable use cases that currently live inside MAUI view models.
- [x] Move round creation, edit, validation, save, and summary orchestration out of `RoundInputViewModel` into focused application services.
- [x] Move putting-game session progression and save orchestration out of `PuttingGameViewModel` while keeping UI state and formatting in the view model.
- [x] Replace linked MAUI source files in `GolfSG.Tests.csproj` with direct references to testable Core/Application projects.
- [x] Introduce reusable async command/navigation guards for save, submit, delete, import/export, and page navigation actions.
- [x] Decide whether `RoundFileStore` should remain in `GolfSG.Core` or move to an infrastructure project if storage grows beyond local JSON.
- [x] Split large C# page classes where interaction handlers, layout construction, and reusable UI components can be separated without changing behavior.
- [x] Keep all strokes-gained calculations, summary rules, and benchmark rules centralized in Core/Application so pages never duplicate business rules.

## Structural cleanup

- [x] Standardize namespaces for Application-owned code under `GolfSG.Application.*`, including view models, services, and workflows.
- [x] Split `HoleEntryPage` into smaller layout/components and focused interaction flows without changing behavior.
- [x] Split `HoleInputViewModel` into focused state/coordination components while keeping the public UI contract stable.
- [x] Extract more reusable MAUI controls from the largest code-built pages where this improves readability and testability.
- [x] Review and remove empty folders, stale project-user files, and other repository artifacts that no longer serve a purpose.
- [x] Organize tests by architectural area—Core, Application, Infrastructure, and UI/view-model workflows—as the test suite continues to grow.
- [x] Run the architecture boundary tests and the full test suite after each structural refactor.

## Rename project to GolfSG

- [x] Rename solution, project folders, project files, namespaces, MAUI metadata, project references, platform identifiers, and README references to GolfSG.
- [x] Restore, build, and test after the rename.

## Round length and early finish

- [x] Let the player configure the number of holes in a round before or during play, such as 9, 18, or a custom number.
- [x] Let the player finish/end a round before every configured hole has been completed.
- [x] Persist whether a saved round was completed normally or ended early.
- [x] Make result views and SG summaries handle partial rounds without treating missing holes as zero-value holes.
- [x] Add tests for custom hole counts, early round finish, and saving partial rounds.

## Round persistence hardening

- [x] Write saved rounds through a temporary file before replacing `rounds.json`.
- [x] Keep a `rounds.json.bak` backup before overwriting the active history file.
- [x] Fall back to the backup file if the active history file is empty, malformed, or unreadable.
- [x] Add repository tests for atomic writes, backup fallback, corrupt JSON recovery, empty file recovery, and preserving multiple rounds after save/delete.
- [x] Add a user-facing warning if saved rounds could only be recovered from backup.
- [x] Add a diagnostic view or export action that shows the active storage path and total saved rounds.
- [x] Add migration support for old app identifiers or storage folders, especially if `ApplicationId`, package name, or project metadata changes.
- [x] Consider storing a schema/version wrapper around saved rounds so future persistence changes can migrate safely.
- [x] Add an optional manual export/import flow for `rounds.json` so users can recover history across installs, devices, or app renames.
- [x] Investigate whether any save flow can overwrite history with a single round after app restart, failed deserialization, or app-data path changes.

## Improve round putting distance input

- [x] Let players navigate back and adjust exact distances with the slider without snapping back to predetermined reference distances.
- [x] Redesign the first-putt distance input in round entry so decimal distances are easy to enter, review, and adjust on mobile.
- [x] Consider quick-pick chips or a stepper/slider for common putting distances, while still allowing precise manual input.
- [x] Make decimal comma and decimal point input behave consistently across device cultures.
- [x] Keep the entered value, display text, saved value, and edit-loaded value aligned.
- [x] Add view-model tests for decimal input, culture-specific separators, manual edits, and saved-round edit reloads.

## Improve approach distance input

- [x] Allow approach start, end, and green-edge distances to accept decimal values instead of whole numbers only.
- [x] Make decimal comma and decimal point input behave consistently across device cultures.
- [x] Use mobile-friendly input controls for approach distances, such as numeric keyboard, quick-adjust buttons, or common-distance presets while still allowing precise manual input.
- [x] Keep entered decimal values, display formatting, saved values, edit reloads, and SG calculations aligned.
- [x] Add validation that catches invalid or negative distances without clearing the player's in-progress input.
- [x] Add tests for decimal approach input parsing, culture-specific separators, save/load round-tripping, and SG calculation with decimal distances.

## Reference tables by lie

- [x] Add lie-based expected-shots listings to reference tables where the baseline depends on lie, including approach and around-the-green tables.
- [x] Show distance, lie, and expected shots in a scan-friendly table layout.
- [x] Keep the displayed units consistent with the app distance setting once configurable units are implemented.
- [x] Add tests that verify exposed reference rows match the calculation baselines for each lie.

## Improve putting-game screen usability

- [x] Redesign the active putting-game screen so the current putt, putts-used controls, running score, and primary action are easier to scan during practice.
- [x] Replace the long inline "remaining distances" text with a compact preview, such as next 3-5 distances, a progress indicator, and an expandable full list.
- [x] Keep the primary submit/save action visible and reachable without being pushed below the viewport.
- [x] Make the running total section more compact, with clearer separation between current result, total putts, and total putting SG.
- [x] Improve mobile spacing and typography so the distance card does not dominate the screen at the expense of actions and progress.
- [x] Add empty/complete states that clearly show when the benchmark is finished and what to do next.
- [x] Add UI tests or view-model tests for progress text, remaining-distance preview, and completion behavior.

## Add ladder benchmark type

- [x] Keep the current benchmark type as the bell-curve/distribution benchmark and make that type explicit in benchmark naming, metadata, and UI copy.
- [x] Add a ladder benchmark type where the player hits a configurable number of putts from each distance before moving to the next longer distance.
- [x] Define ladder benchmark presets, including start distance, end distance, distance step, putts per distance, total attempts, preset ID, and preset version.
- [x] Generate ladder benchmark distances in ascending order from the preset definition instead of using the bell-curve benchmark distance arrays.
- [x] Let the benchmark setup UI choose between bell-curve benchmark and ladder benchmark, then choose the relevant preset/options for that benchmark type.
- [x] Save the benchmark type and ladder preset metadata with round history so ladder attempts can be filtered and compared separately from bell-curve benchmark attempts.
- [x] Update result and history titles/summaries so ladder benchmark attempts show the distance range, step, and putts per distance.
- [x] Add tests for ladder benchmark distance generation, metadata persistence, setup flow, scoring, and saved-history display.

## Putting games menu

- [x] Replace separate landing-page putting game and tour-round buttons with one putting-games entry.
- [x] Add a putting-games list with training game, tour round, and benchmark entries.
- [x] Move tour round under the putting-games list.
- [x] Remove the benchmark/test button from the training-game setup screen.
- [x] Decouple benchmark selection from the training-game setup screen so benchmark opens directly from the games list.
