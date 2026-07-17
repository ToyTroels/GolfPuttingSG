# Attributions

This file records the external technologies, assets, and data sources used by GolfSG. It should be updated whenever a release adds a new dependency or asset.

## Microsoft .NET and .NET MAUI

GolfSG is built on .NET and .NET MAUI and uses the standard Microsoft project structure and platform integrations. Their licensing information is available from the [Microsoft .NET repository](https://github.com/dotnet/runtime) and [dotnet/maui repository](https://github.com/dotnet/maui).

## MSTest

The test suite uses Microsoft's MSTest framework and related test tooling through NuGet packages. Package license information is available from the package metadata and the [MSTest repository](https://github.com/microsoft/testfx).

## Open Sans

The application includes Open Sans font files under `src/GolfSG/Resources/Fonts`. Open Sans is distributed under the SIL Open Font License. See the font project's license and notice files for the applicable terms.

## Application icons and template assets

The app icon and splash assets are maintained in the repository under `src/GolfSG/Resources`. The repository also contains assets inherited from the .NET MAUI template. Unused template assets should be removed before a stable public release.

## Strokes-gained reference data

The current putting reference table is maintained in [`StrokesGainedCalculator.cs`](src/GolfSG.Core/StrokesGainedCalculator.cs). Before a stable release, the project should record the exact source, publication, permissions, and any transformations applied to that table. Until that provenance is complete, the table should be treated as an implementation reference for the alpha product rather than an independently certified statistical dataset.

## Dependency licenses

The authoritative list of package dependencies is defined by the project files and restored lock/assets files. Release automation should generate a complete third-party notice report when distributable packages are published.
