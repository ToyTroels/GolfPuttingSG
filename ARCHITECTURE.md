# Architecture

GolfSG uses four production projects with one-way dependencies:

~~~
GolfSG (MAUI composition and pages)
  -> GolfSG.Application (use cases, view models, ports, action guards)
     -> GolfSG.Core (domain models and all scoring/statistics rules)
  -> GolfSG.Infrastructure (local JSON persistence)
     -> GolfSG.Application
     -> GolfSG.Core
~~~

GolfSG.Tests references Core, Application, and Infrastructure directly. It does not link or recompile files from the MAUI project.

## Responsibilities

- **Core** owns strokes-gained calculations, round summaries, putting-game definitions, benchmark generation/scoring, and domain models. It has no UI or storage dependency.
- **Application** owns round editing/saving orchestration, putting-session progression and saving, repository/settings abstractions, testable view models, and reusable asynchronous action guards.
- **Infrastructure** owns RoundFileStore and the repository implementation for local JSON. The MAUI composition root supplies the platform-specific app-data path.
- **MAUI** owns pages, navigation, platform preferences, visual formatting, and dependency registration. Pages bind to application/view-model results and do not reproduce scoring rules.

## UI concurrency

AsyncActionGate provides the platform-independent single-flight primitive. MAUI's PageActionGuardExtensions gives each page separate action and navigation gates. Save, submit, delete, import/export, and navigation handlers use those gates so repeated taps do not overlap.

## Page structure

Interaction-heavy pages use partial classes to keep action/navigation state machines separate from layout construction:

- RoundInputPage.Actions.cs
- PuttingGamePage.Actions.cs
- HoleEntryPage.GuidedFlow.cs

Reusable form controls live in AppViews.
