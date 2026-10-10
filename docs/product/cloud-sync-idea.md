# Project idea: optional accounts and cloud sync

Status: accounts and cloud hosting are proposed for near-term review; implementation is not scheduled.

The local PostgreSQL API and manual saved-round synchronization are implemented and have been verified through IIS over HTTPS with the Android Debug app. The current API shares one round collection. The next cloud milestone adds sign-in, ownership enforcement, and hosted deployment to this existing foundation. See [round sync setup](../ROUND_SYNC_SETUP.md) and [local IIS setup](../PRODUCTS_API_SETUP.md#local-iis).

## Goal

Let a small group of golfers keep their own rounds in the cloud and sync across devices, while retaining immediate local saves and offline use. Target a zero-cost pilot within the providers' free-tier limits.

## Candidate setup

- Neon Free: shared PostgreSQL database, with every round assigned to its owner.
- Render Free: a small ASP.NET Core API deployed in Docker. Database credentials stay on the server.
- Firebase Authentication Spark: email/password login; the API verifies authentication tokens and enforces ownership on every request.

These are candidate services, not a final decision. Recheck quotas, billing behavior, native-app login integration, regions, and provider terms before committing. Render Free is intended for hobby/testing use and sleeps after 15 minutes without incoming requests; waking can take about a minute. It also limits bandwidth, builds, and unusually high outbound traffic, including external database traffic.

References: [Neon plans](https://neon.com/docs/introduction/plans), [Render Free](https://render.com/docs/free), [Firebase pricing plans](https://firebase.google.com/docs/projects/billing/firebase-pricing-plans).

## Sync design

- Save locally first and track pending uploads durably.
- Sync after saving a round, when opening the app, or through an explicit Sync action.
- Batch pending changes instead of sending a request for each putt.
- Avoid continuous polling, persistent connections, and keep-alive requests so Render and Neon can sleep independently.
- Show sync status without blocking round entry. Retry failures with increasing delays and retain pending changes until server acknowledgement.
- Preserve existing round IDs and make repeated uploads idempotent, preventing duplicate rounds.
- Store the existing round payload as JSON alongside user ID, round ID, version, and update metadata initially.
- Establish how existing local rounds become associated with an account, and isolate local data when switching accounts.
- Define conflict handling and deletion syncing before expanding the pilot. Review privacy documentation and account/data deletion requirements.

## First milestone

One user signs in, saves a round locally, and syncs it to Neon through the API. Extend the existing API and manual sync with login and round ownership, configure Neon, and deploy to Render.

Validate with two accounts that users cannot access each other's rounds, offline saves survive restarts, retries do not duplicate records, and pending changes sync after the server wakes. Continue with editing conflicts and deletion syncing before inviting a wider group.

## Review questions

- Is cloud backup or cross-device continuity the main benefit for the first users?
- Are free-tier sleep delays acceptable for background syncing?
- Can the whole pilot stay at zero cost, including authentication and hosting, without unexpected overage charges?
- Is the local data model stable enough to introduce versioned sync?

This idea complements the earlier [backend database suggestion](../../BACKEND_DATABASE_SUGGESTION.md), which proposed SQLite; evaluate Neon as an alternative suited to hosting without persistent local disk.
