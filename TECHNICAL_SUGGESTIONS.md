# Technical Suggestions

This document collects internal and technical improvements for GolfSG. The app already has a good MVP shape: a small core domain project, MAUI UI, MVVM-style view models, local JSON persistence, and focused calculation tests. The suggestions below are aimed at making the app easier to extend, safer to change, and more reliable as benchmark games, statistics, settings, and saved history become more important.

## Recommended Order

1. Finish extracting putting-game responsibilities from `PuttingGame` into smaller services.
2. Add view-model tests around putting game setup, benchmark starts, and save flows.
3. Split large UI pages into reusable controls or component builders.
4. Centralize app settings, units, colors, strings, and formatting.
5. Add diagnostics, user-visible error states, and CI/build automation.
6. Build benchmark history/comparison now that benchmark metadata is saved.

## High Priority

### 1. Replace string-based putting game modes with typed models

Status: Partially implemented.

Implemented:

- Added `PuttingGameKind`, `PuttingGameScoringMode`, `BenchmarkLength`, and `PuttingGameDefinition`.
- `PuttingGame` now exposes typed definitions for ladder, tour round, custom games, and benchmarks.
- Benchmark definitions include display name, preset ID, preset version, attempt count, distances, and scoring mode.
- Result/history titles now prefer saved game metadata instead of guessing only from legacy mode strings.

Remaining state:

- `RoundTrackingOptions` still stores `PuttingGameMode` as `string?` for backward compatibility.
- Some navigation/start APIs still pass mode strings such as `Ladder`, `TourRound`, `Short`, `Normal`, and `Thorough`.
- `PuttingGame` still owns more responsibilities than ideal.

Why improve it:

- String modes are easy to mistype and hard to evolve.
- Benchmark, custom, ladder, and tour games now have different scoring semantics.
- Future features like leaderboards, personal benchmarks, or benchmark versioning will need richer metadata.

Next suggestion:

Move the remaining string-based entry points toward typed definitions while preserving legacy JSON compatibility.

Current model:

```csharp
public enum PuttingGameKind
{
    Ladder,
    TourRound,
    Custom,
    Benchmark
}

public enum BenchmarkLength
{
    Short,
    Normal,
    Thorough
}

public sealed record PuttingGameDefinition(
    PuttingGameKind Kind,
    string DisplayName,
    IReadOnlyList<double> DistancesMeters,
    PuttingGameScoringMode ScoringMode,
    BenchmarkLength? BenchmarkLength = null,
    string? PresetId = null,
    int? PresetVersion = null);
```

Benefits:

- Reduces string comparisons in app logic.
- Makes custom vs benchmark vs legacy games explicit.
- Lets saved rounds preserve exactly what was played.
- Makes result screens less dependent on guessing from the number of holes or mode string.

### 2. Save the actual putting game definition metadata

Status: Implemented.

Implemented:

- Added `RoundGameInfo` to `Round`.
- Putting games now save type, display name, preset ID, preset version, attempt count, minimum distance, maximum distance, and expected total.
- Benchmark rounds save stable preset IDs such as `benchmark-normal-v1`.
- Custom putting games save their actual distance interval and expected total metadata.
- Result and history UI use `Round.GameInfo` when available, with legacy fallback to `RoundTrackingOptions.PuttingGameMode`.

Current model:

```csharp
public sealed record RoundGameInfo(
    string Type,
    string DisplayName,
    string? PresetId,
    int? PresetVersion,
    int AttemptCount,
    double? MinimumDistanceMeters,
    double? MaximumDistanceMeters,
    double ExpectedTotal);
```

Possible values:

- `Type = "StandardRound"`
- `Type = "PuttingGame"`
- `Type = "PuttingBenchmark"`
- `PresetId = "benchmark-normal-v1"`

Remaining follow-up:

- Add migration or cleanup later if the app ever removes `PuttingGameMode` from `RoundTrackingOptions`.
- Add benchmark history screens that group attempts by `PresetId`.

### 3. Add versioned persistence and atomic file writes

Status: Implemented.

Implemented:

- `RoundFileStore` writes a versioned storage document with `schemaVersion` and `rounds`.
- Saves write to `rounds.json.tmp` and then replace `rounds.json`.
- Existing valid active files are copied to `rounds.json.bak` before overwrite.
- Loading handles legacy array files, versioned documents, corrupt/empty active files, and backup fallback.
- Unreadable active files are preserved before overwrite.
- Storage migration from legacy app data directories is supported.

Current envelope:

```csharp
public sealed record RoundHistoryDocument(
    int SchemaVersion,
    IReadOnlyList<Round> Rounds);
```

Remaining follow-up:

- Surface more repository failure states in view models so the user can see import/save/load problems directly.
- Add intentional handling if a future `schemaVersion` is unsupported.

### 4. Separate scoring rules from distance generation

Current state:

- `PuttingGame` owns target putts, distance presets, bell-curve generation, expected-putt normalization, benchmark constants, and putt building.

Why improve it:

- It is becoming a mixed responsibility class.
- Benchmark scoring and ladder scoring are different concepts.
- Testing is easier if generation, scoring, and game definitions are independent.

