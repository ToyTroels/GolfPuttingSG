# Saved round sync

## One-time database setup

In pgAdmin, connect to **MauiAppDB** as **postgres** (or the database owner), open
Query Tool and run [001_saved_rounds.sql](../GolfSG.Api/Sql/001_saved_rounds.sql).
It adds `public.saved_rounds` and grants the existing `mauiapp_user` access.
It can be run again safely. It does not change `products` or delete existing data.
The API does not create tables or run migrations at startup.

## Try it from the app

1. Build the app and API with these changes, then start the API using its HTTPS profile.
2. Keep the same server address in **Indstillinger → Produkter**. For a USB phone,
   use the forwarding described in [Products API setup](PRODUCTS_API_SETUP.md).
3. Save a round or putting game as usual. Open **Historik → Synkroniser**.
4. The status shows how many records were sent, downloaded or deleted. Check
   `SELECT id, version, payload IS NULL AS deleted FROM public.saved_rounds;` in pgAdmin.
5. Stop the API, save another round and try sync. The saved history remains available.
   Start the API again and press **Synkroniser** to retry.

Saving rounds, games and benchmarks stays local. Sync is explicit, without polling
or uploads after each putt. Existing rounds are included on the first sync. The full
round JSON includes tracking options, hole/shot data, game/benchmark metadata and
completed course-practice results. Active unfinished sessions, course setup files,
image files and diagnostics are not uploaded. A course image used on another device
must also be available on that device.

## Retry, deletion and conflicts

Round IDs stay the same across retries. PostgreSQL stores one row per ID. Writes
check the version read from the server, returning **409 Conflict** if another write
happened in between. Retry reads the current server state first.

The app keeps synchronization hashes in `round-sync.json`, separately for each server
address. Local round changes are compared with the last acknowledged content. A failed
or interrupted upload remains eligible for the next sync, including after restarting
the app. A lost response cannot create duplicate rounds.

Deleting a local round records its ID durably before removing it from history.
The next sync replaces its server payload with a retained deletion (`payload = NULL`).
Server deletions are applied to unchanged local copies. A local round with unsynced
changes takes precedence, including when both copies changed. Downloaded changes are
applied only if the local copy has not changed while the request was running. When
restoring an exported round with the same ID, the restored local copy can be uploaded
again. Clearing app data or uninstalling is a local reset and does not delete server data.

Do not delete `round-sync.json` to fix a sync error: it contains offline deletion
records. An unreadable sync file blocks sync and deletion while preserving local
reading and saving. Back up the local history before repairing storage.

## API contract

| Request | Result |
| --- | --- |
| `GET /api/rounds` | All records, including retained deletions |
| `GET /api/rounds/{id}` | One record, or 404 |
| `PUT /api/rounds/{id}` | Create/update/delete with a version precondition, or 409 |

A write body contains `version` (a new UUID), `expectedVersion` (the current UUID,
or null for creation), and `round` (the complete round, or null to delete). The result
contains `id`, `version` and `round`. Repeating an identical version/body is idempotent.
Invalid payloads return 400; request bodies are limited to 2 MiB. Date and round detail
fields stay inside JSONB rather than being converted into a different round model.

This development API currently has no login or per-user isolation. All devices using
the same API share the same history. Keep this phase on your development connection;
add authentication and ownership before a shared deployment.
