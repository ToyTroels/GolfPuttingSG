# Attributions

This file records the external technologies, assets, and data sources used by GolfSG. It should be updated whenever a release adds a new dependency or asset.

## Microsoft .NET and .NET MAUI

GolfSG is built on .NET and .NET MAUI and uses the standard Microsoft project structure and platform integrations. Their licensing information is available from the [Microsoft .NET repository](https://github.com/dotnet/runtime) and [dotnet/maui repository](https://github.com/dotnet/maui).

## MSTest

The test suite uses Microsoft's MSTest framework and related test tooling through NuGet packages. Package license information is available from the package metadata and the [MSTest repository](https://github.com/microsoft/testfx).

## Open Sans

The application includes Open Sans font files under `src/GolfSG/Resources/Fonts`. Open Sans is distributed under the SIL Open Font License. The required copyright and license text is included in [`OPEN_SANS_LICENSE.txt`](OPEN_SANS_LICENSE.txt).

## Application icons and template assets

The app icon and splash assets are maintained in the repository under `src/GolfSG/Resources`. Unused .NET MAUI template artwork is not included in the distributable application.

## Strokes-gained reference data

GolfSG uses project-maintained approximate reference tables and does not identify them as official PGA Tour or ShotLink data. The table locations, calculation scope, limitations, and required public-release provenance check are documented in [`docs/REFERENCE_DATA.md`](docs/REFERENCE_DATA.md).

## Dependency licenses

The authoritative list of package dependencies is defined by the project files and restored lock/assets files. Release automation should generate a complete third-party notice report when distributable packages are published.
