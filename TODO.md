# TODO

## User-perspective improvement backlog

- [ ] Fix new-round setup so selecting approach and around-the-green tracking is immediate and does not crash the app.
- [ ] Modernize the benchmark game UI so 1-, 2-, and 3-putt actions can be tapped on screen like the regular putting game.
- [ ] Let benchmark players review entered putts in an overview and edit previous benchmark putt results before saving.
- [x] Make guided hole entry the default for new installs so on-course input starts in the faster flow.
- [x] Collapse carried putting distances into a compact putting-step summary with an option to edit the distance manually.
- [x] Let putt quick-action buttons complete the guided putting step in one tap.
- [x] Add a visible undo action for completed around-green shots during multi-shot entry.
- [x] Add a first-class Insights dashboard on the start page using recent history.
- [ ] Fold remaining `SG evaluering beta` period/category drilldown into the start-page insights experience.
- [ ] Add trend charts for total SG, category SG, putting SG by distance band, benchmark scores, 3-putt rate, and make percentage.
- [ ] Upgrade round results from static notes into actionable coaching, including top gained/lost holes, category contribution, and next-practice recommendations.
- [ ] Autosave active rounds and putting games, show a resume action on the start page, and require explicit abandon/delete for in-progress work.
- [x] Improve history with filters for round/game/benchmark type, date period, and best/worst SG sorting.
- [x] Add SG-category-specific history filtering/sorting and round comparison views.
- [ ] Fix the history tab so previously saved rounds are visible and not hidden by filters, loading errors, or round/game type grouping.
- [ ] Fix history row date styling so the date text is visible against the row background.
- [x] Let players open old rounds from the start screen to view detailed results and edit the saved round.
- [ ] Add personal baseline options such as PGA Tour, scratch, handicap ranges, and the player's own recent average.
- [ ] Expand putting practice into a feedback loop with recommended drills, repeat-weak-distance sessions, goals, personal records, and streaks.
- [ ] Complete distance-unit personalization so putting and practice can be entered/displayed in meters or feet consistently.
- [ ] Add an accessibility and polish pass with semantic labels, larger touch targets where needed, visible alternatives to hidden gestures, and consistent Danish/English terminology.
- [ ] Add share/export-friendly round and practice summaries for coaching conversations or personal records.

## Architecture follow-up

- [x] Extract a plain .NET application layer for testable use cases that currently live inside MAUI view models.
- [x] Move round creation, edit, validation, save, and summary orchestration out of `RoundInputViewModel` into focused application services.
- [x] Move putting-game session progression and save orchestration out of `PuttingGameViewModel` while keeping UI state and formatting in the view model.
- [x] Replace linked MAUI source files in `GolfSG.Tests.csproj` with direct references to testable Core/Application projects.
- [x] Introduce reusable async command/navigation guards for save, submit, delete, import/export, and page navigation actions.
- [x] Decide whether `RoundFileStore` should remain in `GolfSG.Core` or move to an infrastructure project if storage grows beyond local JSON.
- [x] Split large C# page classes where interaction handlers, layout construction, and reusable UI components can be separated without changing behavior.
- [x] Keep all strokes-gained calculations, summary rules, and benchmark rules centralized in Core/Application so pages never duplicate business rules.

## Rename project to GolfSG

- [x] Rename solution, project folders, project files, namespaces, MAUI metadata, project references, platform identifiers, and README references to GolfSG.
- [x] Restore, build, and test after the rename.

## Periodic SG evaluations

- [ ] Add an evaluation flow where the player can choose a period to evaluate, such as last 7 days, last 30 days, season to date, or a custom date range.
- [ ] Let the player choose which SG category to evaluate:
  - Putting
  - Approach
- [ ] Filter saved rounds by the selected period.
- [ ] Calculate total SG, average SG per round, best/worst round, and trend for the selected SG category.
- [ ] Show the number of rounds included and clearly handle periods with no saved rounds.
- [ ] Add a summary view for the selected period and category.
- [ ] Decide whether periodic evaluations should live on the start page, result page, or a dedicated evaluation/statistics page.
- [ ] Add tests for date filtering and SG aggregation across multiple rounds.

## Approach SG calculation

- [ ] Rework approach strokes gained so it follows shot-level SG: `expected strokes before shot - 1 - expected strokes after shot`.
- [ ] Do not calculate approach SG as only `expected approach strokes - approach shots` unless the entered data represents a complete same-category approach sequence and the final expected state is handled.
- [ ] Preserve shot-category boundaries so tee shots, approach shots, around-the-green shots, penalties, and putts are not mixed into the wrong SG category.
- [ ] Decide what extra input is needed for the final state after the approach shot, such as remaining putt distance, around-the-green lie, bunker, rough, or penalty.
- [ ] Add tests for single approach shots, multiple approach shots, penalties, and category transitions into putting or short game.

## Configure SG categories

- [ ] Add UI for configuring whether SG approach and SG around the green are tracked for a round.
- [ ] Add the available SG categories to the settings screen, including putting, approach, and around the green.
- [ ] Define what happens when a player toggles an SG category off during an active round, including whether entered shot data is preserved, hidden, excluded from summaries, restored if toggled back on, and warned about before saving.
- [ ] Persist the selected SG category configuration with the active round and saved round history.
- [ ] Show or hide approach and around-the-green inputs based on the selected configuration.
- [ ] Keep SG summaries, result views, and periodic evaluations aligned with the enabled categories.
- [ ] Add tests for category configuration persistence and conditional input/result behavior.

## Save rounds

