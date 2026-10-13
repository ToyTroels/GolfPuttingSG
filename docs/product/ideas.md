# Project ideas

Ideas to evaluate before scheduling implementation. Moving an idea here preserves it without treating it as committed work. See [TODO.md](../../TODO.md) for the active backlog.

## Near-term review

- **Next session: easier course play:** focus on making the course-play flow simpler and more user friendly. Review starting and resuming a round, choosing positions, recording shots, and finishing a round before deciding which interactions to simplify.
- **Google sign-in and easier account sync:** evaluate optional "Continue with Google" and later Apple or similar providers, using the existing GolfSG backend to store and sync rounds. Allow existing users to link a provider to their account without losing history or creating duplicate accounts. Keep offline play without login, show sync status and pending changes clearly, and evaluate automatic sync when connected with retries that accommodate the server's slow wake-up. Provider sign-in identifies the user; it does not itself store or sync round data.
- **Optional accounts and cloud sync:** evaluate Neon Free + Render Free + Firebase Authentication Spark for a small group, retaining offline saves and allowing servers to sleep. Start with one authenticated round sync, then prove isolation between two accounts, reliable retries, and no duplicates. See the [detailed cloud-sync idea](cloud-sync-idea.md).
- **Bogey avoidance insight:** define expected versus actual bogey-or-worse outcomes using par, strokes to reach the green, putting, and penalties. Consider GIR/missed-green views and avoid claiming information missing from older rounds.
- **Personal baselines:** explore scratch/handicap reference options and the golfer's own recent average. Establish data provenance and enough observations before presenting comparisons as meaningful.
- **Practice recommendations:** use weak distance bands to suggest drills and repeat-distance sessions; evaluate whether recommendations help before adding more tracking features.
- **Wind-adjusted playing-distance calculator:** let the golfer enter distance to the hole, wind speed, and wind direction relative to the shot (headwind, tailwind, or an angle). Show an estimated playing distance ("plays like") alongside the actual distance and wind adjustment, with selectable distance and wind-speed units. Evaluate and validate the calculation model before implementation, including how to represent crosswind and whether shot trajectory or club inputs are needed for useful estimates. Keep manual entry usable offline and label the result as an estimate.
- **Custom shot challenges against other players:** let a user define the shots players should hit, including shot type, distance, lie where relevant, and number of attempts. Players follow the same challenge, record their results, and compare scores to see who wins. Evaluate scoring rules, ties, editing of challenge definitions, and whether the first version supports players sharing one device, playing separately, or both. Preserve offline play where practical; connect invitations and cross-device results to the optional accounts/cloud-sync idea. Keep private challenges separate from public leaderboards.

- **Course setup by clicking a map or picture (beta):** the initial offline setup using the supplied facility picture and single-shot round flow is implemented; see the [beta guide](course-practice-beta.md). Further exploration covers interactive satellite maps, GPS positioning, additional approved aerial pictures, course-library/image export and full-hole or multiplayer play. Let a user create a course or practice layout by selecting hitting positions and targets on a supported picture or future interactive map. Name and save the locations, then use them to define practice shots or custom challenges. Use manual distances or calibration against a known distance for supplied pictures; evaluate geographic coordinates for automatic map measurements. Explore native satellite maps and suitably licensed aerial pictures, checking Google Maps pricing, EEA satellite restrictions, attribution, and offline-use permissions before choosing a provider. Keep locally stored layouts and manual entry usable offline where image licensing permits. Evaluate this focused setup feature separately from a full GPS/course-management product.

## Lower-priority ideas

- **Suggested putt counts:** an editable suggestion based on first-putt distance and the selected benchmark. Preserve entered values and require confirmation so a suggestion never becomes an unrecorded result.
- **Optional pre-round conditions:** wind speed/direction, green speed, weather/temperature, and firmness/wetness, stored for later comparison. Implement course name separately first; coordinate fields if this idea proceeds.
- **Goals, personal records, and streaks:** revisit after useful practice recommendations and trends are available.
- **Change training-game length mid-session:** define how adding/removing remaining distances preserves already recorded putts before implementation.

## Outside the current scope

Social feeds/public leaderboards and a full GPS/course-management product remain outside the current scope unless validated. Cloud accounts should serve a clear continuity or backup need.