Suggestion:

Split responsibilities:

- `PuttingDistanceGenerator`: creates bell-curve distance sets.
- `PuttingGamePresets`: exposes ladder, tour, benchmark definitions.
- `PuttingGameScorer`: calculates expected putts and strokes gained for a given game definition.
- `PuttingGameFactory`: builds `HolePuttingData` from a result.

Benefits:

- Cleaner tests.
- Easier to add new benchmark versions.
- Less risk of changing one game mode while fixing another.

### 5. Add persistence tests

Status: Implemented for repository/store behavior.

Implemented:

- Save creates the active file.
- Same-ID save updates instead of duplicating through the mutation path.
- Rounds load sorted by date.
- Delete preserves remaining rounds.
- Invalid/empty active JSON falls back to backup.
- Legacy array files load and migrate.
- Versioned export/import works.
- Configured hole count and early finish survive serialization.
- Putting benchmark metadata survives round-trip serialization.

Remaining useful tests:

- Newer/older schema versions are handled intentionally.
- Import conflict/merge behavior if imports stop replacing the full history.

Benefit:

Persistence is where user trust lives. It deserves tests as soon as the app stores meaningful history.

## Medium Priority

### 6. Move repeated UI primitives into reusable controls/builders

Current state:

- Pages define repeated local methods like `Card`, `StepperButton`, field rows, colors, and button styles.
- Several pages manually construct similar layouts.

Why improve it:

- UI changes require editing many pages.
- Colors and spacing can drift.
- Page classes are long and harder to scan.

Suggestion:

Create reusable helpers or controls:

- `AppCard`
- `PrimaryButton`
- `SecondaryButton`
- `StepperControl`
- `MetricRow`
- `SectionHeader`
- `NumericField`

In MAUI, this can be done as C# helper builders first, then moved to custom controls later if needed.

Benefit:

- Cleaner pages.
- Consistent styling.
- Faster UI iteration.

### 7. Introduce app-wide design tokens

Current state:

- Colors are repeated in C# pages as `Color.FromArgb(...)`.
- Some resources exist in XAML, but page code still owns many styling constants.

Suggestion:

Centralize tokens:

- Colors
- Spacing
- Corner radius
- Font sizes
- Button heights
- Card strokes

Possible approaches:

- Static `AppTheme` class for C# UI.
- XAML resources consumed by code.
- A small `Theme` service if dynamic theme changes become useful.

Benefit:

- More polished UI consistency.
- Dark mode becomes easier later.
- Reduces magic values in pages.

### 8. Add a settings service

Current state:

- Some settings are implied by constants.
- Distance-unit configuration is already listed in `TODO.md`.
- Putting-game defaults live in `PuttingGame`.

Suggestion:

Create an `IAppSettings` or `ISettingsRepository`.

Settings to support:

- Preferred distance unit: meters or feet.
- Default putting game min/max distance.
- Default custom putting attempt count.
- Last selected game mode or benchmark.
- Whether to show detailed remaining distances.

Benefit:

- Settings survive app restarts.
- View models stop relying only on static defaults.
- Future preferences become easier to add.

### 9. Add explicit validation results instead of inline strings

Current state:

- View models validate setup input and set string error messages directly.
- Invalid input handling is tied to UI text.

Suggestion:

Use small validation result types:

```csharp
public sealed record ValidationResult(bool IsValid, string? Message);
```

Or for more structure:

```csharp
public sealed record FieldValidationError(string FieldName, string Message);
```

Benefit:

- Validation can be tested without UI wording.
- Later localization is easier.
- UI can highlight the specific invalid field.

### 10. Use culture-aware parsing consistently

Status: Partially implemented.

Current state:

- Some code normalizes commas to dots and parses invariant culture.
- Display uses Danish culture.
- Putting-game setup now accepts both comma and dot decimals through a small local parser in `PuttingGameViewModel`.

Suggestion:

Create a central `NumberParser` or `DistanceInputParser` and use it everywhere.

It should:

- Accept both `1.5` and `1,5`.
- Clamp where appropriate.
- Return validation errors for impossible values.
- Keep parsing rules consistent across round input and putting-game setup.

Benefit:

- Fewer subtle input bugs.
- Better user experience in Denmark and other locales.

### 11. Improve result summaries for benchmark games

Current state:

- Benchmark games save scores and show totals.
- There is not yet a dedicated benchmark result breakdown.

Suggestion:

Add benchmark-specific result metrics:

- Total strokes gained.
- Strokes gained per 10 putts.
- Putts holed by distance band.
- One-putt percentage by distance band.
- Best/worst distance band.
- Comparison against previous attempts at the same benchmark preset.

Benefit:

- Makes benchmarks feel like a training product, not just a long putting game.
- Gives the player actionable feedback.

### 12. Make benchmark preset generation explicit and stable

Status: Implemented.

Implemented:

- Short, normal, and thorough benchmark distances are explicit arrays in code.
- The bell-curve generator remains available for custom games.
- Benchmark definitions include preset IDs and version numbers.
- Tests lock benchmark counts and the exact short benchmark distribution.

