# Super-admin account access

The app uses its deployment's trusted endpoint in Debug and Release. Old saved endpoint preferences are ignored. Users cannot change connections. Production uses the Render API; an owner can build a separate staging app using `GolfSGEnvironment=Staging` and `GolfSGStagingApiAddress`. This is deployment configuration rather than an account permission.

## Provision an administrator

Register an ordinary account first and obtain its immutable ID from `/api/auth/me`. Set `Auth__SuperAdminAccountIds__0` on the API host to that ID, then restart the service. Additional administrators use indices 1, 2, and so on. No administrators are configured by default. Registration, usernames and client-supplied role claims cannot grant this permission. Keep this server configuration restricted to the deployment owner.

## Administration screen and API

Open `/admin` on the API host and sign in with a provisioned account. The screen lists users and provides enable, disable and revoke-session controls with confirmation. Its token is kept only in page memory and is cleared on sign-out or reload. All data and actions are protected by server authorization.

Authenticate with `/api/auth/admin-login` and send the returned bearer access token. This dedicated login requires at least 12 characters with uppercase, lowercase, a digit and a symbol in every build. Ordinary app tokens cannot access administration, even for a provisioned account. Admin refresh drops administration permission; sign in again to regain it. The following routes require a provisioned super-admin account:

| Request | Effect |
| --- | --- |
| `GET /api/admin/users?offset=0&limit=50` | Paginated account IDs, usernames and lockout status; limit is at most 100. |
| `POST /api/admin/users/{id}/disable` | Block login, refresh and existing authenticated sessions. Configured administrators cannot be disabled. |
| `POST /api/admin/users/{id}/enable` | Clear lockout and failed-login counters; require a fresh login. |
| `POST /api/admin/users/{id}/revoke-sessions` | Invalidate existing access and refresh tokens; the user can log in again. |

Actions are logged with actor and target IDs. Each authenticated request checks the current security stamp and lockout status in the database. Disabled accounts keep their rounds and game records. Password recovery is not included. Remove an administrator's ID from server configuration to revoke administration rights.
