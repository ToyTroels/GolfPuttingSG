# Testing strategy

## Current position

GolfSG now has 203 fast tests covering Core, Application, Infrastructure, and view-model behavior. The suite runs in a few hundred milliseconds and should remain the first pull-request gate.

The current strengths are:

- strokes-gained formulas and interpolation;
- putting, approach, and around-green category transitions;
- round history filtering and statistics;
- storage recovery, migration, import/export, and concurrent writes;
- round and putting-game orchestration;
- result-screen presentation and distance-unit formatting;
- concurrency guards and project dependency boundaries.

The main remaining gap is execution through the real MAUI interface. Critical controls now expose stable `AutomationId` values and semantic descriptions through `src/GolfSG/Views/UiAutomationIds.cs`, but an Appium runner and approved visual baselines are not yet implemented. Until they are, packaged-app smoke, accessibility, and physical-device checks remain mandatory release gates.

## Recommended test layers

| Layer | Purpose | When to run |
| --- | --- | --- |
| Core/Application unit tests | Calculations, validation, transitions, formatting, error paths | Every local build and pull request |
| Infrastructure integration tests | Real JSON files, migration fixtures, recovery, concurrent mutations | Every pull request |
| View-model regression tests | User-visible state, property changes, busy/error states | Every pull request |
| Appium behavior tests | Taps, text entry, navigation, persistence through the actual app | Small smoke set per pull request; full set nightly |
| Visual regression | Layout, clipping, colors, spacing, text scaling | Nightly and before releases |
| Manual exploratory/accessibility | Screen readers, touch ergonomics, real-device behavior | Release candidates |

Microsoft's MAUI guidance distinguishes unit tests from UI tests and recommends exercising the full interface on a real device or emulator for interaction-only regressions. The official UI approach is Appium:
https://learn.microsoft.com/en-us/dotnet/maui/deployment/ui-testing?view=net-maui-10.0

Official Appium/NUnit sample:
https://learn.microsoft.com/en-us/samples/dotnet/maui-samples/uitest-appium-nunit/

## UI automation design

Use the official multi-project Appium structure:

- tests/GolfSG.UITests.Shared
- tests/GolfSG.UITests.Windows
- tests/GolfSG.UITests.Android
- add iOS and Mac Catalyst projects when a macOS runner is available.

Windows can run Windows and Android tests. iOS and Mac Catalyst require a macOS host.

The automation contracts are in place. Before running the Appium suite in CI:

1. Extend the existing stable, unique AutomationId constants whenever a test uses a new control.
2. Add SemanticProperties.Description and Hint where controls are not self-describing.
3. Treat these identifiers as a public testing contract; do not derive them from translated display text.
4. Add a test launch mode with an isolated storage directory and known seed data.
5. Inject a clock and random source so dates and generated putting distances can be deterministic.
6. Disable or shorten animations in test mode.
7. Add a reset-data command available only in test/debug builds.

The official documentation specifically recommends AutomationId for Appium element lookup:
https://learn.microsoft.com/en-us/dotnet/maui/deployment/ui-testing?view=net-maui-10.0#prepare-the-net-maui-app-for-testing

Semantic properties should also support accessibility rather than using automation identifiers as screen-reader text:
https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility

## First UI journeys

Implement these in order:

1. **App launch**
   - Start with empty isolated storage.
   - Verify the start page, New Round, Putting Games, and empty-history state.

2. **Guided putting round**
   - Create a nine-hole round.
   - Enter one-putt, two-putt, and three-putt holes.
   - Finish early, save, verify the result, return to history, reopen, and edit.

3. **Category handoff**
   - Enable approach, around-green, and putting.
   - Enter an approach miss and verify its remaining distance/lie appears around the green.
   - Finish on the green and verify the putting distance is carried forward.

4. **Fixed-distance training**
   - Configure five attempts with minimum and maximum both 2.5 m.
   - Verify all five attempts are presented and the saved result contains five putts.
   - This protects the regression found by the new unit tests.