- [ ] Decide how rounds should be saved locally, including storage format, file location, and whether the app should support future migration.
- [ ] Add a round history model that stores round date, course name if available, hole inputs, calculated SG values, and completion status.
- [ ] Save a round when the player finishes or explicitly chooses to save it.
- [ ] Load saved rounds on app start so they can be shown in previous rounds and used by statistics/evaluations.
- [ ] Show the round date in history entries so saved rounds can be identified by when they were played.
- [ ] Support editing or deleting a saved round without corrupting other saved rounds.
- [ ] Handle save/load failures with a clear user-facing state and avoid losing the active round.
- [ ] Add tests for round serialization, loading multiple saved rounds, editing, deleting, and malformed saved data.

## Round length and early finish

- [x] Let the player configure the number of holes in a round before or during play, such as 9, 18, or a custom number.
- [x] Let the player finish/end a round before every configured hole has been completed.
- [x] Persist whether a saved round was completed normally or ended early.
- [x] Make result views and SG summaries handle partial rounds without treating missing holes as zero-value holes.
- [x] Add tests for custom hole counts, early round finish, and saving partial rounds.

## Carry remaining distances between SG categories

- [ ] When an approach shot misses the green, carry the entered remaining distance to the hole forward into SG around-the-green automatically.
- [ ] When a shot finishes on the green, carry the entered distance to the hole forward into the putting input automatically.
- [ ] For putting after a carried green distance, only ask the player to enter the number of putts used.
- [ ] Preserve the carried distances when saving, loading, editing, and calculating SG for a round.
- [ ] Add tests for approach-to-around-the-green handoff, approach-to-putting handoff, and putting input with only used putts entered.

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

## Prevent duplicate navigation and repeated actions

- [ ] Add shared double-tap protection for navigation buttons so fast repeated taps cannot push the same page multiple times.
- [ ] Apply the guard to start-page actions, round list items, hole cards, next/previous hole buttons, edit buttons, save/finish buttons, settings buttons, and putting-game submit actions.
- [ ] Disable or show busy state on buttons while navigation, save, delete, import/export, or submit actions are in progress.
- [ ] Prefer a reusable `AsyncCommand`/`AsyncActionButton` or navigation gate instead of page-by-page boolean flags.
- [ ] Add view-model or UI-flow tests where practical for repeated save/submit/navigation taps.

## Made percentage by first-putt distance

- [ ] Track made percentage for first putts grouped by distance bands.
- [ ] Define the distance bands to use, preferably matching the existing putting distance buckets where possible.
- [ ] Count a made first putt when `Putts == 1` for a hole with a valid first-putt distance.
- [ ] For each distance band, show attempts, made putts, made percentage, average putts, and SG putting.
- [ ] Add the made percentage stats to the round result view and/or a longer-term statistics view.
- [ ] Handle empty distance bands without showing misleading percentages.
- [ ] Add tests for made percentage calculations, distance-band boundaries, and unfinished holes.

## Configure distances in feet

- [ ] Add a setting that lets the player choose whether putting distances are entered and displayed in meters or feet.
- [ ] Update distance input controls, labels, summaries, and result views to use the selected unit.
- [ ] Convert feet to meters internally if the SG baseline calculations continue to use meters.
- [ ] Decide whether saved rounds store the normalized meter value only or also preserve the unit used at entry time.
- [ ] Update putting distance bands and made percentage by distance to respect the selected unit.
- [ ] Add tests for feet-to-meters conversion, display formatting, and SG calculations using feet input.

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

- [ ] Add lie-based expected-shots listings to reference tables where the baseline depends on lie, including approach and around-the-green tables.
- [ ] Show distance, lie, and expected shots in a scan-friendly table layout.
- [ ] Keep the displayed units consistent with the app distance setting once configurable units are implemented.
- [ ] Add tests that verify exposed reference rows match the calculation baselines for each lie.

## Add semi-rough lie type

- [ ] Add `SemiRough` as a distinct `ShotLie` value instead of grouping every non-fairway lie into `Rough`.
- [ ] Add semi-rough labels/parsing to shot input, summaries, carry-forward logic, and reference views.
- [ ] Initially calculate semi-rough using the existing `Rough` SG baseline for approach shots, while preserving `SemiRough` as the stored/displayed lie.
- [ ] Initially calculate semi-rough around the green using the existing `Rough` SG baseline, while preserving `SemiRough` as the stored/displayed lie.
- [ ] Keep the baseline mapping isolated so semi-rough can get its own expected-strokes table later without changing saved round data.
- [ ] Keep saved round compatibility in mind so older rounds without semi-rough still load correctly.
- [ ] Add tests for label round-tripping, shot input mapping, SG baseline lookup, saved round loading, and reference table display.

## Improve putting-game screen usability

- [x] Redesign the active putting-game screen so the current putt, putts-used controls, running score, and primary action are easier to scan during practice.
- [x] Replace the long inline "remaining distances" text with a compact preview, such as next 3-5 distances, a progress indicator, and an expandable full list.
- [x] Keep the primary submit/save action visible and reachable without being pushed below the viewport.
- [x] Make the running total section more compact, with clearer separation between current result, total putts, and total putting SG.
- [x] Improve mobile spacing and typography so the distance card does not dominate the screen at the expense of actions and progress.
- [x] Add empty/complete states that clearly show when the benchmark is finished and what to do next.
- [x] Add UI tests or view-model tests for progress text, remaining-distance preview, and completion behavior.
- [ ] Let the player change the number of putts in a regular training game after the game has started, including deciding how to add/remove remaining distances without corrupting already entered putts.

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
