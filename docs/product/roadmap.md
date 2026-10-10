# Product roadmap

This is a directional roadmap, not a promise of dates. Priorities may change as real-device testing and golfer feedback reveal the most important problems.

## Now: alpha hardening

- Make the core putting round and practice flows reliable on Windows and Android.
- Resolve history visibility and date-styling issues.
- Finish consistent distance-unit behavior.
- Stabilize approach and around-the-green category configuration before presenting those areas as mature.
- Add accessibility semantics, stable automation identifiers, and a small UI smoke suite.
- Document the strokes-gained methodology and reference-data provenance.
- Establish repeatable CI and release packaging.

## Next: actionable improvement

- Validate the [beta course setup/play flow](course-practice-beta.md) on devices before expanding it to GPS maps, multiplayer or full-hole play.
- Add trend views for total SG, category SG, distance bands, three-putt rate, and make percentage.
- Turn round results into coaching-oriented insights and practice recommendations.
- Add autosave and a clear resume/abandon flow for interrupted sessions.
- Improve benchmark editing and review before saving.
- Add personal baselines and clearly explain when they are statistically meaningful.

## Later: continuity and coaching

- Add stronger practice feedback loops, goals, personal records, and streaks.
- Provide shareable summaries for coaching conversations.
- Improve category-specific reference tables and lie modeling.
- Evaluate optional cross-device synchronization only after the offline data model and privacy expectations are stable.

## Out of scope until validated

- Social feeds or public leaderboards.
- A full GPS/course-management product.
- Complex analytics that do not lead to a clear player action.
- Cloud accounts introduced merely to support features that can remain local.

## Prioritization questions

Before moving an item into implementation, confirm:

1. Does it help a golfer make a better practice or playing decision?
2. Can the input be completed quickly on a phone?
3. Can the result be explained without requiring statistical expertise?
4. Does it work offline and preserve user control of the data?
5. What is the smallest testable version of the outcome?
