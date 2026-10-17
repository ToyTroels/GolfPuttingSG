# Local PostgreSQL setup and updates

The API uses PostgreSQL through `GolfSG.Server.Infrastructure`. PostgreSQL runs
separately; the app continues saving locally and talks only to the API.
Migration `003_course_library_games.sql` adds shared course revisions, durable picture storage, games and participant submissions. Apply it before using the new library/game screens; the setup tool grants the configured application role access to these tables.
`GolfSG.Database.Setup` applies database updates explicitly. Starting the API does
not create or modify tables.
Migration 004 records the initial schema release; migration 005 advances it to
`0.2.0-alpha` for the read-only admin status feature. Configure the application
role so setup also grants SELECT on the database release metadata table.
Use the same process for a fresh local database and the existing `MauiAppDB`.

## 1. Prepare a fresh database (skip for an existing installation)

Install/start PostgreSQL and connect with pgAdmin as `postgres`.
Create a login role named `mauiapp_user` with a local password. Leave superuser,
database creation and role creation permissions disabled.
Create a database named `MauiAppDB`, owned by `postgres`.
Do not recreate an existing database or role. No seed products or rounds are required.

## 2. Configure private connections

In Visual Studio, right-click `GolfSG.Api` and select **Manage User Secrets**.
Keep its existing `ConnectionStrings:Default`, or add the restricted runtime
connection for a fresh installation:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=MauiAppDB;Username=mauiapp_user;Password=YOUR_LOCAL_APP_PASSWORD"
  }
}
```

Then right-click **GolfSG.Database.Setup → Manage User Secrets** and merge these
settings into the existing secrets, replacing the owner password locally:

```json
{
  "ConnectionStrings": {
    "DatabaseSetup": "Host=localhost;Port=5432;Database=MauiAppDB;Username=postgres;Password=YOUR_LOCAL_OWNER_PASSWORD"
  },
  "DatabaseSetup": {
    "ApplicationRole": "mauiapp_user"
  }
}
```

The two projects explicitly share the existing User Secrets ID. Both menus open
the same development secrets store, so preserve existing values while merging
these settings. An existing owner connection does not need to be moved. The API
continues consuming `Default`; only the setup executable consumes the owner
connection. Setup loads User Secrets without requiring a Development environment
or launch profile.

Keep these credentials out of the repository and the MAUI app.
`Default` is the API's restricted runtime connection. `DatabaseSetup` is used only
by the explicit update command and must be the database owner or an administrator
able to create tables and grant access to existing tables. If omitted, setup uses
`Default`; this only works when that connection has the required permissions.
The application role must already exist. Its grants are reapplied on every setup run.

For an existing installation, preserve the working `Default` connection and add
only the owner connection and application-role setting.

## 3. Apply database updates

From the repository root, run:

```powershell
dotnet run --project src/GolfSG.Database.Setup/GolfSG.Database.Setup.csproj -c Release
```

The setup executable loads User Secrets, updates the database, and exits. The
Release command also avoids a Windows application-control rejection observed for
the locally built Debug setup executable; it does not alter Windows policy. The
previous API `--database-update` option has been removed. Success ends with
`Database setup committed successfully.` A failure exits unsuccessfully and rolls
back the transaction; fix the reported issue and rerun the command.

The initial update creates `products`, `saved_rounds`, and a migration history table
named `golfsg_schema_versions`. Existing products and rounds are preserved, including
tables created using the older `001_saved_rounds.sql` script, now retained at
`src/GolfSG.Database.Setup/Sql/001_saved_rounds.sql`. Existing tables must
have the schema expected by `AppDbContext` in Server.Infrastructure and that script; `CREATE TABLE IF NOT EXISTS`
does not repair an incompatible manually created table.

The original `001_initial.sql` and its checksum stay unchanged. The second migration,
`002_identity_round_ownership.sql`, adds ASP.NET Core Identity account tables and
changes the round key to `(user_id, id)`. Previously unowned rounds and deletion
records are preserved under `user_id = '__legacy__'`. They cannot be read by new
accounts and are not automatically assigned to the first user. The same locally
saved round can upload into a chosen account after sign-in without altering the
preserved legacy row. Back up valuable server history before applying the update.

Run the command again after pulling changes that add migrations. Already-applied
updates are skipped. Concurrent setup runs are serialized, and migrations plus grants
are committed together. The runtime role receives product CRUD access, sequence access,
and round read/write access, plus runtime access to the Identity tables; it does not
need migration-history access.

In pgAdmin, connected to `MauiAppDB` as its owner, verify:

```sql
SELECT name, applied_at FROM public.golfsg_schema_versions ORDER BY name;
SELECT version, applied_at FROM public.golfsg_database_release;
SELECT COUNT(*) FROM public.products;
SELECT COUNT(*) FROM public.saved_rounds;
SELECT user_id, COUNT(*) FROM public.saved_rounds GROUP BY user_id;
```

Then start the API normally. Anonymous calls to `/api/products` and `/api/rounds`
now return **401**. Register and sign in before checking those routes; authenticated
round requests return only that account's data. Follow
[Products API setup](PRODUCTS_API_SETUP.md) and [Round sync setup](ROUND_SYNC_SETUP.md)
for connecting the app.

## Adding a database change

Add the next numbered SQL file under `src/GolfSG.Database.Setup/Sql/Migrations`, for example
`006_add_round_index.sql`. Files are embedded in the setup executable and applied in filename order;
use zero-padded numbers. Do not edit or rename an applied migration: its checksum is
verified, and setup refuses to continue if it changes. Line-ending differences between
Windows and Unix are ignored.

Write PostgreSQL SQL without `BEGIN`/`COMMIT`: the runner owns the transaction.
Use statements that support transactions. Preserve existing data; document any data
conversion and take a database backup before applying changes to valuable history.
If a new table needs runtime access, add its explicit grant to
`src/GolfSG.Database.Setup/DatabaseSetup.cs`.
There is no automatic downgrade or reset command.

## IIS

IIS does not load User Secrets. Apply updates from the development command above
against the same database, then publish the API normally. The IIS private runtime
configuration only needs the restricted `Default` connection; keep the owner
connection in the local development secrets. After applying migration 002, republish
the API and rebuild the phone app together so the client supports sign-in. Configure
stable Data Protection keys for IIS as described in [Products API setup](PRODUCTS_API_SETUP.md).
These instructions do not publish or modify the running IIS deployment automatically.
