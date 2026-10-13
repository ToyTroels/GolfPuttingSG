# Course library and games: design and database efficiency review

Reviewed: 2026-10-13. The initial review did not change application code. The four main findings were subsequently implemented; all source changes remain uncommitted. The original measurements and findings below are retained as the baseline.

## Follow-up implementation

The leaderboard now projects only summary fields; submitted outcomes are fetched through an authorized per-player endpoint in pages of 100, sliced inside PostgreSQL. Submission uses a dedicated course/member context rather than the leaderboard. My games is a single joined summary query, and the app looks up pending round IDs with one History read per page, or no read when all items are submitted.

Joining now returns metadata and a round ID without a picture. Play reuses known membership instead of joining again. A separate authorized binary endpoint fetches a picture only when its content-hashed local file is missing or corrupt. Saved pictures from library downloads are reusable. Local actions, including copying an already displayed code, do not refresh authentication unnecessarily.

| Operation | Original DB commands | Optimized DB commands |
| --- | ---: | ---: |
| Leaderboard, non-host participant | 3 | 2 |
| Leaderboard, host | 2 | 2, without shot-result JSON |
| First completed-result submission | 6 | 4 |
| My games page | 2 | 1, without definitions or result JSON |
| First join | 5, with picture | 4, metadata only |
| Repeat join, if actually needed | 3, with picture | 2, metadata only |
| Start/resume known membership with cached picture | 3 for the repeated join | 0 community DB commands |
| Picture cache miss | Included in every join | 1 separate binary-image query |
| Player's requested shot-detail page | Included in every leaderboard | 1 query, up to 100 outcomes |

The optimized command budgets and authorization checks passed against the disposable PostgreSQL database. Transaction bookkeeping and token refresh are excluded as in the baseline. Tracing the normal uncached new-player flow now gives eight commands across four requests: preview (1), join (4), game summary (2), picture (1). A valid cached picture reduces that flow to seven commands across three requests. These flow totals are composed from verified endpoint counts and the app's call paths.

A representative 100-player, 1,000-attempt scoreboard serialized to 34,352 uncompressed bytes in the new summary-size regression check, compared with approximately 19.4 MB for the original result-inclusive scenario. The small fixtures' metadata differs slightly; these sizes describe the same supported player/attempt limits, rather than a byte-identical before/after dataset. Tests also verify that toggling already opened shot details does not refetch them, My games scans History at most once, and a cached picture is reused across a workflow restart.

The changes do not require another database migration. The beta API response shapes changed, so rebuild the app and deploy the updated API together. No hosted API or application database was changed. Lower-priority creation-lock, duplicate publication, image-upload/reference, search-index and full image-decoding observations remain follow-up work.

## Assessment

The foundation is suitable for a small beta: courses have immutable revisions, games freeze the setup, accounts own membership, scores are calculated on the server, and retries and closure are protected by database transactions. Ordinary shot entry, save/resume and local completion make no database calls. There is no continuous leaderboard polling.

The original database calls were not minimal. The largest problem was unnecessarily loading and transmitting complete results and pictures, rather than the raw number of commands. The first four findings are resolved by the follow-up above; the original findings below explain the changes.

## Measurement method

A temporary audit program ran the existing PostgreSQL store and application submission service against a fresh disposable PostgreSQL 18 database prepared by the normal setup tool. An EF Core command interceptor counted executed database commands. Each operation used a new database context. Counts exclude transaction begin/commit/savepoint bookkeeping and account sign-in/token refresh; a command can contain batched SQL statements. These are not production latency or load-test results.

Payload sizes were measured with the existing source-generated JSON serializer. The large synthetic game used the supported limits of 100 participants and 1,000 attempts each, with representative valid outcomes. The figures are uncompressed JSON sizes; they are examples at those limits, not absolute maximum response sizes.

| Operation | Measured DB commands |
| --- | ---: |
| Browse one library page | 1 |
| Download a course revision and picture | 3 |
| Publish a new course | 6 |
| Republish identical content | 4, including an update to its timestamp |
| Create a new game using an already stored picture | 5 |
| Preview a join code | 1 |
| Join for the first time | 5 |
| Join again as an existing participant | 3 |
| Open/refresh leaderboard as host | 2 |
| Open/refresh leaderboard as another participant | 3 |
| Load one page of My games | 2 |
| Submit a participant's completed result | 6 |
| Retry an identical accepted submission | 5, with no result update |
| Close an open game as host | 2 |

Tracing the app's successful new-player flow gives four community HTTP requests and 12 database commands: preview (1), join (5), open the game page (3), then join again on Play (3). A participant submitting from the game page triggers six submission commands followed by three more for the leaderboard reload. Additional authentication requests occur only when the stored token needs refresh or the user signs in.

| Representative response | Uncompressed bytes |
| --- | ---: |
| Leaderboard with all 100 players' 1,000 shot results | 19,427,262 |
| Same game and players, without individual shot results | 37,562 |
| Join response containing the allowed 10 MiB picture | 13,982,362 |

## Findings, in priority order

### 1. P1: Every leaderboard refresh reads and sends every shot result

[PostgresCourseCommunityStore.GetGameAsync](../../src/GolfSG.Server.Infrastructure/Persistence/PostgresCourseCommunityStore.cs) materializes entire participant entities, including `ResultsJson`, then deserializes and returns all results. The page only creates shot-detail controls when expanded, but this does not defer the database read, response serialization, transfer or app deserialization.

