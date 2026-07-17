# User stories

These stories describe user outcomes rather than implementation tasks. Statuses are intentionally broad and should be updated as product behavior changes.

## Alpha journeys

### Record a putting round — Current

As a golfer, I want to record my first-putt distance and putts used for each hole so that I can review my putting performance after a round.

Acceptance criteria:

- I can start a new round and choose its length.
- I can enter the required putting data without leaving the round flow.
- I can finish early and clearly see that the round is partial.
- The saved result distinguishes completed holes from missing holes.

### Understand the result — Current

As a golfer, I want to see strokes gained per hole and for the complete round so that I can identify unusually good and bad performance.

Acceptance criteria:

- The result shows total SG and useful supporting statistics.
- A hole with incomplete input is not silently treated as a zero-value result.
- The meaning of positive and negative results is explained in the UI or documentation.

### Practice from a controlled distance — Current

As a golfer, I want to run repeated putting attempts from known distances so that I can compare practice sessions consistently.

Acceptance criteria:

- I can choose a training or benchmark format.
- The session shows progress and the current attempt clearly.
- I can complete and save the session without losing earlier attempts.

### Review previous work — Current

As a golfer, I want to browse, filter, sort, edit, and reopen history so that I can compare current performance with earlier sessions.

Acceptance criteria:

- Saved rounds and games remain visible after restarting the app.
- Filters and sorting do not hide valid history unexpectedly.
- Editing one item does not duplicate or overwrite unrelated history.

### Protect my history — Current

As a golfer, I want my local history to survive malformed files or interrupted writes so that a single storage problem does not erase my progress.

Acceptance criteria:

- Writes use a safe replacement strategy and a backup where appropriate.
- A recoverable failure produces a clear warning.
- I can export history before changing devices or reinstalling.

## Next product outcomes

### Get a practice recommendation — Planned

As a golfer, I want the app to identify my largest weakness and suggest a drill so that my next practice session has a clear purpose.

Acceptance criteria:

- Recommendations are based on enough recent data and say when there is not enough data.
- The recommendation identifies the relevant category or distance band.
- I can start or configure a matching practice session from the recommendation.

### See progress over time — Planned

As a golfer, I want to see trends in total SG, category SG, three-putt rate, and distance bands so that I can tell whether practice is working.

Acceptance criteria:

- I can choose a date range and category.
- Empty periods are handled honestly.
- The chart or summary explains the sample size and comparison period.

### Use my own baseline — Planned

As a golfer, I want to compare against a personal baseline or handicap range so that the feedback reflects my current ability as well as the reference table.

Acceptance criteria:

- The baseline choice is visible in results.
- Changing a baseline does not corrupt the underlying saved inputs.
- The app explains how much data is needed before a personal baseline is reliable.

### Share a useful summary — Planned

As a golfer, I want to export a clear round or practice summary so that I can discuss it with a coach or keep a personal record.

Acceptance criteria:

- The export is readable without the app.
- It includes date, session type, relevant inputs, results, and baseline assumptions.
- The user chooses when and where the export is shared.

## Cross-cutting acceptance criteria

Every user-facing journey should also consider accessibility labels, large text, touch target size, clear validation, offline behavior, recovery from interruption, and a testable stable automation identifier.