5. **Benchmark**
   - Complete a short ladder benchmark.
   - Verify progress, completion, saved preset metadata, and history title.

6. **Feet preference**
   - Select feet in settings.
   - Verify round entry, putting game setup, result, and history all show feet while saved calculations remain correct.

7. **Repeated taps**
   - Double-tap navigation, save, submit, and delete controls.
   - Verify one page transition and one mutation.

8. **Recovery**
   - Seed a corrupt active history file and a valid backup.
   - Launch and verify the recovery warning and restored rounds.

Use a page-object layer so test methods describe intent, for example StartPage.OpenNewRound and RoundPage.Save, while selectors remain centralized.

Avoid fixed Task.Delay waits. Poll for a control/state with a timeout. The official sample uses delays for simplicity, but condition-based waits will be more reliable in CI.

## Visual regression

Appium can capture screenshots. Keep visual comparison separate from behavior assertions:

- maintain one baseline set per platform, theme, display scale, and text-size profile;
- start with Windows and one fixed Android emulator;
- mask dynamic dates, scores, and system status bars;
- compare with a small pixel tolerance rather than exact bytes;
- save expected, actual, and diff images as CI artifacts;
- run the full visual suite nightly because fonts and platform rendering can make it noisier than behavior tests.

Initial screenshot checkpoints:

- empty start page;
- populated insights/history page;
- round setup;
- each guided entry step;
- active putting game;
- completed benchmark;
- round result with all categories;
- settings at normal and large text sizes.

## Regression test policy

Every production bug should add the lowest-level test that reproduces it:

- calculation bug: Core test;
- orchestration/state bug: Application or view-model test;
- JSON/migration bug: committed fixture plus Infrastructure test;
- navigation or binding bug: Appium test;
- clipping/style bug: screenshot regression.

Prefer invariants over implementation details. Examples:

- generated attempt count equals requested count;
- saving the same round ID never duplicates it;
- no enabled category means no valid round configuration;
- repeated actions create at most one mutation;
- loading a second result removes state from the first result;
- Core never references MAUI, Application, or Infrastructure.

## Persistence fixtures

Add tests/Fixtures/round-history with committed files for:

- legacy array format;
- current schema;
- corrupt/truncated JSON;
- unknown properties;
- missing optional metadata;
- future schema version.

Load those files in compatibility tests. Do not regenerate them during the test run: they are long-lived examples of data existing installations must continue to read.

## Coverage and quality gates

Coverage is useful for finding untested areas, not as proof of correctness. Add coverage reporting to CI and begin with:

- Core and Application: 80% line coverage and 70% branch coverage;
- Infrastructure: 70% line coverage;
- no percentage requirement for MAUI page construction;
- 100% journey coverage for the eight critical UI flows above.

Raise thresholds only after establishing a baseline. Microsoft documents both built-in code coverage and Coverlet integration:
https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage

Keep MSTest for the existing fast suite. There is no value in migrating 203 working tests merely because current MAUI documentation uses xUnit in its unit-test example. Use NUnit only for the Appium projects because that matches the official sample and keeps UI-runner setup conventional.

## Suggested rollout

### Phase 1: UI-test readiness

- [complete] add AutomationId constants and semantic descriptions for critical flows;
- add isolated test storage, deterministic clock/random, and reset/seed hooks;
- establish code coverage reporting.

### Phase 2: Smoke UI suite

- scaffold Shared, Windows, and Android Appium projects;
- implement launch, guided round, fixed-distance training, and repeated-tap journeys;
- capture screenshots on failure.

### Phase 3: Full regression suite

- add category handoff, benchmark, feet preference, and recovery journeys;
- introduce approved visual baselines;
- run Windows smoke tests per pull request and Android/full visual tests nightly.

### Phase 4: Device confidence

- add iOS on a macOS runner;
- run a small smoke suite on at least one physical Android and iOS device before releases;
- include large text, dark/high-contrast theme, and screen-reader exploratory checks.
