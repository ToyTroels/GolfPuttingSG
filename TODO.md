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

## Save rounds

- [ ] Decide how rounds should be saved locally, including storage format, file location, and whether the app should support future migration.
- [ ] Add a round history model that stores round date, course name if available, hole inputs, calculated SG values, and completion status.
- [ ] Save a round when the player finishes or explicitly chooses to save it.
- [ ] Load saved rounds on app start so they can be shown in previous rounds and used by statistics/evaluations.
- [ ] Support editing or deleting a saved round without corrupting other saved rounds.
- [ ] Handle save/load failures with a clear user-facing state and avoid losing the active round.
- [ ] Add tests for round serialization, loading multiple saved rounds, editing, deleting, and malformed saved data.

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
