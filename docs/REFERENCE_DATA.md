# Reference data and calculation scope

GolfSG uses project-maintained reference tables to provide a consistent training comparison. The tables are not official PGA Tour or ShotLink data, are not endorsed by a professional tour, and must not be marketed as such.

## Intended use

- Results are directional training estimates, not an official handicap, tournament score, or personalized performance standard.
- Linear interpolation is used between stored distance points.
- Values outside the stored range are clamped to the nearest endpoint rather than extrapolated.
- Putting, approach, and around-the-green calculations use the same strokes-gained identity: expected strokes before minus one stroke minus expected strokes after, with penalties deducted where applicable.

## Table locations

- Putting: `src/GolfSG.Core/StrokesGainedCalculator.cs`
- Approach: `src/GolfSG.Core/StrokesGainedApproachService.cs`
- Around the green: `src/GolfSG.Core/StrokesGainedAroundGreenService.cs`

## Public-release provenance gate

### Expected birdies make-probability benchmark

`src/GolfSG.Core/PuttingMakeProbability.cs` reproduces the approximate rates supplied in the user's initial expected-birdies screenshot: 100% at 1 ft, 99.4% at 3 ft, 52.9% at 8 ft, 41.3% at 10 ft, 30.1% over 10 through 15 ft, 18.3% over 15 through 20 ft, 12.47% over 20 through 25 ft, and 5.45% beyond 25 ft. Short-putt rates are linearly interpolated between the 1/3/8/10 ft anchors; longer distances use the stated bands. This intentionally retains the coarse bands of that example, rather than claiming a precise continuous probability curve. These probabilities are independent of the expected-putts SG table.

The source screenshot attributed these rates to PGA Tour statistics, but did not establish a season or reproducible original dataset. Official stat definitions are available at https://www.pgatour.com/stats/detail/387 (20–25 ft); this confirms the metric, not the supplied numeric rates. Treat this as a project-maintained approximate benchmark until numeric provenance and redistribution rights are confirmed under the release gate below. Expected birdies sum make probabilities on completed GIR-marked holes, and actual birdies count their one-putts. Eagles and greens reached earlier than normal GIR remain unsupported.

Before a public store release, the maintainer must record how each numeric table was created and confirm that GolfSG has the right to redistribute it. If that evidence is unavailable, replace the table with independently produced or appropriately licensed values and retain this general-reference wording.

The repository and application deliberately avoid identifying the current values as official tour statistics until that gate is satisfied.
