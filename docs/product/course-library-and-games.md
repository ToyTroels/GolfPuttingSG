# Shared course library and games with join codes

Status: implemented for beta course practice. The API deployment needs database migration `003_course_library_games.sql` before the new screens can use the server. Players complete the course independently, with a shared leaderboard.

## User experience

The course-practice menu adds **Banebibliotek**, **Opret spil**, **Deltag med kode** and **Mine spil** alongside existing local courses. Enable beta features in Settings to access this menu.

### Quick start

1. Save a local course, open **Spil bane → Offentliggør bane**, review the picture and disclosure, and press **Offentliggør i biblioteket**. Other signed-in users can find it in **Banebibliotek**, preview it, and choose **Gem kopi til offline spil**.
2. Choose **Opret spil**, select a saved course or downloaded library copy, enter a game name and create it. Copy its six-character code for the other players. Creating a game does not publish the course to the public library.
3. Players open **Deltag med kode**, preview the fixed settings and sharing disclosure, and join. In the game, choose **Spil eller fortsæt**; the host can also choose **Deltag og spil**.
4. Save and resume offline through **Fortsæt træningsrunde**. After saving the completed round, use **Indsend til spil** on its result screen, or **Mine spil → Indsend mit resultat**. History can reopen the saved result for another attempt after a failed submission.
5. Use **Opdater** to refresh a leaderboard. The host can **Luk spil** after reviewing the closure explanation. A closed game retains its leaderboard and rejects new joins and first submissions; retrying an already accepted identical submission is safe.

In the library, a user can search published courses, preview their picture and positions, and save a copy for offline play. A course card shows the course name, creator, number of positions and total attempts. Publishing is an explicit action on a saved local course; ordinary saves do not publish anything. Publishing shares the picture, calibration, positions, lies, categories, measured distances and attempt counts, without player results or unfinished input.

A host chooses a local or library course, names the game, reviews its positions and attempt counts, and creates it. Publishing to the public library is optional: a private local course can also be used for a game. The app returns a short, randomly generated join code, for example `H7K2Q9`, that the host can copy or share.

A participant signs in, enters the code, previews the game and joins. The app downloads the course and picture so the participant can complete the course independently, choosing positions in any order and saving or resuming as in current course practice. Joining again opens the existing participation rather than creating another entry. The host can join as a player through the same flow.

After finishing, the participant submits the result to the game. A successful upload adds it to the shared leaderboard. The result also stays in personal History. A result saved offline shows **Awaiting submission** until the player submits it with a connection. Saving to History and submitting to a game are separate operations with separate status.

Participants can refresh the leaderboard when they want. The first version does not require everyone to be online together or use a continuously live connection.

Leaderboard refreshes return names, submitted status, scores and ranks. **Vis/skjul slagresultater** fetches a selected player's outcomes only when first opened, with **Vis flere slag** loading subsequent pages of 100. Hiding and reopening already loaded outcomes makes no new request until the whole leaderboard is refreshed. My games returns compact summaries and checks local History once per page for pending results.

Joining returns the fixed course and membership without transferring its picture. Pressing Play reuses an existing membership and a verified, locally saved picture; a missing or corrupt picture is fetched once through the game's authorized binary image endpoint. This also reuses pictures previously downloaded from the library. Ordinary shot entry and saved-session recovery remain local.

## Fair scoring and fixed setup

- Every participant receives the same frozen course, calibration, shot settings and attempt counts. Game creation fixes these values; later library or local edits do not change the game.
- Disable the existing per-position attempt-count adjustment during game play. Ordinary personal practice keeps that feature.
- Allow one scored round per account per game, with save/resume and undo before submission. A submitted result is final in the first version; repeat submission of the same result is safe and does not create duplicates.
- Rank completed submissions by total strokes gained, highest first. Everyone must complete the same attempts before receiving a rank. Equal scores share a rank using a defined, consistent rounding precision.
- Show incomplete participation as **In progress** or **Awaiting submission**, separately from completed rankings. The first version shares completed outcomes, not each player's unfinished input.
- Recalculate scoring on the server from the game's fixed setup and submitted outcomes. Do not trust totals supplied by the app. Fix the scoring baseline version for the game so all submissions remain comparable after application updates.

## Ownership and visibility

Signed-in users publish courses, create games and participate using their existing accounts. Local personal practice continues to work without login.

Only a course's owner can publish a new revision or unpublish it. Downloaded copies remain editable local copies. Downloading the current revision explicitly creates a new copy when the content has changed; repeating an unchanged download preserves the existing copy and its local edits. Unpublishing hides future discovery while retaining the revisions needed by existing games and recorded results. Manage publication under **Banebibliotek → Mine delte baner**.

