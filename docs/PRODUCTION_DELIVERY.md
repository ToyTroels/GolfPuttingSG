# Production requires your approval

Feature branches are for development. Merge them into `dev` for automatic tests,
migrations and staging delivery. Once that version is ready, promote it to `master`.
Production Render follows `master` with Auto-Deploy **Off**.

Only pushes that change API build or delivery inputs enter production preparation
and approval. App-only and documentation-only pushes still run CI but skip API
migrations, deployment and the production approval request. The input paths are
defined in `scripts/api_changes.py` and shared with staging delivery.

The master CI run checks application/page tests, PostgreSQL integration tests,
dependency audits, the API container and Android packaging. Apple and Windows remain
disabled. A preparation job checks the GitHub protection rules and confirms that the
master API inputs match healthy staging; commits may differ in SHA or app/documentation
files while containing identical API inputs. The release summary names the exact production SHA.

The final job waits at the `production` GitHub environment. In **Actions → CI → Review
deployments**, select production and press **Approve and deploy** only when you want
that release. Rejection leaves production unchanged. The agent must leave this decision
to you; creating a commit or merging a branch does not approve deployment.

After approval, the job verifies the protections again, runs the versioned database
setup as its owner, deploys the exact approved SHA via the production Render hook,
and checks `/api/deployment` and `/api/health`. Failed database setup prevents the
deploy. A failed deploy does not undo a successful database migration, so migrations
must stay compatible with the previous API and installed apps.

## Account settings

- GitHub environment `production`: sole required reviewer `ToyTroels`; administrator
  bypass disabled; deployment branch rule `master`, type Branch. Prevent self-review
  stays off so the owner can approve their own releases.
- Keep `PRODUCTION_DATABASE_SETUP_CONNECTION` and `PRODUCTION_RENDER_DEPLOY_HOOK`
  as **environment secrets in production**, outside repository-wide secrets.
- The database secret is the direct PostgreSQL URL for `GolfSg` → `production` →
  `neondb`, owner `neondb_owner`, with TLS required. The helper rejects staging or
  pooled database connections and the wrong Render service.
- Render Auto-Deploy stays Off. Manual dashboard deploys and possession of its hook
  remain administrative ways to deploy, so ordinary releases must use GitHub approval.

Public identifiers are in `deploy/production.json`; no credentials are committed.
The runtime database role is recorded as currently configured. It remains
`neondb_owner` during this repair; separating it into a restricted runtime login is a
later change requiring a reviewed Render credential update.

## Initial repair

Commit `a0aafba` was automatically deployed while production still followed the feature
branch. The production database had migrations 001 and 002, while the new course/game
API also requires 003. The first approved release applies 003 transactionally. It adds
five new course/library/game tables and indexes without deleting existing accounts,
products or saved rounds. It is tested on an isolated copy before production approval.

## Stale or unprepared releases

The workflow stops if master changes while approval is pending, if staging is unhealthy,
if its API inputs differ from the release, or if the manual protection is removed. Review the
latest validated release instead. Dev and master runs are serialized without cancelling
an active migration or deploy.

References: [GitHub deployment approvals](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/review-deployments),
[Render deploy hooks](https://render.com/docs/deploy-hooks).
