# Optional local usage diagnostics

Enable **Indstillinger → Data → Lokal brugsstatistik**. Disabled by default; no network collection or analytics SDK. Export opens the platform share sheet only when requested. Deleting the summary leaves rounds intact; disabling stops collection and preserves existing counters.

Reports are grouped by app version/build, guided/collapsible input, and configured tracking categories. They include:

- Completed holes and button presses leading to completion; their ratio is average presses per completed hole.
- Correction visits and presses during those visits. A correction changes a previously completed hole; merely reopening it does not count.
- Input-method activations: distance presets, steppers, popup editing, inline editing, and other buttons.
- Foreground sessions and unexpected endings detected on the next launch. Unexpected endings can be crashes, force-stops, or operating-system termination. This is **not** a crash-free session rate.

Counts measure button presses and input activation, not keyboard keys, scrolling, or every physical screen tap. Cancelled presses can count. No distances, scores, round IDs, names, locations, or individual event histories are written into reports. Temporary hole fingerprints are held only in memory to detect actual corrections; hole deduplication lasts for the app process. Returning to a saved completed hole establishes a baseline rather than counting it as a new completion.

The summary is saved locally after completed visits and lifecycle changes. Storage failures are ignored by collection and cannot block round saving. The export action reports errors to the user. No input-time metric is collected because play and idle time would confound it.

## Phone verification before release

1. With collection off, enter a hole and confirm no usage file is created.
2. Enable collection. Complete a putting-only hole using presets and quick putt buttons; export and check its cohort, completion count, and input-method counters.
3. Repeat in collapsible mode with Approach and Around Green configured. Verify its cohort is separate.
4. Reopen a completed hole without changes: no correction. Change putts and leave: one correction. Multi-shot holes must behave the same way.
5. Cancel the distance popup: input remains unchanged. Confirm popup activation still counts.
6. Background and resume the app: sessions increase without unexpected endings. Force-stop and relaunch: an unexpected ending is recorded, not labelled a confirmed crash.
7. Disable collection: counters stop. Delete the usage summary: round data remains available.

Unit tests cover completion versus incomplete visits, corrections, repeated unchanged visits, and structurally equivalent shot lists. Native event coverage, selection, sharing, and lifecycle behavior require phone testing.
