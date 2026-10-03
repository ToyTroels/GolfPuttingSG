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

Before a public store release, the maintainer must record how each numeric table was created and confirm that GolfSG has the right to redistribute it. If that evidence is unavailable, replace the table with independently produced or appropriately licensed values and retain this general-reference wording.

The repository and application deliberately avoid identifying the current values as official tour statistics until that gate is satisfied.
