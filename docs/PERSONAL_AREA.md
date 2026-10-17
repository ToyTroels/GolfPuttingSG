# Personal account area

The Blazor website includes `/my` for ordinary GolfSG accounts. Users sign in
at `/my/login` with the same username and password as the mobile app. This flow
does not require Entra and grants no administrative privileges.

The pane shows:

- Saved rounds synced to the server, their IDs and last update times. Each can
  be downloaded as the API's JSON round snapshot, including the user's round data.
- Courses the user owns in the server course library, including unpublished
  entries, publication status, positions and attempts.
- Games the user hosts or has joined, with their own submitted SG when available.

Lists use pages of 25. Device-only data is not uploaded by opening this pane;
sync or publish explicitly from the app first. Deleted round tombstones are
excluded. The round list links to readable hole-by-hole or practice-attempt
details and the performance overview. These are read-only pages. JSON downloads are individual API snapshots rather than
the mobile app's bulk `rounds.json` export format.

## Performance and round details

Open `/my/performance` for a small performance overview or use View round in
history to open `/my/rounds/{id}`. The overview loads at most 30 most recently
updated, non-deleted synced records, then orders activity and trends by played
date. It is explicitly a window of synced history, not lifetime statistics.

Normal-round averages and the trend exclude course practice, putting games and
benchmarks. Averages include partial rounds with recorded results and are not
normalized per 18 holes. Category averages include only tracked categories with
recorded results, with coverage counts shown. Highest/lowest category observations
appear only when at least two categories each have three recorded rounds; these
are descriptive, not a claim that different round formats are directly comparable.

Round detail uses the existing GolfSG scoring summaries, tracking flags, archived
practice results and penalty counts. Repeated course attempts remain distinct.
Practice is explicitly labelled and kept out of normal-round aggregates. Missing
category results display as unavailable rather than fabricated scores. This is a
training/reference baseline, not an official handicap or tour statistic.

Unreadable or invalid historical records are excluded from the overview with a
visible count; their stored content is not modified. Detail returns unavailable
for unreadable, deleted or unowned records. API routes are GET
`/api/me/performance` and `/api/me/performance/rounds/{roundId}`, with the same
ordinary app-account authentication and no client-selectable account ID.

## Security

The API endpoint `GET /api/me/data/{rounds|courses|games}` obtains account identity
only from the validated ordinary app bearer principal. It accepts no account-ID
selector. Database queries restrict rounds and courses to their owner and games
to hosts/participants; game scores are projected only for the signed-in account.
Round downloads use the existing owner-filtered `/api/rounds/{id}` endpoint.

The browser gets a separate Secure/HttpOnly player-session cookie, backed by the
server-side ticket store. API access tokens stay in process memory and are not
included in HTML, URLs or browser storage. Sessions last at most 15 minutes;
refresh tokens are not retained. Each authenticated browser request revalidates
the API identity so password resets, revoked sessions and lockouts take effect.
API outages deny access rather than serving another account's cached data.

Login and logout validate antiforgery tokens; login is rate limited and failures
use generic messages. Personal responses and downloads use `no-store`. Player
cookies cannot authorize admin endpoints, and personal queries cannot choose
another account ID. No public registration or password-reset bypass is added.

## Local setup

This area can run with administration disabled and no Entra setup. Configure:

| Setting | Local value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `AdminAuth__Enabled` | `false` |
| `PlayerPortal__Enabled` | `true` |
| `PlayerPortal__ApiBaseUrl` | `https://localhost:7151/` |
| `AllowedHosts` | `localhost` |

Run the API on its trusted local HTTPS address, then run the Blazor project on
`https://localhost:7246`. Sign in at `https://localhost:7246/my/login`. Keep secrets
in the existing API secrets store; the website has no database credentials.

The sample preview at `http://localhost:7247/my` shows fictitious personal records
without signing in. It has no API client or database connection and does not show
real data. The overview links to this personal view.

Staging uses its own HTTPS API origin, exact allowed hostnames, and trusted reverse
proxy settings as documented for the admin site. Both areas currently require
Development or Staging; production enablement remains gated. The server-side ticket
store supports one instance; replace it with a protected shared store before scaling.

Version baseline: website `0.5.0-alpha`, API `0.6.0-alpha`, database unchanged at
`0.2.0-alpha`. No new migration is needed.

The performance trend plots total, putting, approach, and around-green SG separately on a shared scale. Missing or untracked category scores are null and break the line; exact per-round values are also shown below the chart. Practice remains excluded.

Dashboard preferences: users can choose SG lines and all available, last 30/90 days, or inclusive custom dates. Dates filter cards, category averages, recent activity and the trend within the 30-record synced window before limiting the chart to 12 rounds. Preferences persist for one year in an HttpOnly cookie scoped to the account and browser; they do not follow the user to other devices. Reset view restores all lines and all available dates. No database migration is needed.

Personal API caching: the website keeps successful directory, performance, and round-detail responses in a dedicated 32 MiB in-process memory cache for 30 seconds. Keys include the account, a hash of the access token, and resource path (including pagination). Concurrent ordinary reads share a fetch. Errors, sign-in checks, authentication decisions, exports and password-reset actions are not cached. Every protected page still validates the current token with the API before using cached data; revocation or an identity-service failure therefore denies access. Page responses remain `no-store` for browsers and proxies. Refresh data bypasses the relevant cache entry; failed refreshes do not fall back to stale data. Cached entries become inaccessible on logout and expire within 30 seconds. Each web instance has its own cache. Future browser writes must invalidate affected account data before redirecting.
