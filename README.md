# GolfSG

GolfSG is an offline-first .NET MAUI app for tracking golf putting performance with strokes gained. It helps golfers record rounds and practice sessions, compare results with a general strokes-gained reference baseline, and identify where practice will have the greatest impact.

> **Status: 0.3.0-alpha**
>
> The core experience is usable and repository-side release hardening is in place, but this remains prerelease software. Public distribution still requires production signing, reference-data provenance approval, store metadata, and physical-device acceptance.

## What it does

- Records putting performance by hole, including first-putt distance and putts used.
- Calculates strokes gained per hole and for the complete round or session.
- Supports partial rounds and configurable round lengths.
- Provides putting games and benchmark sessions, including ladder-style practice.
- Automatically saves unfinished putting games and offers resume from the start page or putting-games menu, retaining shot order and entered results.
- Supports undo of the last submitted practice putt and review/edit of recorded putts during a game or from its completion screen.
- Includes beta course practice rounds: a menu lets you play a saved course or set up a new one. New courses use the supplied offline facility picture. Place and drag start/target markers, calibrate distances, save shot lies, layouts and attempt counts, then use the shot overview to choose remaining positions in any order. Each attempt has its own result and SG, with resumable input, undo and history. See [the course-practice guide](docs/product/course-practice-beta.md).
- Stores round and practice history locally as JSON.
- Includes an account-based course library and games joined with a six-character code. Publish picture-based courses, download offline copies, play a fixed game independently and explicitly submit results to a participant leaderboard. Requires the updated API and database migration 003; see [course library and games](docs/product/course-library-and-games.md).
- Provides history, filtering, editing, recovery, and import/export support.
- Import files one at a time to merge round history. Rounds are matched by ID; existing saved versions are kept, and repeated imports do not add duplicates. Copies with different IDs are treated as separate rounds.
- Use **Konto og login** on the start screen to sign in or create an account. In **Historik**, import a GolfSG `rounds.json` export, then choose **Synkroniser** to upload your local rounds to that account. Import and local play work offline.
- On a fresh launch, signed-out users see the login/signup screen first. Successful login or signup opens the main page; a remembered account is restored locally and continues immediately. Opening the app, returning from the background and saving the server address make no API or database calls. **Fortsæt uden login** opens the local app immediately.
- Login, manual connection checks and sync allow two minutes per request for an API that takes about a minute to wake. Login and connection checks show a wait message and **Annuller**. No background polling or automatic retries keep the server awake; unchanged rounds are not uploaded again.
- Keeps scoring and statistics in testable, platform-independent .NET projects.

The current product focus is putting. Approach and around-the-green tracking are being expanded and should be considered experimental in this alpha release.

## Supported development targets

The project is configured for:

- Windows
- Android
- iOS
- Mac Catalyst

CI compiles every configured platform and validates Android and Windows packaging. Production signing and store/device acceptance are platform-owner release gates.

## Run locally

Requirements:

- Visual Studio with the .NET MAUI workload, or an equivalent .NET SDK setup.
- A configured Windows target or mobile emulator/device for the chosen platform.

From the repository root:

```powershell
dotnet restore GolfSG.sln
dotnet test tests\GolfSG.Tests\GolfSG.Tests.csproj
dotnet build src\GolfSG\GolfSG.csproj -f net10.0-windows10.0.19041.0
```

To run the app, open `GolfSG.sln` in Visual Studio, select the `GolfSG` startup project, choose a target, and start debugging. See [RUN_ON_PHONE.md](RUN_ON_PHONE.md) for Android device notes.

## Build Android packages

For a separate **GolfSG Staging** installation connected to its own API and
database, follow [staging setup](docs/STAGING_SETUP.md).

An ordinary local Release APK may use a development signing identity and must not be distributed. Production Android releases are built as both AAB and APK files by the guarded release workflow. Follow [docs/ANDROID_RELEASE_SIGNING.md](docs/ANDROID_RELEASE_SIGNING.md) and verify the certificate fingerprint before installation or upload.

For an unsigned packaging check:

```powershell
dotnet publish src\GolfSG\GolfSG.csproj `
  -f net10.0-android `
  -c Release `
  -p:AndroidPackageFormats=aab%3Bapk
