# GolfSG

GolfSG is an offline-first .NET MAUI app for tracking golf putting performance with strokes gained. It helps golfers record rounds and practice sessions, compare results with a general strokes-gained reference baseline, and identify where practice will have the greatest impact.

> **Status: 0.2.0-alpha**
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
- Provides history, filtering, editing, recovery, and import/export support.
- Import files one at a time to merge round history. Rounds are matched by ID; existing saved versions are kept, and repeated imports do not add duplicates. Copies with different IDs are treated as separate rounds.
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

| Project | Responsibility |
| --- | --- |
| `src/GolfSG` | .NET MAUI composition, pages, navigation, and platform integration |
| `src/GolfSG.Application` | Application services, view models, ports, and workflows |
| `src/GolfSG.Core` | Domain models, strokes-gained calculations, summaries, and game rules |
| `src/GolfSG.Infrastructure` | Local JSON persistence and storage recovery |
| `tests/GolfSG.Tests` | Core, application, infrastructure, and view-model tests |

See [ARCHITECTURE.md](ARCHITECTURE.md) for dependency rules and [TESTING_STRATEGY.md](TESTING_STRATEGY.md) for the testing roadmap.

## Scoring model

The putting calculation uses the reference table in [`StrokesGainedCalculator.cs`](src/GolfSG.Core/StrokesGainedCalculator.cs). Values between known baseline points use linear interpolation. Values below the lowest supported distance use the lowest baseline, and values above the highest supported distance use the highest baseline rather than extrapolating beyond the available data.

The methodology, limitations, and public-release provenance gate are documented in [docs/REFERENCE_DATA.md](docs/REFERENCE_DATA.md). GolfSG is a training and analysis tool; its results are not official tour statistics, a handicap, or a tournament scoring system.

## Data and privacy

GolfSG is designed to work without an account or an internet connection. Round and practice data is stored in the app's local data directory. The current application does not provide cloud synchronization, advertising, or an analytics account.

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
