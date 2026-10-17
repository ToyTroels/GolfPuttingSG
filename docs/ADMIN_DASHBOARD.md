# Blazor admin dashboard

`src/GolfSG.Admin` is a separate .NET 10 Blazor Web App, version `0.5.0-alpha`.
The first release uses static server rendering: no JavaScript or live circuit is
needed for this read-only overview. It shows environment, API/admin versions,
installed database version, deployed API commit, and explicitly uncertified
compatibility status. Refresh is manual; there is no background polling.

Users, Courses and Games provide search, pages of 25 records, and expandable
details. Users show account IDs, usernames and lockout status. Courses show
creator, publication status, positions, attempts and update time. Games show host,
open/closed status, participant/submission counts, scoring version and creation
time. Password/security fields, tokens, join codes, pictures and raw results are
excluded. The directory queries remain read-only. Users have a separate,
explicitly privileged password-reset flow described below.

GET `/api/admin/directory/users`, `/api/admin/directory/courses` and
`/api/admin/directory/games` use the same OIDC/MFA/`admin.read` policy as status.
Search is capped at 100 characters, page size at 100, and offset at 100000.
Invalid parameters are rejected. Queries use read-only projections and
parameterized filters, fetching one extra row to detect another page. Directory
access is audited by actor, section and offset, without search terms or returned
account data in the audit event. Keep gateway request logs from collecting search
query strings; the dashboard suppresses routine HTTP client/request logging.

The dashboard is disabled by default and currently permits only Staging and
Development environments. It cannot be enabled in Production. It is not deployed
automatically by the existing API workflow. The API is `0.6.0-alpha`; setup remains
`0.2.0-alpha`; database migration 005 establishes schema `0.2.0-alpha` and setup
grants the runtime role SELECT access only to the release metadata table.

## Access model

The website also hosts a separate [personal account area](PERSONAL_AREA.md) at
`/my`, with its own cookie, ordinary app-account login and owner-scoped API queries.
Enabling this pane does not enable administrator login or grant admin permissions.

### Password resets

Under Users, expand an account and choose **Reset password**. The operator must
type the current username, enter and repeat a replacement password, and confirm
that current app sessions will be revoked. Passwords must have 12–128 characters,
uppercase, lowercase, a number and a symbol. The API enforces this even when
ordinary local-testing password rules are relaxed. Protected legacy administrator
accounts cannot be reset through this action.

This is an administrator-set replacement password, not an emailed recovery link.
It does not automatically force a password change on next login or clear lockout.
The operator must communicate it using an approved private channel; the application
does not send messages. Passwords and internal reset tokens are not returned in
URLs, HTML, audit events or API responses. The success receipt is short-lived,
bound to the operator and account, and consumed once.

The API requires all ordinary admin-read checks plus:

- Delegated scope `admin.users.password-reset`.
- The actor’s immutable ID in API configuration
  `AdminAuth__PasswordResetSubjects__0` (additional indices for other operators).
- A signed access-token `auth_time` claim showing sign-in within five minutes.
  Missing, stale or implausibly future timestamps deny the operation. `iat` is not
  substituted for authentication time.

Expose and grant the dedicated reset scope in the identity provider, and configure
the full scope URI as dashboard `AdminAuth__PasswordResetScope`. Configure the
provider to issue authoritative `auth_time` in the access token as well as the
required MFA evidence. The Sign in again action requests `prompt=login` and
`max_age=0`; the API still independently checks the returned claims. If the chosen
provider cannot emit this evidence, resets remain denied rather than weakening
the requirement. Normal read-only administrators receive no reset permission by
default. Do not grant this scope or allowlist entry broadly.

GET `/api/admin/password-resets/access` checks access; POST
`/api/admin/password-resets/{id}` accepts `confirmUsername` and `newPassword`.
The API creates and consumes an Identity reset token internally. A successful
Identity reset updates the password hash and security stamp together, invalidating
existing app access and refresh tokens through the existing stamp checks. Audit
logs record actor, account and operation result without password/token values;
retain these logs in the deployment’s protected log sink. Network timeouts can
leave an uncertain result; verify the account before retrying.

The local preview demonstrates the form and confirmation using sample users only.
Password fields are disabled there, and its separate process cannot access real
accounts. No migration is needed for this feature.

- Use a dedicated workforce identity provider, with invitation/assignment only.
  Microsoft Entra ID is the recommended starting point. No registration or
  password handling is implemented in the dashboard.
- OpenID Connect authorization code flow with PKCE and HTTPS metadata validation.
  Pin the tenant issuer; do not use a multi-tenant/common authority.
- Explicit immutable account allowlists on both services. The API independently
  validates signed access tokens, issuer, audience, expiry, MFA evidence and the
  delegated `admin.read` scope. Ordinary GolfSG app bearer tokens are rejected.
- Login succeeds only after the API accepts the user's access token. This lets
  the API validate MFA evidence even when ID and access tokens have different claims.
- Server-side session tickets retain OAuth tokens in process memory; the browser
  receives only a protected session handle in a Secure, HttpOnly, host-only cookie.
  Sessions expire after 15 minutes without sliding renewal. No refresh tokens
  are requested. Restarting the dashboard signs all users out.
- Allowlist removals are checked on each request. Identity-provider revocation
  may take until token/session expiry; this is not continuous access evaluation.
- Logout is POST-only and validates an antiforgery token. Pages are not cached,
  cannot be framed, and use a restrictive script-free content security policy.
- Status reads are logged by immutable account ID. No passwords or tokens are logged.

Enabling `AdminAuth:Enabled` on the API also disables the legacy password-only
admin login and its authorization policy, including already issued legacy admin
tokens. The old `/admin` page is not the new dashboard and must not be used as a
fallback. User-management writes are unavailable through the new read-only scope.

