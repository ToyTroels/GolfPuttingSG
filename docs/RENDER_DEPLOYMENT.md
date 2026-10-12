# Deploy the API to Render

`GolfSG.Api/GolfSG.Api.csproj` is included in `GolfSG.sln` and tracked in this
repository. Render selects repositories, not individual Visual Studio projects.
The root Dockerfile publishes only the API and its server dependencies; no MAUI
workloads are needed.

## Render service settings

Push the Dockerfile and .dockerignore to the branch Render deploys, then create
a Web Service from this repository (or configure the existing Docker service):

| Setting | Value |
| --- | --- |
| Language/runtime | Docker |
| Root Directory | Leave blank |
| Dockerfile Path | `Dockerfile` |
| Docker Build Context Directory | `.` (repository root) |
| Docker Command | Leave blank; the image starts the API |
| Health Check Path | `/api/health` |

The container listens on Render's `PORT`, with 8080 as the local fallback.
Render terminates public HTTPS and forwards HTTP to the container. Do not set
`ASPNETCORE_HTTPS_PORT` to a container port: this image does not serve HTTPS.
The current HTTPS-redirection middleware may log that no HTTPS port is configured;
Render supplies the public HTTPS endpoint.

Add `ConnectionStrings__Default` as a private environment variable, using Npgsql
format rather than the `postgresql://` URL:

```text
Host=<neon-host>;Port=5432;Database=neondb;Username=<runtime-role>;Password=<password>;SSL Mode=Require;Channel Binding=Require
```

Never put database passwords in the Dockerfile or commit them to the repository.
Use a restricted role for runtime access. Apply the existing database updates
separately with the database owner using Neon's direct connection, following
[database setup](LOCAL_DATABASE_SETUP.md). The runtime image contains only the API;
it does not include the setup tool or run migrations on startup.

For stable login tokens across deployments, mount persistent writable storage and
set `Auth__DataProtectionKeysPath` to a directory on that mount. Ensure the
container's non-root app user can write there. Without persistent keys, users can
need to sign in again after restarts. Multiple API instances need shared keys.

After deployment, check `https://golfputtingsg.onrender.com/api/health` and
verify registration, login, and manual round sync. Release builds always use
`https://golfputtingsg.onrender.com/`; saved endpoint overrides are ignored and
address-editing controls are removed. Debug builds retain configurable endpoints
under **Konto og server**. If the production hostname changes, update
`PreferenceProductsApiSettings.ProductionAddress` and rebuild the app.

Upgrading from a development server clears its login and sync tracking on local
account load, preserving rounds for manual sync after signing in on Render.

If the repository is missing from Render's list, check the connected Git provider's
repository permissions. If the API files are missing, check that Render uses the
branch containing `GolfSG.Api` and that changes have been pushed.

## Local container check

```powershell
docker build -t golfsg-api .
docker run --rm -p 8080:8080 --env-file .env golfsg-api
```

The private `.env` file must supply `ConnectionStrings__Default`; it is excluded
from the Docker build context. `/api/health` requires an initialized reachable
database to report readiness.

See [Render Docker documentation](https://render.com/docs/docker) and
[Render web services](https://render.com/docs/web-services).
