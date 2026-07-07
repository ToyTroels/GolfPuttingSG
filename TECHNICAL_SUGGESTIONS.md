# Technical Suggestions

Last reviewed: 2026-07-07.

This document summarizes internal improvements for GolfSG after reviewing the current codebase, the existing technical suggestions, `TODO.md`, and the app/core/test project structure. The app has moved beyond the earlier putting-only MVP: it now tracks putting, approach, around-the-green, putting games, bell-curve benchmarks, ladder benchmarks, saved history, import/export, diagnostics, and hardened local JSON persistence.

The main conclusion is that persistence and putting-game internals are much healthier than before. The next internal bottleneck is the round and hole entry flow, especially the view-model and page code that now has to coordinate multiple strokes-gained categories.

## Current Architecture Snapshot

- `src/GolfSG.Core` contains domain models, strokes-gained calculators, putting-game presets/generation/scoring/factory helpers, and JSON file persistence.
- `src/GolfSG` contains the MAUI app, code-built pages, view models, repository adapter, theme helpers, and navigation flow.
- `tests/GolfSG.Tests` covers core calculations, persistence, putting-game view-model flows, start/history ordering, and a few hole-entry behaviors.
- `RoundFileStore` already has a versioned envelope, legacy-array support, atomic temporary writes, backup recovery, corrupt-file preservation, import/export, migration from legacy storage folders, and mutation serialization.
- `PuttingGame` is now mostly a facade over `PuttingGamePresets`, `PuttingDistanceGenerator`, `PuttingGameScoring`, and `PuttingGameFactory`.
- `GolfTheme` and `AppViews` exist, so UI tokenization has started, but large page classes still duplicate controls and layout patterns.

Largest current source files, excluding generated `bin`/`obj` output:

| File | Lines | Why it matters |
|---|---:|---|
| `src/GolfSG/ViewModels/HoleInputViewModel.cs` | 1143 | Mixes parsing, formatting, shot mapping, carry-forward rules, SG recalculation, and UI state. |
| `src/GolfSG/Views/HoleEntryPage.cs` | 940 | Large code-built page with many local control builders and complex visibility/layout behavior. |
| `src/GolfSG/Views/RoundInputPage.cs` | 516 | Repeats page composition, cards, buttons, and summary blocks. |
| `src/GolfSG/Views/PuttingGamePage.cs` | 509 | Similar repeated UI primitives, but less domain complexity than hole entry. |
| `src/GolfSG/ViewModels/PuttingGameViewModel.cs` | 417 | Manageable, but still stores legacy mode strings and local parsing. |

## Recommended Order

1. Decompose `HoleInputViewModel` into smaller state, parsing, mapping, and carry-forward services.
2. Continue moving consumers from persisted `HolePuttingData` toward the new internal `HoleResult` mapper before adding more SG categories.
3. Centralize distance conversion, distance parsing, and shot-lie text mapping.
4. Add typed repository operation results and view-model-level busy/error state instead of relying on mutable repository flags plus page-level `try/catch`.
5. Continue extracting reusable C# MAUI builders/controls from the large pages, starting with hole-entry primitives.
6. Add an app statistics/query layer before implementing periodic evaluations, benchmark trends, or made-percentage reporting.
7. Make storage schema-version handling explicit for unsupported future versions.
8. Add CI, analyzers, and formatting after the highest-churn refactors settle.

## High Priority

### 1. Split `HoleInputViewModel` into focused pieces

Current state:

- `HoleInputViewModel` is the largest hand-written class in the repo.
- It owns all hole-entry mutable text state.
- It parses comma/dot decimal input.
- It maps Danish/English lie labels to `ShotLie`.
- It converts meters, feet, and yards.
- It builds `GolfShot` objects for approach and around-the-green.
- It carries approach results forward into around-green or putting.
- It recalculates strokes gained and raises many UI property changes.

Why improve it:

- Small changes to one category can accidentally affect another category.
- It is hard to test carry-forward, parsing, and shot construction independently.
- Adding off-the-tee or richer approach/ARG input would make the class significantly harder to reason about.

Suggested extraction:

- `DistanceInputParser`: accepts both comma and dot decimals and returns a typed validation result.
- `DistanceConversions`: owns meters/feet/yards constants and conversions.
- `ShotLieLabels`: maps UI labels to/from `ShotLie`.
- `ShotInputMapper`: builds approach and around-green `GolfShot` instances from input state.
- `HoleCarryForwardService`: decides how approach and ARG finish states populate later categories.
- `HoleEntryState`: a plain mutable or immutable state object that can be tested without MAUI notifications.

Good first slice:

