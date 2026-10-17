# Active TODO

Remaining implementation and verification work. Exploratory features live in [project ideas](docs/product/ideas.md); recorded completions live in the [completed backlog archive](docs/product/completed-backlog.md). The [roadmap](docs/product/roadmap.md) describes direction, and the [release checklist](docs/RELEASE_CHECKLIST.md) tracks distribution gates.

## Round entry and usability

- [ ] Allow users to enter and edit their own round and practice results in the web browser, with validated inputs, secure owner-only saving, and cache invalidation after successful writes.

- [ ] Add course name at round setup and preserve it through autosave/resume, history, editing, import/export, results, and summaries. Older rounds may omit it.
- [ ] Add an in-round scoreboard showing registered holes, unfinished holes, and running SG by configured category, with entered values preserved when returning to hole entry.
- [ ] Fix history date contrast on supported themes/devices.
- [ ] Finish meters/feet consistency across putting entry, games, practice, reference views, summaries, and statistics buckets. The preference already exists; keep internal values normalized and verify conversions and formatting.
- [ ] Complete accessibility and language polish: semantic labels, focus, large text, touch targets, visible actions, and consistent Danish/English terminology.

## Practice and benchmarks

- [ ] Optimize and improve Banerunde på billede: review the setup, editing, shot overview, and result-entry flows on real devices; make zoom, marker dragging, shot selection, calibration, and navigation easier to use. Measure and improve image loading, rendering responsiveness, and memory use for larger pictures and courses. Improve readability, touch targets, progress feedback, and save/import/share feedback while preserving offline play, saved layouts, and resumable rounds. Use player feedback and device measurements to prioritize changes.
- [ ] Validate beta course setup/play on physical Android and Windows devices: offline picture rendering, map drag versus scroll, calibration, large text, autosave/resume, repeated-result entry, failed final save, and history. See [the beta guide](docs/product/course-practice-beta.md).
- [ ] Add benchmark 1-, 2-, and 3-putt quick actions consistent with regular putting games.

## Shared courses, games, and challenges

- [x] Save and share a picture-based course round (banerunde på billede) as a `.golfsgcourse` file containing its picture and complete setup; import a reusable offline copy without player results. Repeated imports preserve local edits, and changed shared files create separate copies. See [the beta guide](docs/product/course-practice-beta.md#save-and-share-a-course) for behavior and physical-device acceptance checks.
- [ ] Let a user set up a game or challenge that other users can join. Support choosing a saved picture-based course or defining practice shots, setting common rules and attempt counts, inviting players or sharing a join link/code, recording each participant's results, and comparing scores. Define scoring, ties, joining/closing participation, and changes after play begins; preserve offline result entry and sync when connected.

## Scoring and shot tracking

- [ ] Add picture-based position entry during a round (beta): let the player mark the ball and pin on a calibrated course picture, with green boundaries defined during course setup. Calculate distance to the green edge and available green along the shot line, including room between the near edge and pin and beyond the pin, to identify short-sided shots. Use this context for approach/around-green classification and evaluate a validated SG adjustment for available green; establish reference data before changing expected strokes. Preserve positions, boundaries and measurements through autosave/resume and history, with manual overrides when the picture is inaccurate.
- [ ] Identify and finish gaps in complete approach/around-green shot sequences, including start/end distance and lie, holed outcomes, review/editing, and persistence. Reuse existing carry-forward logic and preserve manual overrides; verify remaining end-to-end handoffs rather than reimplementing them.
- [ ] Review remaining legacy distance-only approach SG paths. Prefer the existing shot-level calculation, preserve category boundaries and penalties, and keep older round compatibility explicit.
- [ ] Finish category configuration behavior for mid-round changes: preserve entered data, define inclusion in summaries, restore hidden input safely, and verify settings/save/load/evaluation consistency. Setup switches already exist.
- [ ] Extend expected birdies to distinguish eagle putts and early green hits, recording hole par and strokes to reach the green. Cover normal GIR, early green hits, eagle results, and older rounds without implying missing data.
- [ ] Add SemiRough as a stored/displayed lie across inputs, mapping, summaries, carry-forward, and reference views. Initially reuse Rough baselines through an isolated mapping; verify older-round compatibility.

## Insights and statistics

- [ ] Integrate remaining SG evaluation period/category drilldown into Insights, retaining existing preset periods, summaries, and empty states; add custom date ranges.
- [ ] Add trend charts for total/category SG, putting distance bands, benchmark scores, three-putt rate, and make percentage. Reuse existing statistics and make-percentage calculations.
- [ ] Improve round results with top gained/lost holes, category contribution, and specific next-practice recommendations.
- [ ] Add shareable round/practice summaries suitable for coaching and personal records, beyond raw history-file export.

## Security

- [ ] Address the prioritized findings, hardening recommendations, and verification gaps in the [security TODO](docs/SECURITY_TODO.md). The review covers admin commit `2105d02` against `dev` base `44d3975`; deployment-dependent risks still need live verification.

## Technical follow-up

- [ ] Verify saved-round sync recovery on the physical Android phone: save offline, restart the app, reconnect and sync, then repeat sync without duplicates. Include offline edits and deletions before expanding beyond the local pilot.
- [ ] Finish extracting remaining history/evaluation/result repository queries and aggregation into application query services.
- [ ] Register query-service interfaces and inject them into consumers rather than constructing services in view models.
- [ ] Add focused query-contract coverage for missing rounds, tracking fallback, recovery metadata, and benchmark/statistics aggregation where not already covered.
- [ ] Audit remaining unguarded navigation/save/submit/import/delete actions using the existing shared guards; make busy/error states consistent and verify repeated taps and failure behavior.
- [ ] Make storage schema-version handling explicit for supported, missing, and future versions, preserving legacy import compatibility.

## Release readiness

- [ ] Complete applicable physical-device smoke and accessibility checks, reference-data provenance approval, production signing, and store metadata using the [release checklist](docs/RELEASE_CHECKLIST.md).
