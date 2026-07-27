# Architecture cleanup guide

## Assessment

GolfSG has a sound four-project structure:

- `GolfSG.Core` contains domain models, strokes-gained calculations, statistics, and putting-game rules.
- `GolfSG.Application` contains workflows, ports, action guards, and testable view models.
- `GolfSG.Infrastructure` contains local JSON persistence.
- `GolfSG` contains the .NET MAUI composition root, pages, navigation, platform integration, and visual formatting.

This cleanup is intended to improve maintainability as the application grows. It is not a proposal to replace the architecture, introduce a large framework, or add unnecessary abstraction layers.

## Goals

- Make project ownership obvious from namespaces and file locations.
- Reduce the size and responsibility of the largest pages and view models.
- Keep business rules out of MAUI pages.
- Keep persistence details behind Application-owned interfaces.
- Preserve the current testability and dependency direction.

## Recommended order

### 1. Standardize Application namespaces

Application-owned files currently use namespaces such as `GolfSG.ViewModels` and `GolfSG.Services`, even though they live in the `GolfSG.Application` assembly.

Move them to names that communicate the assembly boundary, for example:

```csharp
namespace GolfSG.Application.ViewModels;
namespace GolfSG.Application.Services;
namespace GolfSG.Application.Common;
```

Update using directives, dependency-registration code, tests, and any global aliases. Do this as one mechanical change, then run the complete test suite.

Do not move domain classes into Application just to make the namespaces look consistent. Domain code should remain in Core.

### 2. Decompose the largest UI classes

The largest maintenance hotspots are:

- `src/GolfSG/Views/HoleEntryPage.cs`
- `src/GolfSG.Application/ViewModels/HoleInputViewModel.cs`

Keep the existing public behavior and bindings stable while separating concerns.

For `HoleEntryPage`, consider extracting:

- Reusable visual sections for putting, approach, and around-the-green input.
- Repeated labels, cards, and input-row construction.
- Navigation and guided-flow coordination, which can remain in a partial/action class or a small coordinator.

For `HoleInputViewModel`, consider extracting:

- Distance parsing and formatting.
- Category-specific input state.
- Carry-forward and shot mapping coordination.
- Result/summary projection for the UI.

Avoid splitting code merely by file length. Each extracted component should have one clear responsibility and a useful test seam.

### 3. Add query services when the read workflows grow

Several view models currently combine repository access, filtering, aggregation, and presentation formatting. This is acceptable at the current size, but history and evaluation features will become easier to maintain if the read-side workflows move into Application services.

Possible services include:

- `IRoundHistoryQueryService` for loading, filtering, and sorting history.
- `IRoundStatisticsQueryService` for date/category aggregation and trend calculations.
- `IRoundResultQueryService` for loading a round and producing result data.

These services should return domain/application result models. View models should remain responsible for binding state, commands, and display strings.

Do not introduce a service for every method. Add a service when it removes duplicated business logic or makes a workflow independently testable.

### 4. Extract reusable MAUI controls selectively

The application intentionally builds its UI in C#. That is valid and does not require a wholesale migration to XAML.

Extract a reusable control when:

- The same layout pattern appears on multiple pages.
- A page has become difficult to read because layout construction dominates the file.
- The control has a clear input/output or binding contract.

Continue using shared helpers such as `AppViews` for small styling primitives. Prefer focused controls for substantial sections rather than adding more methods to a single helper class.

### 5. Repository hygiene and test organization

Review empty folders, stale `.csproj.user` files, generated artifacts, and other local-only files. Remove only files confirmed to be unused or generated.

As the test suite grows, group tests by architectural area:

```text
tests/GolfSG.Tests/Core
tests/GolfSG.Tests/Application
tests/GolfSG.Tests/Infrastructure
tests/GolfSG.Tests/ViewModels
```

This is organizational only; it should not require changing test behavior.

## Non-goals

- Do not replace the four-project structure.
- Do not introduce a mediator, CQRS framework, or dependency-injection framework beyond the existing .NET facilities without a demonstrated need.
- Do not move scoring rules into view models or infrastructure.
- Do not rewrite the code-built MAUI UI solely for stylistic reasons.
- Do not add abstractions that only forward one method without improving testing or ownership.

## Definition of done

Each cleanup step is complete when:

- The dependency direction remains Core ← Application ← MAUI/Infrastructure as documented.
- Core and Application still have no MAUI dependency.
- Architecture boundary tests pass.
- The complete test suite passes.
- User-visible behavior and saved JSON compatibility are unchanged unless the task explicitly requires otherwise.
- The resulting code is easier to locate and modify than before the refactor.

The current baseline is a passing test suite with 188 tests. Keep that baseline green throughout the cleanup.