Move only static parsing/conversion/lie mapping and `BuildApproachShot`/`BuildAroundGreenShot` behavior out of the view model. Keep public view-model properties unchanged. Add tests that assert the produced `GolfShot` values match today's behavior.

Benefits:

- Lower-risk tests around the most complex hole-entry rules.
- Easier future category work.
- Less property-notification noise in the core behavior.

### 2. Replace or isolate `HolePuttingData` before it grows further

Status:

- First slice implemented: `HoleResult`, `PuttingResult`, `ApproachResult`, `AroundGreenResult`, and `HoleResultMapper` now isolate the persisted `HolePuttingData` DTO from read-side category logic.
- `HolePuttingData` remains the v1 saved-round shape, so existing saved rounds continue to load without a storage migration.
- Tracked-hole completion, made-percentage read logic, and result item display now use the category-aware mapper.
- Mapper tests cover round-tripping complete holes, old-style approach data, multiple around-green shots, and empty holes.

Current state:

`HolePuttingData` now stores putting fields, legacy approach fields, shot-level approach fields, around-green fields, and a list of around-green shots. The name no longer matches the responsibility.

Why improve it:

- The model is becoming a catch-all DTO.
- Optional category fields are represented as many zero/default values.
- Future off-the-tee, penalties, or multiple approach shots will make the record wider and less trustworthy.
- The persistence schema depends on this shape, so casual renaming would be risky.

Suggested model direction:

```csharp
public sealed record HoleResult(
    int HoleNumber,
    PuttingResult? Putting,
    ApproachResult? Approach,
    AroundGreenResult? AroundGreen);

public sealed record PuttingResult(
    double FirstPuttDistanceMeters,
    int Putts,
    double ExpectedPutts,
    double StrokesGained);

public sealed record ShotCategoryResult(
    IReadOnlyList<GolfShot> Shots,
    double ExpectedStartStrokes,
    double ExpectedFinishStrokes,
    double StrokesGained);
```

Migration approach:

- Keep `HolePuttingData` as the persisted v1 DTO for now. Done for the first mapper slice.
- Use `HoleResultMapper` when read paths need category-specific data instead of reaching directly into the wide DTO.
- Move summaries/calculators to the new model when practical.
- Only write a v2 storage schema once the new model is stable.

Benefits:

- Clear category boundaries.
- Fewer misleading zero/default states.
- A safer path to off-the-tee or multi-shot approach tracking.

### 3. Centralize distance units, input parsing, and lie labels

Current state:

- `MetersPerFoot`, `MetersPerYard`, `FeetPerYard`, and conversion helpers are repeated across core services, view models, result formatting, and reference pages.
- Numeric parsing is repeated in `HoleInputViewModel` and `PuttingGameViewModel`.
- Lie text mapping is repeated in `HoleInputViewModel`, `AroundGreenShotSummaryViewModel`, and `HoleResultItemViewModel`.
- Some property names such as `ApproachStartDistanceYards` are used by UI setters that accept meters and later convert to yards, which is a naming trap.

Suggestion:

- Add a small core `DistanceConversions` helper.
- Add app-level `DistanceInputParser` and `DistanceFormatter` if UI culture concerns should stay out of core.
- Add `ShotLieLabels` for stable UI text mapping.
- Rename UI-facing state over time so names describe the displayed unit, for example `ApproachStartDistanceInputMeters`, while persisted/domain shot values remain explicit about feet/yards/meters.

Benefits:

- Fewer unit bugs.
- Easier feet/meters preference implementation.
- Less duplicated formatting and label logic.

### 4. Move repository warnings/errors into typed operation results

Current state:

- `IRoundRepository` exposes mutable status flags such as `WasLastReadRecoveredFromBackup`.
- Views generally catch exceptions and show alerts.
- View models do not consistently expose `IsBusy`, `ErrorMessage`, or disabled states.
- Repository status can be overwritten by a later repository call, which makes diagnostics and user warnings dependent on call order.

Suggestion:

Introduce operation result types:

```csharp
public sealed record RoundRepositoryResult<T>(
    T Value,
    IReadOnlyList<RoundRepositoryWarning> Warnings);

public enum RoundRepositoryWarning
{
    RecoveredFromBackup,
    MigratedFromLegacyStorage,
    PreservedUnreadableActiveFile
}
```

Then let view models own user-facing state:

- `IsLoading`
- `IsSaving`
- `ErrorMessage`
- `CanSubmit`
- `CanDelete`
- `StorageWarnings`

Benefits:

- Warnings travel with the data that caused them.
- Pages become thinner.
- Double-submit and repeated-tap issues become easier to prevent.
- Save/load/import failures can be tested in view-model tests.

### 5. Make storage schema-version behavior explicit

Current state:

- Versioned history documents are written with `schemaVersion = 1`.
- Loading accepts any object with a `rounds` array and does not currently branch on `schemaVersion`.
- Legacy arrays are still supported.

Suggestion:

- Treat missing `schemaVersion` plus `rounds` as v1-compatible if needed.
- Switch on known versions when reading.
- Return a clear unsupported-version error for future versions rather than silently reading a shape that may have changed.
- Add tests for missing, current, older, and future schema versions.

Benefits:

- Future storage migrations become deliberate.
- Importing a newer file from a future app version cannot silently corrupt assumptions.

### 6. Continue extracting reusable MAUI view builders

Current state:

- `GolfTheme` and `AppViews` are good starts.
- Large pages still define repeated local helpers such as cards, steppers, distance panels, stat blocks, quick action chips, picker panels, toggle rows, and metric rows.

Suggestion:

Keep this simple and code-first at first:

- `MetricRow`
- `SgMetric`
- `CounterEditor`
- `DistanceEditor`
- `ShotPositionEditor`
- `QuickChoiceGroup`
- `ErrorBanner`
- `StorageWarningBanner`
- `AsyncActionButton`
- `PageSectionHeader`

Benefits:

- Smaller page classes.
- Consistent spacing and typography.
- Easier accessibility work because labels/hints can be added once.

### 7. Add an app statistics/query layer

Current state:

- `RoundResultViewModel` builds one-round summaries and analysis strings inline.
- `RoundListItemViewModel` calculates history row summaries directly.
- `TODO.md` includes periodic SG evaluations and made percentage by first-putt distance.
- Benchmark metadata is now rich enough to support benchmark history and comparisons.

Suggestion:

Create a statistics service before adding more analysis UI:

- `RoundStatisticsService`
- `BenchmarkHistoryService`
- `PuttingDistanceBucketService`
- `PeriodFilter`
- `SgTrendSummary`

Useful outputs:

- Total SG by category.
- Average SG per round.
- Best/worst round.
- Trend over period.
- Made percentage by distance bucket.
- Benchmark best, last, average of last 3, and preset-specific trend.

Benefits:

- Result, history, benchmark, and future statistics screens use the same calculations.
- Tests can cover analysis behavior without MAUI page dependencies.
- The app can add richer insights without duplicating summary logic.

## Medium Priority

### 8. Gradually move app logic out of the MAUI project

Current state:

The test project links app view-model source files directly:

- `HoleInputViewModel.cs`
- `PuttingGameViewModel.cs`
- `RoundListItemViewModel.cs`
- `StartViewModel.cs`
- `UiFormat.cs`
- `ViewModelBase.cs`
- `IRoundRepository.cs`

This works, but it becomes fragile as view models depend on more MAUI-specific services.

Suggestion:

Consider a small `GolfSG.AppLogic` class library later:

- View models that do not need direct MAUI APIs.
- Repository interfaces and result types.
- App services such as round editing, statistics, and settings.
- UI formatting helpers that are not tied to controls.

Benefits:

- Cleaner tests.
- Less compile-link trickery.
- MAUI pages become closer to pure views.

### 9. De-duplicate old and new approach calculation paths

Current state:

- `ApproachStrokesGainedCalculator` still has a distance-only `AddApproach` path.
- `StrokesGainedApproachService` has the better shot-level SG formula and category boundary handling.
- `StrokesGainedCalculator.BuildHole` supports both legacy distance-only approach fields and shot-level approach shots.

Suggestion:

- Treat shot-level approach as the preferred internal path.
- Keep distance-only calculation only as a legacy compatibility path.
- Make summaries and new UI flows operate on shot-level data.
- Add comments/tests around which path is legacy and when it can be removed.

Benefits:

- Fewer competing definitions of SG approach.
- Less risk that a new feature accidentally uses the simplified MVP formula.

### 10. Remove remaining game-mode string flow from new code

Current state:

- `PuttingGameDefinition` and `RoundGameInfo` are now strong enough for current game metadata.
- `RoundTrackingOptions.PuttingGameMode` is still `string?` for backward compatibility.
- `PuttingGameViewModel` still starts from mode strings for ladder/tour game paths.

Suggestion:

- Keep persisted legacy strings readable.
- Prefer passing `PuttingGameDefinition` or stable preset IDs in new app logic.
- Restrict string normalization to a compatibility boundary.

Benefits:

- Less fragile benchmark/game routing.
- Easier benchmark versioning.
- Better separation between saved metadata and UI flow.

### 11. Add more view-model tests around round and hole entry

Current state:

- Putting-game view-model coverage is now useful.
- Start/history ordering has tests.
- Hole-entry tests cover only a few important behaviors.
- `RoundInputViewModel` is not directly covered.