At the supported player/attempt limits, the measured response was about 19.4 MB; removing individual outcomes reduced the same response to about 38 KB. This is the highest-impact optimization.

Return a leaderboard summary with player names, completion state, total SG and rank. Load a selected player's outcomes only when requested, ideally with pagination. Preserve host/member authorization on the detail endpoint. Avoid selecting the JSONB result column in the summary query.

### 2. P2: Joining throws away the downloaded picture and Play downloads it again

[CourseGameJoinPage](../../src/GolfSG/Views/CourseGamePages.cs) discards the `JoinedCourseGame` returned by the first join. `CourseGamePage` calls Join again when the user presses Play. The store always reads and returns the picture on both calls, even for an existing participant. The same behavior applies when entering an already joined game to resume via My games.

This adds three database commands and one full picture transfer to the initial flow. With a 10 MiB picture, the two join responses together contain roughly 28 MB of uncompressed JSON. The existing content-hashed file names make picture reuse feasible, but the join flow does not check the local cache before fetching.

Keep and persist the first joined package, or return lightweight membership plus an immutable image identity and download the image only on a cache miss. A binary image endpoint avoids base64 expansion. Account/server scope must remain attached to cached participation. Keep membership and closure checks; they should not require downloading the picture.

### 3. P2: Submission loads the whole leaderboard before validating one player

[CourseCommunityService.SubmitAsync](../../src/GolfSG.Application/Courses/CourseCommunityService.cs) calls `GetGameAsync` to obtain the fixed course and scoring version. That also reads, deserializes and ranks every participant's existing results. The store then reads the game and submitting participant again under its transaction lock.

For a non-host participant, a first submission uses six commands, and an identical retry uses five. The extra complete-result read is more costly than those counts suggest, especially late in a game when most players have submitted.

Add a dedicated, lightweight submission context containing the frozen course, scoring version and authenticated participant identity. Keep final round-id, closure and duplicate-result checks atomic. Do not route result submission through the leaderboard workflow. Validate cheap request bounds before loading large data.

### 4. P2: My games reads unused result JSON and repeatedly scans local History

[PostgresCourseCommunityStore.MyGamesAsync](../../src/GolfSG.Server.Infrastructure/Persistence/PostgresCourseCommunityStore.cs) selects full participant entities solely to obtain the round ID and whether a result exists. The audit confirmed that its SQL selects the `results` column. It also returns full game definitions where the list uses only title, code and state.

The app then calls `FindRoundAsync` once per joined list item. [RoundFileStore.GetRoundAsync](../../src/GolfSG.Infrastructure/Persistence/RoundFileStore.cs) rereads, parses and sorts the complete History file on every call. A full list page can therefore scan History 30 times. This is local disk work, not additional database queries.

Use a small My-games response and project the submitted flag in SQL without fetching result JSON. A left join can combine game and membership summaries into one command. Read local History once per list load and look up round IDs in a dictionary; skip local checks for already submitted items if the UI does not need them.

### 5. P2: Game creation serializes unrelated games and retransmits stored images

`CreateGameAsync` takes one global advisory lock for every game creation. It holds that lock through image insertion and the final save, serializing different users creating unrelated games. A game-row lock is appropriate for same-game joins/submission/closure; a global lock across all game creations is a scaling bottleneck.

The create request also always includes image bytes. `EnsureImageAsync` sends those bytes to PostgreSQL even when `ON CONFLICT DO NOTHING` finds that the picture already exists. Stored-image deduplication saves storage, but does not avoid app-to-API or API-to-database transfer.

Prefer a lock scoped to the retry/request ID and handle rare code collisions through the existing unique constraint with bounded retries. Allow game creation to reference an authorized library revision or existing owned asset so an unchanged picture need not be uploaded again. Keep definition freezing and ownership checks.

## Other design checks

- Library browsing is already a single paged query. Download authorization, revision and image lookup currently use three commands; an authorized join/projection could combine them, but prioritize the large-result and duplicate-image fixes first.
- Identical publication retries safely reuse the revision, but still update `UpdatedAt`, changing library order and causing a write. Return unchanged published content without writing unless republishing an unpublished course is intended.
- The public/date and account indexes cover the main lookup patterns. The case-insensitive substring name search has no matching search index; examine query plans and add a suitable search index as the collection grows. Do not infer production performance from a tiny test database.
- `CourseCommunityPage.RunAsync` obtains an authenticated session even for local actions such as copying an already displayed code or opening a saved result. Usually this performs local session reads; with a nearly expired token it can trigger a network refresh and block an otherwise local action. Separate local actions from authenticated server actions.
- Package validation only checks the PNG/JPEG signature and byte limit. The audit's eight-byte PNG-signature fixture is accepted despite not being a decodable picture. Validate actual image structure and decoded dimensions before public publication, with valid image fixtures in tests.
- The scoring-version identifier is a useful foundation. Future baseline changes need an explicit versioned scorer strategy so previously created games keep their original scoring rules.
- PostgreSQL binary picture storage is a reasonable beta choice. There is no need to replace it just to eliminate redundant image calls; first improve the API and client cache behavior.

## Recommended follow-up

The main query/payload changes and their regression checks are implemented in the follow-up above. Account isolation, fixed quotas, duplicate submission and closure checks continue to pass. Remaining review items are narrower creation locks, image-reference uploads, unchanged-publication writes, search plans, full image validation and future scoring-version compatibility.

No application database or hosted API was touched by this review. The disposable audit server was stopped after measurements.