```

The output is written below `src\GolfSG\bin\Release\net10.0-android\publish`.
## Project structure

For the local ASP.NET Core API and PostgreSQL database, follow
[Local database setup](docs/LOCAL_DATABASE_SETUP.md), then
[Products API setup](docs/PRODUCTS_API_SETUP.md) and
[Round sync setup](docs/ROUND_SYNC_SETUP.md).

| Project | Responsibility |
| --- | --- |
| `src/GolfSG` | .NET MAUI composition, pages, navigation, and platform integration |
| `src/GolfSG.Application` | Application services, view models, ports, and workflows |
| `src/GolfSG.Core` | Domain models, strokes-gained calculations, summaries, and game rules |
| `src/GolfSG.Infrastructure` | Device JSON persistence, storage recovery, and HTTP API clients |
| `GolfSG.Api` | HTTP endpoints, account authentication, authorization, response mapping, and server composition |
| `src/GolfSG.Server.Infrastructure` | PostgreSQL persistence, ASP.NET Core Identity storage, EF Core mappings, and server storage implementations |
| `src/GolfSG.Database.Setup` | Explicit versioned database updates and application-role grants |
| `tests/GolfSG.Tests` | Core, application, infrastructure, and view-model tests |
| `tests/GolfSG.Api.Tests` | API responses, authentication boundaries, account isolation, and PostgreSQL integration |

See [ARCHITECTURE.md](ARCHITECTURE.md) for dependency rules and [TESTING_STRATEGY.md](TESTING_STRATEGY.md) for the testing roadmap.

See [component versioning and compatibility](docs/COMPONENT_VERSIONING.md) for the current version inventory, initial feature requirements matrix, and proposed independent release policy.

A separate [Blazor admin dashboard](docs/ADMIN_DASHBOARD.md) provides a staging overview with explicitly approved administrators. It supports GolfSG password login for initial deployment or OpenID Connect with mandatory MFA, and is disabled until configured.

The website also has a separate [personal account area](docs/PERSONAL_AREA.md) for ordinary users to view their own synced rounds, courses and games using their existing GolfSG account.

## Scoring model

The putting calculation uses the reference table in [`StrokesGainedCalculator.cs`](src/GolfSG.Core/StrokesGainedCalculator.cs). Values between known baseline points use linear interpolation. Values below the lowest supported distance use the lowest baseline, and values above the highest supported distance use the highest baseline rather than extrapolating beyond the available data.

The methodology, limitations, and public-release provenance gate are documented in [docs/REFERENCE_DATA.md](docs/REFERENCE_DATA.md). GolfSG is a training and analysis tool; its results are not official tour statistics, a handicap, or a tournament scoring system.

## Data and privacy

Production builds, including Debug, always use `https://golfputtingsg.onrender.com/`.
Users cannot configure the endpoint. Owners select a separate staging endpoint at build time.
See [super-admin account access](docs/SUPER_ADMIN.md) for deployment configuration and user administration.

GolfSG's scoring and local history work without an account or an internet connection. Optional manual sync to the configured API requires a username/password account. The server separates rounds and retained deletions by the authenticated account. The app stores tokens in platform secure storage and binds an installation's sync history to the first signed-in account and server. Signing out leaves local history available and preserves that binding. A deployment endpoint change signs out and resets the binding and sync tracking, keeping local rounds available for manual upload after signing in on the configured server. There is no advertising or analytics account.

See [PRIVACY.md](PRIVACY.md) for the current data-handling statement and [ATTRIBUTIONS.md](ATTRIBUTIONS.md) for third-party assets and data notes.

## Versioning and releases

The version is maintained centrally in `Directory.Build.props`:

```powershell
.\scripts\Set-AppVersion.ps1 -Show
.\scripts\Set-AppVersion.ps1 -Bump Patch
```

Release history is maintained in [CHANGELOG.md](CHANGELOG.md). The current release notes are [v0.3.0-alpha](docs/release-notes/v0.3.0-alpha.md). Use the [release checklist](docs/RELEASE_CHECKLIST.md) before tagging or distributing a package.

## Product documentation

- [Product brief](docs/product/product-brief.md)
- [User stories](docs/product/user-stories.md)
- [Roadmap](docs/product/roadmap.md)
- [Architecture](ARCHITECTURE.md)
- [Testing strategy](TESTING_STRATEGY.md)

## License

GolfSG is released under the [MIT License](LICENSE). Review [ATTRIBUTIONS.md](ATTRIBUTIONS.md) for dependencies, fonts, template assets, and baseline-data notes.