High-value tests:

- Round setup cannot disable all SG categories.
- Changing hole count preserves entered holes and prevents trimming entered data.
- Editing a round preserves ID/date/tracking settings.
- Approach finish on green carries putting distance when putting is enabled.
- Approach miss carries ARG start distance/lie when ARG is enabled.
- ARG finish on green carries putting distance when putting is enabled.
- Manual override prevents carry-forward from overwriting user input.
- Save failures surface a view-model error and do not navigate as success.
- Repository backup/migration warnings surface in start/history state.

### 12. Add settings behind an interface before adding feet/meters preference

Current state:

- There is a `FeatureSettings` helper.
- UI and defaults are mostly constants.
- `TODO.md` includes configurable distance units.

Suggestion:

Add `IAppSettings` or `ISettingsRepository` for:

- Preferred distance unit.
- Default putting-game putt count.
- Default custom game min/max distance.
- Last selected benchmark type/order.
- Whether detailed remaining distances are expanded by default.

Benefits:

- Settings survive restart.
- Unit preference can be implemented without scattering conditional logic.
- View models stop depending on static defaults.

## Lower Priority, High Leverage Later

### 13. Add structured logging at boundaries

Log:

- Storage read/write/import/export failures.
- Backup recovery and legacy migration.
- Unsupported schema versions.
- Benchmark starts/completions.
- Unexpected invalid persisted data.

Avoid:

- Per-keystroke logging.
- Full personal round history in logs.

### 14. Add CI and static analysis

Suggested checks:

- `dotnet restore GolfSG.sln`
- `dotnet test tests/GolfSG.Tests/GolfSG.Tests.csproj`
- Windows MAUI build if the runner supports it.
- Analyzer pass after style noise is under control.

Suggested config:

- `.editorconfig`
- Nullable warnings expectations.
- Culture-aware parsing/formatting rules.
- Async naming and unused member cleanup.
- Treat selected warnings as errors later.

### 15. Keep localization as a planned cleanup

Current state:

- User-facing text is embedded in C# pages and view models.
- Danish is the primary UI language, while technical identifiers and some legacy strings are English.

Suggestion:

- Keep the MVP language decision simple for now.
- Move strings into resources once the UI stabilizes.
- Start with repeated alerts, validation messages, labels, and benchmark names.

## Implemented or Lowered From Earlier Priority

These earlier recommendations are now done or no longer the first internal bottleneck:

- Putting-game responsibilities have mostly been split into presets, generation, scoring, and factory helpers.
- Benchmark metadata is saved in `RoundGameInfo`.
- Ladder benchmark presets and metadata are implemented.
- Versioned persistence, atomic writes, backup fallback, legacy import, migration, import/export, and diagnostics are implemented.
- Repository mutations are serialized.
- Some view-model tests now exist for putting-game and start/history flows.
- App theme tokens and basic shared view builders exist.

## Suggested Refactor Slices

### Slice 1: Shared units and labels

Add:

- `DistanceConversions`
- `DistanceInputParser`
- `ShotLieLabels`

Move duplicated constants and parsing/label mapping out of view models. Add focused tests. This is small and reduces risk for later work.

### Slice 2: Shot input mapping

Add `ShotInputMapper` and move approach/ARG `GolfShot` construction out of `HoleInputViewModel`. Keep the current public view-model API intact. Add tests for approach-to-green, approach-to-ARG, holed approach, ARG-to-green, and penalty cases.

### Slice 3: Carry-forward service

Move carry-forward decisions into `HoleCarryForwardService`. The view model should ask the service what to update, then apply updates with property notifications. Test manual override behavior separately from MAUI binding behavior.

### Slice 4: Round operation state

Add `IsBusy`, `ErrorMessage`, and typed warnings to `StartViewModel`, `RoundInputViewModel`, and `PuttingGameViewModel`. Update pages to bind state instead of each page owning all error semantics.

### Slice 5: Statistics service

Before adding periodic evaluations or benchmark trend UI, create a service that calculates those summaries from saved rounds. Use tests to lock the aggregation rules.

## Final Recommendation

The strongest next technical move is to treat hole entry as its own internal subsystem. The calculation layer is reasonably isolated, persistence is now sturdy, and putting games have a healthier split than the old suggestions described. The internals will become much easier to extend if `HoleInputViewModel` stops being the place where every category rule, conversion, formatter, and shot mapping lives.

After that, broaden `HolePuttingData` into a real category-aware hole result model and put all statistics/benchmark history behind a tested query layer. That gives the app a clean path to periodic evaluations, made-percentage analysis, configurable units, and future SG categories without making the UI layer carry the domain complexity.
