# Architecture

GolfSG separates the app, HTTP API, server persistence, and database setup into components with one-way dependencies:

~~~
GolfSG (MAUI composition and pages)
  -> GolfSG.Application -> GolfSG.Core
  -> GolfSG.Infrastructure -> GolfSG.Application

GolfSG.Api (HTTP and server composition)
  -> GolfSG.Application -> GolfSG.Core
  -> GolfSG.Server.Infrastructure -> GolfSG.Application

GolfSG.Database.Setup (explicit schema updates)
  -> Npgsql
~~~

`GolfSG.Infrastructure` also references Core for its local persistence models. PostgreSQL runs as a separate database service. `GolfSG.Server.Infrastructure` is a library loaded by the API; moving persistence there separates code ownership without creating another running service. `GolfSG.Database.Setup` is a separate command that connects directly to PostgreSQL and exits. It does not reference or start the API.

GolfSG.Tests references Core, Application, and device Infrastructure directly. GolfSG.Api.Tests covers the API, server stores, setup ownership, and their dependency boundaries. Neither suite links or recompiles files from the MAUI project.

## Responsibilities

- **Core** owns strokes-gained calculations, round summaries, putting-game definitions, benchmark generation/scoring, and domain models. It has no UI or storage dependency.
- **Application** owns round editing/saving orchestration, putting-session progression and saving, repository/settings abstractions, shared API contracts, testable view models, and reusable asynchronous action guards. Its `Server` area owns database-independent server storage interfaces and `ServerRoundService`, including round-write validation. It has no ASP.NET Core, EF Core, or PostgreSQL dependency.
- **Infrastructure** owns local JSON persistence and the app's HTTP API clients, including authenticated requests and token refresh. Completed history and the single active-round draft are stored separately so unfinished data cannot affect statistics. The MAUI composition root supplies the platform-specific app-data path and secure token storage. An installation binds its sync state to the first account/server; signing out removes its tokens while preserving offline history and that binding.
- **MAUI** owns pages, navigation, platform preferences, visual formatting, and dependency registration. Pages bind to application/view-model results and do not reproduce scoring rules.
- **API** owns HTTP routes, username/password registration and sign-in, protected bearer token issuance/refresh, authorization, request/response mapping, status codes, request-size limits, error handling, and dependency registration. Round controllers obtain the owner ID from the authenticated `NameIdentifier` claim. Requests cannot choose an owner. Controllers call Application services or storage interfaces rather than EF Core or SQL. The Products route remains an authenticated, shared connection-test feature. The weather sample endpoint has been removed.
- **Server.Infrastructure** owns `AppDbContext`, ASP.NET Core Identity storage and password hashing, database entities and mappings, EF Core/Npgsql dependencies, and implementations of the server storage interfaces. All round reads and atomic version-checked writes include the authenticated owner's ID. PostgreSQL keys rounds by `(user_id, id)` and retains deletion records under that same owner. Database entities do not become the HTTP response contract.
- **Database.Setup** owns the transactional migration runner, versioned SQL, and application-role grants. The API never runs schema updates at startup and has no database-update command.

## Local database administration

Use [Local database setup](docs/LOCAL_DATABASE_SETUP.md) to run the setup project before starting the API. Runtime access uses `ConnectionStrings:Default`; the setup command uses the owner connection and explicit grants. The two executables currently share the existing development User Secrets store so established local settings still work. This configuration compatibility does not make schema updates part of API execution.

Applied migration filenames, contents, and checksums remain compatible with the previous API-hosted runner. Migration files live in `src/GolfSG.Database.Setup/Sql/Migrations`; add a new file for a schema change rather than editing an applied migration. Migration `002_identity_round_ownership.sql` adds Identity tables and round ownership while preserving the original migration unchanged. Existing unowned server rounds and tombstones retain the reserved owner `__legacy__` and are invisible to new accounts. They are not automatically assigned to whoever registers first. Existing local history can upload as independent owned records after sign-in.

## Local account authentication

`/api/auth/register` and `/api/auth/login` accept a username and password. `/api/auth/refresh` accepts a refresh token; `/api/auth/me` returns the authenticated account ID and username. Access and refresh tokens use ASP.NET Core Identity's protected opaque bearer format rather than JWTs. PostgreSQL stores password hashes, never plaintext passwords. The app persists tokens in platform secure storage and does not retain the password.

Rounds and Products require the Identity bearer scheme. Products are shared test data; rounds are private to the current account. Authentication endpoints apply a per-IP rate limit, and repeated failed passwords temporarily lock an account. This local pilot has no email verification, password recovery, account deletion screen, or public identity provider. Persist the API's Data Protection keys for IIS so tokens survive restarts; see [Products API setup](docs/PRODUCTS_API_SETUP.md).

## Active round recovery

An active round is autosaved as an `ActiveRoundSession` containing its draft, current hole, and update time. Finalizing writes the completed or intentionally shortened round to history before deleting the active session. The start page reconciles a stale active session whose id is already present in history.

## UI concurrency

AsyncActionGate provides the platform-independent single-flight primitive. MAUI's PageActionGuardExtensions gives each page separate action and navigation gates. Save, submit, delete, import/export, and navigation handlers use those gates so repeated taps do not overlap.

## Page structure

Interaction-heavy pages use partial classes to keep action/navigation state machines separate from layout construction:

- RoundInputPage.Actions.cs
- PuttingGamePage.Actions.cs
- HoleEntryPage.GuidedFlow.cs

Reusable form controls live in AppViews.
