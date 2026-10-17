# Component versioning and compatibility

Status: initial component versions are implemented in build properties and database
migration 004. The broader release policy below is proposed; runtime compatibility
checks and automated cross-release certification are not implemented yet.

## Release identities

Version independently deployable components, while keeping internal libraries tied
to the commit that builds them.

| Component | Proposed identity | When to change it |
| --- | --- | --- |
| MAUI app (all platforms) | App semantic version plus increasing store build number | Each app release; increase the build number for every distributed build |
| HTTP API | API semantic version plus source commit | Each API release, independently of app releases |
| Database setup tool | Setup semantic version plus source commit | Each released change to the setup tool or bundled migrations |
| Database schema | Independent database semantic version, mapped to an exact set of migration IDs and checksums | Each released schema change; never edit an applied migration |
| Local files and exports | Separate integer schema version for each format | When that format changes incompatibly |
| Scoring rules and reference data | Explicit scoring/baseline identifier | When results could change for identical input |

Core, Application, Infrastructure and Server.Infrastructure do not need separate
release schedules while they are project references shipped inside the app/API.
Record their source commit with the containing release. Give a library its own
version policy if it becomes an independently distributed package.

For app, API, database schema and setup releases, use major/minor/patch: breaking changes, compatible
features, and compatible fixes respectively. Prerelease labels do not waive the
requirement to document breaking changes. An app version and API version need not
match. A store build number, migration number and API contract are different identities.

Keep the existing `v<app-version>` tags working for Android releases. Introduce
`api/v<version>` and `database-setup/v<version>` when independent releases are wired
into the build. Do not rename existing tags.

## Database version

Give the database schema its own release version, independent of the database
setup executable and PostgreSQL engine version. Use a major bump for incompatible
schema changes (for example removing a column required by a supported API), a
minor bump for compatible additions, and a patch bump for compatible corrections.
Data migrations that change application behavior also require a schema release.

Each database release must map to an immutable manifest of required migration
names and checksums. Keep `public.golfsg_schema_versions` as the migration audit
trail, and record the installed database release in database metadata only after
its complete migration set has succeeded, in the same transaction. A failed
upgrade must leave both the schema and installed release at the previous version.
Do not infer a release solely from the highest migration number.

The initial database semantic version is `0.1.0-alpha`, established by migration
`004_database_release_version.sql` after migrations 001–003. It is stored in
`public.golfsg_database_release`, independently of the migration audit trail.
The setup tool checks preceding migrations' checksums before reaching migration
004; this does not claim compatibility with untested historical API releases.
Use `database/v<version>` tags for future database schema releases. The API must
declare its supported database version range, with tested combinations recorded
below. The setup tool may apply several database releases without sharing their
version numbers.

## Current inventory

| Item | Evidence in the current source | Limitation |
| --- | --- | --- |
| App | `Directory.Build.props`: `0.3.0-alpha`, build `4` | Does not identify an installed package's source commit by itself |
| API | `0.6.0-alpha`, configured by `ApiVersionPrefix` / `ApiVersionSuffix` in `Directory.Build.props` | Adds owner-scoped performance and round details; not a deployment claim |
| Blazor website | `0.5.0-alpha`, configured by `AdminVersionPrefix` / `AdminVersionSuffix` | Admin and personal areas with performance and round detail views |
| Database setup tool | `0.2.0-alpha`, configured by `DatabaseSetupVersionPrefix` / `DatabaseSetupVersionSuffix` in `Directory.Build.props` | Independent of both API and database schema versions |
| Database | `0.2.0-alpha` after migrations 001–005 in `src/GolfSG.Database.Setup/Sql/Migrations` | Installed version must be read from the actual database |
| Local course-practice storage | `FileCoursePracticeRepository` reads schemas 1–4 | This range applies only to that format |
| Course sharing package | `CourseShareService` accepts schema 1 | Separate from local course storage |
| Active putting-game file | `FileActivePuttingGameRepository` uses schema 1 | Separate from app release version |

## Compatibility matrix

Start with feature requirements, then add exact tested release combinations.
Do not infer compatibility from matching version numbers or a successful build.

