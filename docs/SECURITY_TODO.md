# Security TODO

Open work from the independent security review on 2026-10-17. Reviewed branch: `codex/admin-user-access`, commit `2105d02b2035fd132d597dc745542e5409a57d4c`, against `dev` base `44d3975ef997969a199cee5f7505d985f9877020`.

These are review findings and follow-up tasks, not a security certification. The review found no confirmed super-admin authorization bypass. Code-confirmed findings below were established by inspection, not attacks against deployed accounts. Source line references describe the reviewed commit and may move as fixes land.

## Priority 2: confirmed availability vulnerabilities

### SEC-01: Separate administrative disablement from password lockout

- [ ] Introduce an explicit administrative disabled state, separate from temporary password lockout. Check that state and the security stamp on authenticated requests and refresh. Retain brute-force protection on password login.
- [ ] Verify that five incorrect password attempts cannot interrupt an administrator's existing valid session, while administrative disablement still blocks login, refresh, and existing sessions.

**Evidence:** `GolfSG.Api/Program.cs:81-85` rejects existing sessions whenever `IsLockedOutAsync` is true. `GolfSG.Api/Controllers/AuthController.cs:41-42,57-59,75` applies or checks the same lockout, and `src/GolfSG.Server.Infrastructure/ServiceCollectionExtensions.cs:36-38` sets five failures and a five-minute duration.

**Attack and impact:** An unauthenticated attacker who knows an administrator's username can submit five incorrect passwords to ordinary login, blocking that administrator's active access token and refresh. Repeating the attack after lockout expires can sustain disruption. The admin controller's protection against disabling provisioned administrators does not protect this path. The rejection of existing sessions is introduced by this branch.

### SEC-02: Apply rate limits before database-backed session validation

- [ ] Move applicable rate limiting after bearer authentication but before database-backed session validation. Keep security-stamp validation before authorization and endpoint execution.
- [ ] Assess a global request or concurrency limit for routes without an endpoint policy.
- [ ] Verify that over-limit requests do not reach the session-validation database lookup, including requests with revoked but unexpired protected access tickets.

**Evidence:** `GolfSG.Api/Program.cs:75-90` performs the security-stamp lookup before `UseRateLimiter`; invalid sessions return early, before reaching that limiter.

**Attack and impact:** A registered user can repeatedly send an ordinary token to account or admin endpoints and force database work even after exhausting the endpoint's rate limit. A revoked token whose protected ticket has not expired still triggers a lookup and returns 401 before rate limiting. This consumes database capacity outside the intended request budget. This validation ordering is introduced by the branch; production load impact was not measured.

## Priority 2: deployment-dependent availability issue

### SEC-03: Configure trusted proxy forwarding for account rate limits

- [ ] Verify the actual client IP and request scheme seen behind the deployed proxy, including any host-level forwarding overrides.
- [ ] Configure validated proxy trust and process forwarded client IP and scheme before HSTS, HTTPS redirection, and rate limiting. Do not trust arbitrary client-supplied forwarding headers.
- [ ] Verify that unrelated clients receive independent request budgets and spoofed forwarding headers cannot bypass limits. Separate ordinary account and admin budgets where appropriate.

**Evidence:** `GolfSG.Api/Program.cs:38-44` keys the shared 20-request-per-minute accounts policy on `Connection.RemoteIpAddress`. The tracked Docker/Render setup does not configure forwarded-header processing. `GolfSG.Api/Controllers/AdminUsersController.cs:14` adds admin requests to that policy.

**Attack and impact:** Under the shipped proxy configuration, unrelated clients reaching the same reverse proxy can share its request budget. An anonymous client can exhaust that budget and disrupt registration, login, refresh, and admin operations. The forwarding gap predates this branch; the new admin routes inherit it. Live impact is conditional because host overrides and actual proxy behavior were not inspected.

## Hardening recommendations

These items are distinct from the confirmed vulnerabilities above.

