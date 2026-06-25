# Technical Suggestions

This document collects internal and technical improvements for GolfSG. The app already has a good MVP shape: a small core domain project, MAUI UI, MVVM-style view models, local JSON persistence, and focused calculation tests. The suggestions below are aimed at making the app easier to extend, safer to change, and more reliable as benchmark games, statistics, settings, and saved history become more important.

## Recommended Order

1. Strengthen the domain model for game modes and saved round metadata.
2. Make persistence versioned, atomic, and resilient to corrupt files.
3. Split large UI pages into reusable controls or component builders.
4. Add tests around persistence, view-model flows, and benchmark presets.
5. Centralize app settings, units, colors, strings, and formatting.
6. Add diagnostics, error states, and CI/build automation.

## High Priority

### 1. Replace string-based putting game modes with typed models

Current state:

- Putting game modes are represented with strings such as `Ladder`, `TourRound`, `Short`, `Normal`, and `Thorough`.
- `RoundTrackingOptions` stores `PuttingGameMode` as `string?`.
- Saved result screens infer behavior from mode strings and saved hole data.

Why improve it:

- String modes are easy to mistype and hard to evolve.
- Benchmark, custom, ladder, and tour games now have different scoring semantics.
- Future features like leaderboards, personal benchmarks, or benchmark versioning will need richer metadata.

Suggestion:

Create typed records/enums for putting game definitions.

Example direction:

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
    BenchmarkLength? BenchmarkLength = null);
```

Benefits:

- Removes string comparisons from app logic.
- Makes custom vs benchmark vs legacy games explicit.
- Lets saved rounds preserve exactly what was played.
- Makes result screens less dependent on guessing from the number of holes or mode string.

### 2. Save the actual putting game definition metadata

Current state:

- Saved rounds contain holes and `RoundTrackingOptions`.
- For custom or benchmark putting games, the actual hole distances are saved through each `HolePuttingData`, which is good.
- But the round does not clearly store whether it was a custom game, a short benchmark, normal benchmark, thorough benchmark, or which benchmark preset version was used.

Why improve it:

- If benchmark distances are ever adjusted, old rounds should still be interpreted correctly.
- History should be able to say `Normal Benchmark`, not just `Putting Game`.
- Benchmark comparison only works if the app knows which benchmark was played.

Suggestion:

Extend round metadata with a dedicated game info record.

Example direction:

```csharp
public sealed record RoundGameInfo(
    string Type,
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

Benefits:

- Result/history UI becomes truthful and simple.
- Benchmark results can be grouped reliably.
- Future migrations can handle old rounds without fragile inference.

### 3. Add versioned persistence and atomic file writes

Current state:

- `FileRoundRepository` stores all rounds in one `rounds.json`.
- Saves use `File.Create(filePath)` directly.
- Deserialization assumes the file is readable and compatible.

Risks:

- A crash during write can leave `rounds.json` partially written.
- A malformed file can break loading all history.
- Future model changes may break older saved data.

Suggestion:

Introduce a storage envelope with schema version and atomic writes.

Example direction:

```csharp
public sealed record RoundsFile(
    int SchemaVersion,
    IReadOnlyList<Round> Rounds);
```

When saving:

1. Serialize to `rounds.json.tmp`.
2. Flush and close the file.
3. Replace `rounds.json` with the temp file.
4. Optionally keep `rounds.json.bak`.

When loading:

- Catch `JsonException`, `IOException`, and incompatible schema cases.
- Try backup recovery.
- Return a user-visible error state instead of silently returning empty history.

Benefits:

- Protects user data.
- Gives room for migrations.
- Makes storage bugs easier to diagnose.

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

Current state:

- Tests cover strokes-gained calculations well.
- There are no repository tests for save/load/edit/delete behavior.

Suggestion:

Add tests using a repository constructor that accepts a file path or storage abstraction.

Test cases:

- Saving a first round creates the file.
- Saving the same round ID updates it rather than duplicating it.
- Multiple rounds load sorted by date.
- Delete removes only the requested round.
- Invalid JSON returns a controlled failure or recovery path.
- Newer/older schema versions are handled intentionally.
- Putting benchmark metadata survives round-trip serialization.

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

Current state:

- Some code normalizes commas to dots and parses invariant culture.
- Display uses Danish culture.
- Putting-game setup currently parses numeric text in the view model.

Suggestion:

Create a central `NumberParser` or `DistanceInputParser`.

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

Current state:

- Benchmark lists are deterministic because they are generated from fixed constants and algorithm.
- But the preset values are not written out as explicit arrays.

Concern:

- If the bell-curve algorithm changes, benchmark distances change too.
- The user requirement says the distances should be the same for every user.

Suggestion:

Either:

- Store generated benchmark arrays explicitly in code as fixed lists, or
- Add snapshot tests that lock the exact benchmark distribution and key distances.

Best option:

- Generate once, paste the arrays as `IReadOnlyList<double>` constants, and keep the generator for custom games only.

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

- Replace benchmark generation with explicit preset arrays.
- Add snapshot tests for short, normal, and thorough benchmark distributions.
- Save benchmark preset ID and version in round metadata.
- Update history/result title to show benchmark name.

### Sprint 2: Protect saved data

- Add `RoundsFile` schema version.
- Add atomic writes and backup recovery.
- Add repository tests with temp files.
- Add UI error messages for failed load/save.

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

The strongest next technical move is to make putting games and benchmark rounds first-class domain concepts instead of string modes plus inferred behavior. That will make the new benchmark feature much easier to trust, compare, migrate, and extend.

After that, invest in persistence safety. For a golf tracking app, losing or corrupting saved rounds is the highest-trust failure. Atomic writes, schema versioning, backup recovery, and repository tests will give the app a much sturdier foundation.
