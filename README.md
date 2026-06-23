# Golf Putting SG

.NET MAUI MVP til Visual Studio-suiten, bygget i C# med MVVM-lag og lokal lagring.

Appen tracker strokes gained putting pr. golfrunde mod en PGA Tour-baseline.

## Struktur

- `GolfPuttingSG.sln`: Visual Studio-løsningen.
- `src/GolfPuttingSG`: .NET MAUI-appen.
- `src/GolfPuttingSG.Core`: domænemodeller, PGA-baseline, interpolation og strokes gained-beregning.
- `tests/GolfPuttingSG.Tests`: MSTest unit tests for beregningerne.

## Funktioner i MVP

- Startskærm med ny runde og tidligere runder.
- Runde-input med 18 huller, første putt-afstand og hurtig putt-stepper fra 0 til 5.
- Beregnet SG pr. hul og total SG.
- Resultatskærm med total SG, SG pr. hul, gennemsnitlig første putt-afstand, total putts, 3-putt rate, bedste/værste hul og simpel analyse.
- Gem/rediger runder lokalt i appens datafolder via JSON.
- Halvfærdige runder understøttes: huller med `0` putts regnes ikke med i summary.

## Beregning

Baseline-tabellen ligger i `src/GolfPuttingSG.Core/StrokesGainedCalculator.cs`.

- Afstande mellem to baselinepunkter beregnes med lineær interpolation.
- Afstande under 0,3 m bruger laveste baseline.
- Afstande over 27,4 m bruger højeste baseline. Det er valgt i MVP’en, fordi tabellen ikke har nok datapunkter til troværdig ekstrapolation.

## Kør i Visual Studio

1. Åbn `C:\Users\Troel\Documents\Golf\GolfPuttingSG.sln` i Visual Studio.
2. Vælg startup-projektet `GolfPuttingSG`.
3. Vælg target, fx `Windows Machine` eller en Android-emulator.
4. Tryk Run.

## Kommandoer

```powershell
dotnet restore GolfPuttingSG.sln
dotnet test tests\GolfPuttingSG.Tests\GolfPuttingSG.Tests.csproj
dotnet build src\GolfPuttingSG\GolfPuttingSG.csproj -f net10.0-windows10.0.19041.0
```
