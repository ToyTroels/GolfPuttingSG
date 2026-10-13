# Separate staging from production

The staging app is a Release build named **GolfSG Staging**, with a separate
application identity and an explicit staging API address. It can coexist with
the production installation and has its own local history, preferences and
secure session storage. Normal Release builds retain their production identity
and `https://golfputtingsg.onrender.com/` endpoint. Normal Debug builds retain
their existing configurable development endpoint.

## Environment layout

| Setting | Production | Staging |
| --- | --- | --- |
| Git branch | `master` | `dev` |
| API service | Existing production service | Separate `golfputtingsg-staging` service |
| Database | Existing production database | Fresh database with staging-only accounts/data |
| Android app ID | `com.GolfSG.myapp` | `com.GolfSG.myapp.staging` |
| Apple/Windows app ID | `com.troel.golfsg` | `com.troel.golfsg.staging` |
| App title | GolfSG | GolfSG Staging |
| API endpoint | Fixed production address | Fixed address supplied when building |
| ASP.NET environment | Production | Staging |

The staging resources have been provisioned. Their public identifiers are recorded
in [staging metadata](../deploy/staging.json), with no passwords or tokens:

- API: `https://golfputtingsg-staging.onrender.com/`.
- Render service: `golfputtingsg-staging` in **My Workspace**, Frankfurt, free plan.
- Neon project: **GolfSG Staging**, with a fresh `golfsg_staging` database and
  restricted `golfsg_staging_app` runtime role. Migrations 001, 002 and 003 are applied.
- GitHub `dev` starts at `5d8364de27653111fbb02266eb0785f76249abc7`, the production
  service's running commit when staging was created.

Production currently tracks `codex/maui-products-api`, not `master`. This setup
does not switch its branch. The staging service initially runs the same committed
API baseline; the app build changes and newer feature work remain in the local
working tree until deliberately committed and promoted to `dev`.
Auto-deployment is off, pending the later CI/CD work.
The provisioned service passed live database readiness, account registration,
login, authenticated round reads, and a synthetic round upload/read-back.
The staging database contains only the test accounts/data created for those checks.

## 1. Create the staging database

Use a fresh Neon project/database in the chosen account and region, or a
schema-only branch with a separate staging database and credentials. Avoid a
normal branch of production: it starts with production data and copied roles.
A fresh database lets the versioned setup tool create the complete schema and
its migration history consistently.

Create a restricted login role named `golfsg_staging_app` for the API. Keep the
database owner credentials for setup only. Use staging-only passwords and test
accounts; do not copy production connections, users, pictures or round history.

Before running setup, set `ConnectionStrings__DatabaseSetup` in the local
process to the **staging database's direct owner connection** in Npgsql format.
Set `DatabaseSetup__ApplicationRole` to `golfsg_staging_app`. These environment
variables override the existing development User Secrets. Confirm the staging
hostname and database first; never rely on setup's default-connection fallback.

```powershell
$env:DatabaseSetup__ApplicationRole = 'golfsg_staging_app'
dotnet run --project src/GolfSG.Database.Setup/GolfSG.Database.Setup.csproj -c Release
```

Set the owner connection privately, outside checked-in scripts and this document.
Remove both setup variables from the process afterward. See
[database setup](LOCAL_DATABASE_SETUP.md) for connection format and grants.
Starting the API does not apply migrations, and its container does not contain
the setup tool.

## 2. Create the staging API service

[The staging Blueprint](../deploy/render.staging.yaml) describes the target
configuration and defines only staging. The current service was created through
the Render integration. Its health-check path is configured as `/api/health`
in the dashboard.
For future Blueprint management, use the custom file path
`deploy/render.staging.yaml` and review its matching staging service before syncing.

The initial configuration uses Docker, the `dev` branch, Frankfurt, a free web
service, `ASPNETCORE_ENVIRONMENT=Staging`, and `/api/health` as its health check.
Provide `ConnectionStrings__Default` privately in Render with the restricted
**staging** runtime connection. Never add the owner connection to the API service.
Render prompts for this secret because the Blueprint uses `sync: false`.

Auto-deployment is disabled until CI on `dev` and the migration/deployment sequence
are configured. The first deployment still requires an initialized database.
Record the actual URL returned by Render rather than assuming the service name
will be available as a hostname.

After the repository change is deployed, staging uses a separate Data Protection
application name, `GolfSG.Api.Staging`.
Keep its key storage separate from production. A free Render service has no
persistent disk, so tokens may be invalidated on restart/redeployment; sign in
again during testing. For stable staging tokens, configure independent persistent
key storage when choosing a supported hosting plan. Do not share production keys.

Reference: [Render Blueprint specification](https://render.com/docs/blueprint-spec).

## 3. Build the staging Android app

Use the actual staging URL, with HTTPS and a trailing slash. The address is public
configuration, not a credential. The command below uses the provisioned service:

```powershell
dotnet publish src/GolfSG/GolfSG.csproj `
  -f net10.0-android `
  -c Release `
  -p:GolfSGEnvironment=Staging `
  -p:GolfSGStagingApiAddress=https://golfputtingsg-staging.onrender.com/ `
  -p:AndroidPackageFormats=apk `
  -o .artifacts/staging/android
```

The shared environment targets also apply to Windows, iOS and Mac Catalyst builds.
Apple device distribution requires profiles for the staging bundle ID; Windows
installation requires an appropriate signed package. Follow
[Android signing](ANDROID_RELEASE_SIGNING.md) for distributable packages. Local
development-signed staging APKs are for personal device testing only.

The build rejects a missing staging address, the production API host, addresses
with credentials/paths/queries/fragments, and unknown environment names. Both
Debug and Release staging builds lock the endpoint and hide the endpoint editor.
An ordinary production Release build never consumes the staging address.
Staging uses separate intermediate and build-output folders so native package
manifests cannot be reused accidentally when switching back to production.

## 4. Verify isolation

1. Verify the deployed staging service's branch, environment and database hostname.
2. Check `/api/health` on its actual URL and confirm database readiness.
3. Install GolfSG Staging alongside GolfSG and confirm both app names are present.
4. Register a staging-only account, save a test round and manually synchronize it.
5. Confirm it appears in staging and does not appear in production.
6. Verify course publication, library download and game submission with test data.
7. Launch the production app and confirm its existing history and account remain intact.

The endpoint behavior can also be checked without installing the app:

```powershell
dotnet test tests/GolfSG.PageTests/GolfSG.PageTests.csproj -c Release
dotnet test tests/GolfSG.PageTests/GolfSG.PageTests.csproj -c Release `
  -p:GolfSGEnvironment=Staging `
  -p:GolfSGStagingApiAddress=https://staging.example.invalid/
```

These tests use the public address as configuration only and do not contact it.