- [ ] **SEC-04: Add super-admin MFA.** Implement a flow that completes a second-factor challenge before issuing the admin claim. The current `CheckPasswordSignInAsync` followed by direct token issuance in `GolfSG.Api/Controllers/AuthController.cs:75-79` does not complete MFA; merely enabling a user's two-factor flag is insufficient.
- [ ] **SEC-05: Review admin session lifetime and logout revocation.** Consider shorter admin access-token lifetimes and server-side logout revocation. `GolfSG.Api/Controllers/AdminPageController.cs:41,75` clears page memory only, so a previously copied access token remains usable until expiry or revocation. The unmodified framework access-token default is one hour. Preserve the rule that refresh drops admin permission.
- [ ] **SEC-06: Restrict the production runtime database role.** Replace the owner runtime role recorded at `deploy/production.json:19` with a restricted application role and verify effective grants. Keep schema migrations under a separate owner identity. This pre-existing configuration increases compromise impact; the review did not establish an SQL injection vulnerability.
- [ ] **SEC-07: Protect Data Protection keys.** Verify deployment persistence, permissions, backup access, encryption at rest, and key sharing between instances. `GolfSG.Api/Program.cs:28-31` explicitly protects persisted keys with DPAPI only on Windows. Treat key material as credentials and keep it out of source, image layers, and public artifacts.
- [ ] **SEC-08: Restrict local-testing password overrides.** Prevent public deployments from enabling `Auth:AllowSimplePasswordsForLocalTesting`; verify Release build and effective host settings. The override in `src/GolfSG.Server.Infrastructure/ServiceCollectionExtensions.cs:26-35` can weaken ordinary account registration even in Release. Dedicated admin login has a separate strong-password gate.

## Verification still required

- [ ] **SEC-09: Add and run PostgreSQL-backed admin HTTP tests** against an explicitly disposable, prepared database. Cover anonymous denial; ordinary-token denial even for provisioned admins; dedicated admin login; refreshed-token denial for admin routes; removal of provisioning; invalid/expired tokens; persisted stamp revocation of both access and refresh tokens; disable/enable behavior; temporary lockout; and old-token rejection after re-enable.
- [ ] **SEC-10: Verify persistence and concurrency behavior.** Exercise permanent-lockout timestamp mapping, partial update failures, concurrent login/refresh/revoke/disable/enable operations, and ensure disabled accounts cannot regain access through races. The current admin account tests mock persistence and do not establish these guarantees.
- [ ] **SEC-11: Verify deployed controls.** Inspect effective secret/configuration settings without recording credential values, database grants, proxy trust, HTTPS redirect and HSTS responses, Data Protection key storage, and deployment approval protections. Render terminates public HTTPS; the app's HTTP container listener alone is not evidence of public plaintext access.
- [ ] **SEC-12: Verify browser behavior.** Exercise the admin screen in a real browser, including CSP enforcement, safe rendering of account values, cache behavior, memory-only token handling, logout, and failure paths. Source/string tests do not establish browser enforcement.
- [ ] **SEC-13: Scan Git history and release artifacts for secrets.** The review inspected current tracked files and workflows with targeted scans, not every historical revision or published artifact. Public API URLs, public resource IDs, and the user-secrets identifier are not credentials. Do not copy any discovered credential into this document or an issue.

## Baseline verification and protections to preserve

At the reviewed commit:

- 106 Release API tests excluding PostgreSQL passed; 19 deployment-guard tests passed. The backend NuGet audit reported no advisories, and the diff whitespace check passed.
- PostgreSQL integration was not run because no disposable test database was configured. Existing admin tests use synthetic principals and mocked account operations; they do not establish end-to-end HTTP authorization or database behavior.
- No committed credentials were found in the targeted current tracked-file/workflow inspection. This is not proof that all secrets exposure is absent.
- Admin authorization requires a server-provisioned immutable account ID and a dedicated login claim. Ordinary login does not add that claim; refresh rebuilds the principal without it.
- Preserve per-request stamp validation, disable/revoke semantics, and stamp rotation before enabling an account.
- Preserve `textContent` rendering, fresh CSP nonces, restrictive framing/connection policies, memory-only admin tokens, and `no-store` responses. No concrete XSS or CSRF exploit was found in the reviewed page flow.

## References

- [Super-admin setup and behavior](SUPER_ADMIN.md)
- [ASP.NET Core 10.0.12 password and lockout handling](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Core/src/SignInManager.cs)
- [ASP.NET Core 10.0.12 bearer-token defaults](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Security/Authentication/BearerToken/src/BearerTokenOptions.cs)
- [Trusted proxy and forwarded-header configuration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)
- [Render web-service networking](https://render.com/docs/web-services#connect-to-your-web-service)