The game code permits discovery and joining; it does not grant host rights. Game details, participants and submitted results are visible to the host and joined participants. Ordinary private History remains private. Before joining, the preview explains that the participant's display name, submitted outcomes and score will be shared within that game.

The host can close a game. Closure stops new joins and submissions, preserves the final leaderboard and leaves local player progress available as personal practice. The server accepts a submission only while the game is open; a pending offline result cannot enter a game after closure. The close action explains this consequence.

## Persistence and architecture

Use the existing authenticated API and PostgreSQL database, adding dedicated storage rather than making users' private saved rounds publicly readable.

| Record | Purpose |
| --- | --- |
| Shared course | Owner, name, published status, current revision and searchable metadata |
| Course revision | Immutable validated course definition and image reference |
| Course image | Durable image bytes or durable object reference, content hash and image metadata |
| Game | Host, unique join code, name, open/closed status, frozen definition and scoring baseline version |
| Game participant | Game/account membership, local round association and submitted result state |
| Game submission | Validated complete outcomes, calculated scores and submission identity |

Images live as deduplicated binary data in PostgreSQL's `course_images`, independently of the API's temporary filesystem. Definitions are stored as JSONB. Each immutable course revision retains its picture reference. A game owns its frozen definition and image reference without requiring public publication. Downloaded image files are saved on each device for offline play.

New server workflows belong in Application, endpoints and authorization in the API, PostgreSQL entities and stores in Server.Infrastructure, and explicit new migrations in Database.Setup. The device Infrastructure layer owns the API clients, downloaded images and durable pending submissions. MAUI owns the library, game setup, join, game list and leaderboard screens.

Extend the active course-practice session with optional game/participant identity. Keep game metadata separate from editable personal course definitions and retain the frozen setup when resuming. Preserve the current single-active-session rule for the first version; attempting to start another game offers resume or explicit discard instead of replacing progress.

Use database constraints for unique join codes and one participant/submission per game/account. Submission retries must remain safe after a timeout, restart or lost response. Authentication determines the submitting account; requests cannot choose another participant's identity. Bound and validate uploaded pictures and definitions, and limit failed code lookups.

Existing personal round synchronization remains separate. It can upload a personal copy of a completed game round, but it must never publish that round to a leaderboard implicitly or bypass game submission rules.

## Database setup and verification

Apply the migration using the existing standalone setup tool and owner connection; the API never migrates on startup:

```powershell
dotnet run --project src/GolfSG.Database.Setup/GolfSG.Database.Setup.csproj -c Release
```

The setup tool also grants the configured application role access to the new tables. Deploy the updated API and rebuild the app after preparing the server database. No production migration or deployment is performed by the source change.

The database-efficiency follow-up changes the beta API responses to metadata-only joins, summary-only leaderboards and compact My games entries, adding binary game-image and paged player-result routes. Update the API and app together. This follow-up needs no migration beyond 003. See [the design review and measured query budgets](course-community-design-review.md).

```powershell
dotnet test tests/GolfSG.Tests/GolfSG.Tests.csproj -c Release
dotnet test tests/GolfSG.PageTests/GolfSG.PageTests.csproj -c Release
dotnet test tests/GolfSG.Api.Tests/GolfSG.Api.Tests.csproj -c Release
```

For PostgreSQL and authenticated HTTP integration cases, set `GOLFSG_TEST_DATABASE` to an explicitly disposable, prepared database named `golfsg_test_*`. These cases cover persistence across contexts, revision and picture recovery, account isolation, concurrent joins and submissions, frozen quotas, shared ranks, closure and lost-response retries. Page checks exercise search, sharing disclosure, offline download, fixed attempt controls and request cancellation. Physical-device checks remain necessary for native picture rendering, navigation, clipboard and touch interactions.

## Implementation stages

1. Shared library: publish, browse/search, preview, download, revision handling and offline copies.
2. Games: create from a local or library course, join by code, persist membership and resume independent play with fixed attempts.
3. Results: durable pending submission, server scoring, retry-safe upload, participant leaderboard and host closure.

Automated coverage includes two accounts seeing the same published course; an unrelated account being unable to edit it; library revisions leaving existing games unchanged; repeat joins/submissions creating no duplicates; fixed attempt counts; offline save and restart; server scoring of mixed shot categories and penalties; equal-score ranking; and game closure rejecting a pending result while preserving personal History.

## Existing foundation

The implementation extends reusable local course layouts, picture-inclusive file sharing, frozen course-practice sessions, independent per-shot outcomes, local recovery, authenticated account-owned round sync and PostgreSQL persistence. Course-practice local storage now writes schema version 4 to preserve optional game/account/server linkage; versions 1–3 remain readable. Personal round synchronization and explicit game submission remain separate workflows.

See [course practice](course-practice-beta.md) and [architecture](../../ARCHITECTURE.md).
