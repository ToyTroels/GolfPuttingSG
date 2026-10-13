# CI and continuous delivery to staging

GitHub Actions runs CI on pull requests and pushes to `dev` and `master`.
Only a push to `dev` can enter the staging delivery job. Pull requests build and
test without database owner credentials or deployment hooks.

## What happens after a merge into dev

1. Run application/page tests, backend/PostgreSQL integration tests, dependency
   audits, platform builds and an API Docker build.
2. Build Android packaging-check artifacts for **GolfSG Staging**, using its
   distinct application ID and staging endpoint. These are test artifacts, not
   production-signed store releases.
3. After every CI job succeeds, validate the staging database URL and Render hook
   against the public identifiers in `deploy/staging.json`.
4. Restore and run `GolfSG.Database.Setup` at the validated commit, connecting
   directly to the staging database as its owner and granting the restricted
   runtime role access. A failed migration stops deployment.
5. Request a Render deployment with `ref` set to that same commit SHA.
6. Wait for `/api/deployment` to report the expected SHA and `/api/health` to
   report database readiness. A healthy previous deployment does not count as
   success. Timeout or deployment failure makes the job fail.

Dev runs and staging delivery are serialized; an active migration/deployment is
not cancelled by a newer push. A rerun skips an unnecessary Render deployment
when that exact commit is already healthy. Database setup remains idempotent.

## One-time Actions secrets

Add these repository secrets under **GitHub → Settings → Secrets and variables
→ Actions**, or as secrets in the `staging` GitHub environment:

| Secret | Value |
| --- | --- |
| `STAGING_DATABASE_SETUP_CONNECTION` | Direct Neon PostgreSQL URL for database `golfsg_staging`, role `golfsg_staging_owner`; disable connection pooling in Neon's Connect dialog |
| `STAGING_RENDER_DEPLOY_HOOK` | Deploy Hook URL from the `golfputtingsg-staging` Render service's Settings |

The database secret accepts Neon's `postgresql://...` URL and converts it to
Npgsql format internally. It requires TLS and rejects the runtime role, pooled
hostname, another database or another host. The hook must reference the staging
service ID; a production deploy hook is rejected before any migration runs.

Credentials stay outside Git and command-line arguments. Only the migration
process receives the owner connection. The API continues using the restricted
runtime connection configured privately in Render.

Keep Render **Auto-Deploy off**. GitHub Actions owns the order of migration,
deployment and verification; independent Render auto-deploy would bypass it.
The `staging` GitHub environment records deployments.

## Check a delivery

Open the repository's **Actions → CI** run for the merged commit. The final job
is **Migrate and deploy staging**. On success it reports the verified SHA.
The Android test packages are under **android-Staging-packaging-check**.

If CI fails, fix the failure and push a new commit. If secrets are missing, add
them and rerun the failed job. If database setup fails, correct the migration or
credentials; it is transactional and leaves uncommitted updates rolled back.
If Render fails, inspect the staging service's deployment logs. The previous
application may still be running; the workflow will not mistake it for success.

Migrations must remain compatible with the preceding API and installed mobile
apps. A deployment failure after a successful migration does not undo the schema
change. Applied migration files are immutable; add the next numbered migration.

## Production

This workflow deploys staging only. Production still follows
`codex/maui-products-api`; it does not change its branch, credentials or deployment
settings. Moving production to `master` and wiring production delivery is a
separate step. Production Android releases keep the existing version-tag workflow.

References: [Render deploy hooks](https://render.com/docs/deploy-hooks),
[Render commit environment variable](https://render.com/docs/environment-variables),
[GitHub Actions secrets](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets).