| App / feature | Required API | Required database | Status / evidence |
| --- | --- | --- | --- |
| Current source: local rounds and practice | None for local use | None for local use | Offline behavior described in README; release acceptance still required |
| Admin dashboard 0.1.0-alpha | API 0.2.0-alpha with configured OIDC, MFA and admin.read scope | Schema 0.2.0-alpha, migrations 001–005 and runtime role grants | Source requirement; actual identity-provider and deployment acceptance pending |
| Admin dashboard 0.2.0-alpha directories | API 0.3.0-alpha with configured OIDC, MFA and admin.read scope | Schema 0.2.0-alpha; no additional migration | Read-only local query checks pass; identity-provider/deployment acceptance pending |
| Admin dashboard 0.3.0-alpha password reset | API 0.4.0-alpha, reset scope, reset-operator allowlist and recent signed auth_time/MFA claims | Schema 0.2.0-alpha; no additional migration | Isolated local password/stamp test passed; identity-provider acceptance pending |
| Website 0.4.0-alpha personal area | API 0.5.0-alpha and existing ordinary app-account login | Schema 0.2.0-alpha; no additional migration | Two-account ownership checks passed on disposable PostgreSQL |
| Website 0.5.0-alpha performance | API 0.6.0-alpha, ordinary app-account login | Schema 0.2.0-alpha; no additional migration | Domain scoring, owner isolation and malformed-record checks passed |
| Current source: shared course library and join-code games | API containing course-library/game endpoints | Migrations 001–003 applied | Source requirement documented in `product/course-library-and-games.md`; no deployment or cross-release certification implied |
| Previously distributed app with current API | To be recorded | To be recorded | Unknown until tested with the actual distributed artifact |
| Current app with previous API | To be recorded per feature | To be recorded | Unknown; missing online features must not prevent local play |

For every release candidate, record a row containing exact app version/build and
commit, API version and deployed commit, installed database version and applied migration IDs, environment,
platform, test date, result, and a link to test evidence. Use explicit results:
**tested**, **incompatible**, or **unknown**. Requirements alone are not a tested result.
Only claim supported ranges after testing their boundaries and documenting the
policy that covers intervening releases.

At minimum, exercise the new app against the new API, the last supported app
against the new API, and the new app against the previous supported API. Cover
login, round sync, course download, game join/submission, and offline recovery.
Record feature-specific failures rather than declaring the whole app incompatible.

## Implementation sequence

The initial versions are now configured in `Directory.Build.props` without changing
the app version script or existing app tag format. To inspect built identities:

```powershell
dotnet msbuild GolfSG.Api/GolfSG.Api.csproj -getProperty:Version,InformationalVersion
dotnet msbuild src/GolfSG.Database.Setup/GolfSG.Database.Setup.csproj -getProperty:Version,InformationalVersion
```

To inspect an installed database with administrative read access:

```sql
SELECT version, applied_at FROM public.golfsg_database_release;
```

A missing release table means the database predates migration 004; treat its release
as unknown. Future database releases must add a migration updating the singleton
release row, and leave all already applied migrations unchanged. Running the normal
explicit setup command installs migration 004; API startup does not migrate databases.

Remaining work:

1. Add a central component manifest with independent app, API, database schema and setup versions.
   Keep store build numbers app-specific. Make build properties, release scripts
   and artifact names read that manifest; update Android's tag validation in the
   same change so there are no competing sources of truth.
2. Include component version and source commit in API deployment metadata and
   app diagnostics. Record actual applied migrations using the existing migration
   ledger and record the installed database release transactionally after verifying
   its migration manifest. Keep infrastructure credentials and connection strings out of metadata.
3. Define a stable API capability response with an explicit contract identity
   and feature identifiers. Use it during user-initiated connection/login/sync
   flows, respecting the existing no-background-polling behavior. Handle older
   servers without this endpoint as unknown and fall back per feature.
4. Declare which API contracts each app supports and which database schemas each
   API supports. Validate the database before serving affected features. Avoid
   requiring a particular app patch version when a contract/capability suffices.
5. Automate matrix validation in CI and attach exact tested combinations to
   releases. Require retained API contracts to pass integration tests; use actual
   previous release artifacts for cross-release checks.

Use additive database migrations first, deploy the compatible API, then release
the app. Remove old fields/endpoints only after explicitly ending support for
clients that depend on them. Database rollback is a separate decision from API
rollback: a previous API must be tested against the migrated schema before it
can be listed as a rollback option. Do not automatically downgrade local files or
discard files written by a newer app.

For shared games, persist the scoring/baseline identity with the frozen game
definition so later scoring changes cannot silently change comparisons.
