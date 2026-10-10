# Product roadmap

This is a directional roadmap, not a promise of dates. Priorities may change as real-device testing and golfer feedback reveal the most important problems.

See [the active backlog](../../TODO.md) for remaining work, [project ideas](ideas.md) for exploration, and [the completed archive](completed-backlog.md) for recorded completions.

## Now: alpha hardening

- Make the core putting round and practice flows reliable on Windows and Android.
- Verify history reliability and resolve remaining date contrast issues; history visibility has already been addressed.
- Finish consistent distance-unit behavior.
- Stabilize approach and around-the-green category configuration before presenting those areas as mature.
- Add accessibility semantics, stable automation identifiers, and a small UI smoke suite.
- Document the strokes-gained methodology and reference-data provenance.
- Complete production signing and physical-device acceptance using the existing CI and release packaging workflows.

## Next: actionable improvement

- Review [optional accounts and cloud sync for a small group](cloud-sync-idea.md), including free-tier feasibility, while preserving offline use. This is an idea for evaluation before implementation.
- Add trend views for total SG, category SG, distance bands, three-putt rate, and make percentage.
- Turn round results into coaching-oriented insights and practice recommendations.
- Validate putting-game autosave/resume on physical devices alongside the existing round recovery flow.
- Validate practice-result undo and editing on devices, including resumed and just-finished benchmarks.
- Validate the [beta course setup/play flow](course-practice-beta.md) on devices before expanding it to GPS maps, multiplayer or full-hole play.
- Add course name and an in-round scoreboard.
- Review personal baseline options from the ideas list before scheduling implementation.

## Later: continuity and coaching

- Evaluate practice recommendations first; keep goals, personal records, and streaks in the ideas list until prioritized.
- Provide shareable summaries for coaching conversations.
- Improve category-specific reference tables and lie modeling.
- Expand cloud synchronization only if the near-term feasibility review validates the offline data model, privacy expectations, and free-tier constraints.

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
