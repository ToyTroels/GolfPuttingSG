# TODO

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
