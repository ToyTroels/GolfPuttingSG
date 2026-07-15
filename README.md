# Golf SG

.NET MAUI MVP til Visual Studio-suiten, bygget i C# med MVVM-lag og lokal lagring.

Appen tracker strokes gained putting pr. golfrunde mod en PGA Tour-baseline.

## Struktur

- `GolfSG.sln`: Visual Studio-løsningen.
- `src/GolfSG`: .NET MAUI-appen.
- `src/GolfSG.Core`: domænemodeller, PGA-baseline, interpolation og strokes gained-beregning.
- `tests/GolfSG.Tests`: MSTest unit tests for beregningerne.

## Funktioner i MVP

- Startskærm med ny runde og tidligere runder.
- Runde-input med 18 huller, første putt-afstand og hurtig putt-stepper fra 0 til 5.
- Beregnet SG pr. hul og total SG.
- Resultatskærm med total SG, SG pr. hul, gennemsnitlig første putt-afstand, total putts, 3-putt rate, bedste/værste hul og simpel analyse.
- Gem/rediger runder lokalt i appens datafolder via JSON.
- Halvfærdige runder understøttes: huller med `0` putts regnes ikke med i summary.

## Beregning

Baseline-tabellen ligger i `src/GolfSG.Core/StrokesGainedCalculator.cs`.

- Afstande mellem to baselinepunkter beregnes med lineær interpolation.
- Afstande under 0,3 m bruger laveste baseline.
- Afstande over 27,4 m bruger højeste baseline. Det er valgt i MVP'en, fordi tabellen ikke har nok datapunkter til troværdig ekstrapolation.

## Kør i Visual Studio

1. Åbn `GolfSG.sln` i Visual Studio.
2. Vælg startup-projektet `GolfSG`.
3. Vælg target, fx `Windows Machine` eller en Android-emulator.
4. Tryk Run.

Se `RUN_ON_PHONE.md` for agent-noter om at køre appen på en trådløst forbundet Android-telefon via ADB/MSBuild.

## Kommandoer

```powershell
dotnet restore GolfSG.sln
dotnet test tests\GolfSG.Tests\GolfSG.Tests.csproj
dotnet build src\GolfSG\GolfSG.csproj -f net10.0-windows10.0.19041.0
```

## Versionsstyring

App-versionen styres centralt i `Directory.Build.props`.

- `VersionPrefix` er den numeriske version, fx `0.1.0`.
- `VersionSuffix` er release-kanalen, fx `alpha`. Aktuelt er appen `0.1.0-alpha`.
- `ApplicationBuildNumber` er buildnummeret, som skal stige ved nye installerbare builds.
- Platformenes interne app-version holdes numerisk via `ApplicationDisplayVersion`, mens Indstillinger-siden viser release-labelen med suffix.

Vis den aktuelle version:

```powershell
.\scripts\Set-AppVersion.ps1 -Show
```

Bump versionen før en release. Scriptet hæver automatisk buildnummeret med 1 og beholder suffixet:

```powershell
.\scripts\Set-AppVersion.ps1 -Bump Patch
.\scripts\Set-AppVersion.ps1 -Bump Minor
.\scripts\Set-AppVersion.ps1 -Bump Major
.\scripts\Set-AppVersion.ps1 -Bump Build
```

Skift release-kanal:

```powershell
.\scripts\Set-AppVersion.ps1 -Suffix alpha
.\scripts\Set-AppVersion.ps1 -ClearSuffix
```

Sæt en bestemt version og build:

```powershell
.\scripts\Set-AppVersion.ps1 -Version 1.0.0 -BuildNumber 20 -ClearSuffix
```

Anbefalet release-flow:

1. Kør et version-bump med scriptet.
2. Kør tests.
3. Byg eller publish appen.
4. Commit ændringen i `Directory.Build.props`.
## Udgiv Android APK til OneDrive

Når appen skal udgives som en lokal Android APK, brug denne kommando fra repoets rod:

```powershell
dotnet publish src\GolfSG\GolfSG.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk -p:PublishTrimmed=false -p:RunAOTCompilation=false -p:AndroidLinkMode=None --no-restore
```

Den signerede APK bliver oprettet her:

```text
src\GolfSG\bin\Release\net10.0-android\publish\com.troel.golfsg-Signed.apk
```

Kopier den derefter til OneDrive:

```powershell
New-Item -ItemType Directory -Path 'C:\Users\Troel\OneDrive\GolfSG' -Force
Copy-Item -LiteralPath 'C:\Users\Troel\source\repos\GolfPuttingSG\src\GolfSG\bin\Release\net10.0-android\publish\com.troel.golfsg-Signed.apk' -Destination 'C:\Users\Troel\OneDrive\GolfSG\GolfSG.apk' -Force
```

Noter:

- `--no-restore` bruges, fordi restore tidligere ramte en NuGet-lock i `AppData`.
- `PublishTrimmed=false`, `RunAOTCompilation=false` og `AndroidLinkMode=None` bruges til en lokal installérbar APK, fordi trimmed/AOT publish tidligere fejlede i Android assembly processing.
- Kopiering til OneDrive kræver skriveadgang uden for repoet.
