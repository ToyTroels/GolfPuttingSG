# Release smoke tests

Run these journeys against the packaged application, not only a debugger build. Record platform, OS version, device, package hash, and pass/fail evidence. Stable selectors for the critical controls live in `src/GolfSG/Views/UiAutomationIds.cs` so these cases can be moved into Appium without relying on translated text.

## First launch and navigation

1. Install with no prior GolfSG data and launch offline.
2. Confirm Start, New Round, Putting Games, History, and Settings open once even after rapid repeated taps.
3. Confirm the empty-history state is understandable and the in-app privacy page opens.

## Round categories and guided entry

1. Start a nine-hole round with putting, approach, and around-the-green enabled.
2. Complete an approach-to-short-game handoff, a short-game-to-putting handoff, and a hole completed directly from each category.
3. Navigate backward, edit values, use undo, and confirm carried distances and summaries remain consistent.
4. Finish early, save, reopen the result, edit it, and verify there is exactly one history entry.
5. Open a hole from the overview, advance through several holes, then tap "Til oversigt". Confirm the round overview opens immediately. Repeat after using the previous-hole arrow, with rapid repeated taps, on the final hole, and with guided input disabled. Verify entered values are preserved.
6. Open hole 1, use the hole picker to jump to untouched hole 13, then open round settings. Confirm selecting nine holes is refused, hole 13 remains editable, and its subsequent input appears in the overview and survives resuming the round.

## Persistence and recovery

1. Force-close during an active round, relaunch, and resume it at the same state.
2. Abandon an active round and confirm it no longer resumes.
3. Save a round, regular game, and benchmark; confirm the default History view shows all three.
4. Filter and sort History, then return to the default all-items view.
5. Export data, delete or replace local data, import the export, and verify all entries.
6. Validate recovery from a corrupt active file and a valid backup.

## Putting-game recovery

1. Start a training game, tour round, randomized bell-curve benchmark, and ladder benchmark in turn. Record several results, adjust the current putt count, force-close, and relaunch. Resume from Start or Putting Games; verify the original distances/order, progress, current input, score, and benchmark metadata.
2. Use Save and Close, resume again, and verify the same state. Cancel a discard/replacement prompt and verify the saved game remains. Confirm discard and verify it no longer resumes.
3. Finish a resumed game; verify one history entry and no remaining resume action after relaunch. Repeat the final save and verify no duplicate history entry.
4. With storage unavailable, verify a save failure leaves the current putt and recorded results intact, offers retry, and does not report a successful save-and-close.

## Practice-result corrections

1. During training, tour, randomized bell-curve, and ladder sessions, submit several results, undo the latest, and confirm it becomes current with its recorded count. Resubmit and verify progress, distances/order, totals, and SG without duplicated results.
2. Open "Se og ret registrerede putts", edit an earlier count, and verify totals/SG update while the current putt input and shot order stay unchanged. Cancel an edit and verify nothing changes.
3. Relaunch after an undo or edit and resume; verify the corrected state. Finish the game, edit from the completion screen, and verify one history entry with the original date and benchmark metadata, with no new unfinished game.
4. Simulate storage failures during undo/edit and verify the earlier state remains visible and can be retried. Rapid repeated taps must not submit or correct multiple results unintentionally.

## Preferences and accessibility

1. Switch between meters and feet and verify entry, results, and history formatting while stored calculations remain consistent.
2. Exercise the primary journeys at the largest supported text size without clipped or unreachable controls.
3. With the platform screen reader enabled, verify control names, hints, state, focus order, and actionable elements.
4. Check contrast and focus visibility in every supported theme.
5. Check the overview, round settings, and hole picker on Android with gesture navigation and system navigation buttons. Confirm controls remain reachable above system bars and around display cutouts.

## Packaging and lifecycle

1. Confirm the package identifier, version, build number, icon, splash screen, and display name.
2. Install over the previous release and verify existing data remains available.
3. Confirm the app works without network access and does not request unrelated permissions.
4. Background, resume, rotate/resize where supported, and repeat the active-round save path.
5. Verify Android APK/AAB and Windows MSIX signing identities match the intended production identities.

Any crash, data loss, duplicate mutation, inaccessible critical action, debug signing identity, or unexplained scoring change blocks distribution.