## Configure identity and MFA

Provision the identity provider before enabling either service. Require explicit
user assignment, disallow self-service registration, and enforce phishing-resistant
MFA through the provider's policy. Use separate staging and production registrations.

For Entra ID, create a single-tenant confidential web application for the dashboard
and a separate API registration exposing the delegated scope `admin.read`. Configure
v2 access tokens for the API. Grant that scope only to the dashboard application
and the assigned admin users; do not grant application/client-credentials access.
Use `oid` as the immutable subject claim because Entra `sub` is pairwise per app.

Configure a Conditional Access authentication context (for example `c1`) with an
enabled policy requiring phishing-resistant MFA. Set `MfaClaim=acrs` and
`MfaValue=c1`; the dashboard requests that context in the access token and the API
requires it. Creating a context without attaching an enforced MFA policy is not
sufficient. Confirm tenant licensing and policy availability before deployment.
For another provider, configure its documented MFA claim and value; never substitute
an email, display name or client-supplied claim for MFA evidence.

The identity provider must issue the configured claims in its signed tokens.
Missing MFA evidence denies access; there is no permissive fallback. Verify actual
issued tokens in an isolated test without copying them into logs or repository files.

Set these configuration keys privately on **both** services:

| Key | Value |
| --- | --- |
| `AdminAuth__Enabled` | `true` after all settings and policies are ready |
| `AdminAuth__Authority` | Tenant-specific HTTPS OIDC authority |
| `AdminAuth__Issuer` | Exact issuer expected in signed tokens |
| `AdminAuth__SubjectClaim` | `oid` for Entra, otherwise the provider's stable account claim |
| `AdminAuth__Subjects__0` | An explicitly assigned administrator's immutable ID |
| `AdminAuth__MfaClaim` | `acrs` for the Entra authentication-context setup |
| `AdminAuth__MfaValue` | Enforced authentication context, for example `c1` |

API-only setting: `AdminAuth__Audience`, the API's expected v2 token audience.

Dashboard-only settings:

| Key | Value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` |
| `AllowedHosts` | Exact dashboard hostname; wildcards are rejected |
| `AdminAuth__ClientId` | Dashboard confidential application ID |
| `AdminAuth__ClientSecret` | Secret supplied through the host secret store |
| `AdminAuth__ApiScope` | Full delegated scope URI, e.g. `api://<api-app-id>/admin.read` |
| `AdminApi__BaseUrl` | Staging API HTTPS origin, with no path or credentials |
| `AdminAuth__DataProtectionKeysPath` | Optional private key directory; on Linux protect its storage and access |

Register only the exact HTTPS callback `https://<admin-host>/signin-oidc` on the
identity provider. Do not allow arbitrary redirect URLs. Signing out clears the
dashboard session, not the user's entire identity-provider session.

## Run and deploy

To view the interface before configuring Entra, run the separate loopback-only
preview host. It renders the shared dashboard component with labelled sample data;
it has no API client, database access, or authentication bypass in the real app.

```powershell
dotnet run --project tools/GolfSG.Admin.Preview/GolfSG.Admin.Preview.csproj -c Release
```

Open `http://localhost:7247` and use Users, Courses and Games in the sidebar to
explore the searchable sample records. Keep this developer tool local; it is not a deployment
target. The secure dashboard remains disabled until its identity settings are ready.

For local development, configure secrets for `src/GolfSG.Admin/GolfSG.Admin.csproj`
using its separate User Secrets ID, `GolfSG.Admin.Staging`. Use a trusted HTTPS
development certificate and register the exact local HTTPS callback.

```powershell
dotnet run --project src/GolfSG.Admin/GolfSG.Admin.csproj --urls https://localhost:7246
```

Apply the explicit database setup command with the configured restricted runtime
role before deploying API 0.2.0-alpha. Migration 005 is additive and the API's
existing app-facing routes retain their authentication scheme.

For a container, build from repository root using
`src/GolfSG.Admin/Dockerfile`. Deploy as a separate staging service with one instance.
The in-memory ticket store intentionally does not support multiple instances;
replace it with a protected shared ticket store before scaling. Put this service
behind a private network or identity-aware access gateway, and restrict ingress.
No database credentials belong on the dashboard service.

TLS termination requires ASP.NET Core to see the trusted original HTTPS scheme.
Configure forwarded headers with explicitly trusted proxy addresses/networks;
do not clear the trusted proxy restrictions. The app does not trust arbitrary
forwarding headers. Add known proxy IPs through `ReverseProxy__KnownProxies__0`
(and subsequent indices); the application accepts the forwarded scheme only from
trusted proxies. Test HTTPS redirects and callback URLs through the actual
gateway before making the service accessible.

## Acceptance before enabling staging access

Run API and admin tests, then verify the actual identity-provider round trip:
assigned admin + required MFA succeeds; unassigned user, wrong tenant, missing
authentication context, expired token, wrong audience and ordinary app tokens fail.
Confirm non-admins cannot obtain status data, legacy admin writes are denied,
logout rejects missing antiforgery tokens, sessions expire, and no token appears
in page HTML or browser storage. Check narrow-screen layout and keyboard navigation.
Identity-provider policy enforcement and gateway configuration require deployment
verification; automated local tests do not certify those external controls.

References: [Microsoft Blazor OIDC guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/blazor-web-app-with-oidc?view=aspnetcore-10.0),
[Entra authentication context](https://learn.microsoft.com/en-us/entra/identity-platform/developer-guide-conditional-access-authentication-context),
[Entra ID token claims](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference).