Remaining follow-up:

- Add exact snapshot tests for normal and thorough arrays too, not only counts.
- If a future benchmark changes, add `benchmark-*-v2` instead of editing v1.

Benefit:

- Strong guarantee that all users get the same benchmark.
- Benchmark versioning becomes easier.

### 13. Add view-model tests

Current state:

- Core calculations are tested.
- View-model flows are not tested.

Important tests:

- Starting a custom putting game creates the requested number of attempts.
- Benchmark buttons start 40/75/100 putts.
- Invalid setup input shows an error and does not start.
- Submitting the final putt saves exactly one round.
- Round input prevents both tracking toggles from being off.
- Editing an existing round preserves its ID.

Benefit:

- Protects the user flows most likely to regress during UI changes.

### 14. Add repository-level async/error states to view models

Current state:

- Save/load methods generally assume repository calls succeed.
- UI does not expose loading, saving, or error states.

Suggestion:

Add properties like:

- `IsBusy`
- `ErrorMessage`
- `CanSave`
- `CanSubmit`

And wrap repository calls in controlled error handling.

Benefit:

- Prevents double-submit.
- Avoids silent failures.
- Makes the app feel more robust on real devices.

## Lower Priority, High Leverage Later

### 15. Add structured logging

Current state:

- Debug logging is enabled, but app code does not log meaningful events.

Suggestion:

Inject `ILogger<T>` into repositories and major view models.

Log:

- Save/load/delete failures.
- Storage migrations.
- Unexpected invalid data.
- Benchmark starts/completions.

Avoid logging:

- Excessive per-keystroke input changes.
- Sensitive personal data if added later.

Benefit:

- Much easier debugging once the app is on phones.

### 16. Add CI checks

Suggestion:

Set up a simple GitHub Actions or Azure DevOps pipeline:

- Restore.
- Build core and tests.
- Run tests.
- Optionally build Windows target.
- Fail on warnings once analyzer noise is under control.

Benefit:

- Prevents regressions before they land.
- Makes refactoring safer.

### 17. Add static analysis and formatting rules

Suggestion:

Add `.editorconfig` rules for:

- Nullable reference types expectations.
- Field naming.
- Culture-aware formatting.
- Async naming.
- Analyzer severity.

Consider:

- Treating important warnings as errors.
- Adding StyleCop or Roslyn analyzers later, not immediately.

Benefit:

- Keeps style consistent as the project grows.

### 18. Consider separating app text for localization

Current state:

- UI text mixes Danish and English.
- Text is embedded in C# page construction.

Suggestion:

Decide on a primary language for MVP, then move strings to resources when the UI stabilizes.

Benefit:

- Cleaner localization.
- Easier copy review.
- Avoids accidental Danish/English mixing.

### 19. Improve data model naming

Current state:

- `HolePuttingData` now contains putting and approach fields.
- That made sense historically, but it is becoming a general hole result model.

Suggestion:

Rename later to something broader:

- `HoleResult`
- `HoleTrackingData`
- `HolePerformance`

Do this in a dedicated refactor commit because it will touch many files.

Benefit:

- Domain language stays accurate.
- Future approach/short-game/tee-shot tracking fits better.

### 20. Add benchmark history and comparison models

Suggestion:

Once benchmark games are stable, add a dedicated statistics layer:

- `BenchmarkAttempt`
- `BenchmarkSummary`
- `BenchmarkTrend`

Useful metrics:

- Best score by preset.
- Average of last 3 attempts.
- Improvement trend.
- Distance-band strengths and weaknesses.

Benefit:

- Turns benchmark mode into a real training feedback loop.

## Suggested Near-Term Backlog

### Sprint 1: Make benchmark data robust

- Done: Replace benchmark generation with explicit preset arrays.
- Done: Save benchmark preset ID and version in round metadata.
- Done: Update history/result title to show benchmark name.
- Done: Add short benchmark snapshot coverage.
- Remaining: Add full snapshot coverage for normal and thorough benchmark arrays.

### Sprint 2: Protect saved data

- Done: Add storage schema version.
- Done: Add atomic writes and backup recovery.
- Done: Add repository tests with temp files.
- Remaining: Add UI error messages for failed load/save/import.

### Sprint 3: Clean up UI foundations

- Extract shared button/card/field builders.
- Centralize colors and spacing.
- Add `IsBusy` and disabled states around save/submit.
- Add view-model tests for putting-game setup and round save flow.

### Sprint 4: Improve analysis value

- Add made percentage by distance band.
- Add benchmark-specific result screen details.
- Add previous benchmark comparison.
- Add period-based SG evaluation from `TODO.md`.

## Final Recommendation

Putting games and benchmark rounds are now first-class enough to support trustworthy saved history and future comparisons. The next strongest technical move is to finish separating `PuttingGame` into presets, scoring, generation, and factory responsibilities, then add view-model tests around the flows that create and save those games.

Persistence safety is in a much sturdier place now: atomic writes, schema versioning, backup recovery, legacy migration, and repository tests are present. The remaining trust work is mostly user-facing: clear load/save/import error states and diagnostics.
