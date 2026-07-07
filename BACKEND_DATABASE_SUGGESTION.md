# Backend database suggestion

## Recommended MVP setup

Use an ASP.NET Core API with SQLite as the first backend database.

```text
GolfSG MAUI app
  -> ASP.NET Core API
     -> SQLite database file on the server
```

This keeps the app simple while still giving you a real backend boundary. The MAUI app should not know whether the API stores data in SQLite, PostgreSQL, or another database later.

## Why SQLite is a good starting point

SQLite is a good MVP choice when the app has a small number of users, modest write traffic, and simple storage needs. It is free, easy to deploy, and does not require running a separate database server.

For GolfSG, SQLite can work well because each user mostly creates and reads their own golf rounds. That is a simple data pattern and does not require a large database setup at the start.

## Important limitation

SQLite stores data in a file. That file must live on persistent server storage.

Do not host the API somewhere where the filesystem is temporary or reset on deploy/restart unless you also configure persistent storage. If the SQLite file is deleted, the stored rounds are lost.

Good hosting options for SQLite:

- VPS or own server
- Azure App Service with persistent storage configured
- Fly.io with a volume
- Render with persistent disk

Avoid SQLite for this setup on:

- serverless functions without persistent disk
- free hosts where local files are temporary
- multiple API instances writing to the same database file

## Suggested backend design

Create a new project:

```text
src/GolfSG.Api
```

The API should expose endpoints like:

```text
GET    /api/rounds
GET    /api/rounds/{id}
POST   /api/rounds
PUT    /api/rounds/{id}
DELETE /api/rounds/{id}
```

Each request should be tied to an authenticated user, so users only see and modify their own rounds.

## Suggested database table

Start by storing the existing `Round` object as JSON. This matches the current local file storage and keeps the first backend implementation small.

```sql
create table rounds (
  id text primary key,
  user_id text not null,
  date text not null,
  data text not null,
  updated_at_utc text not null
);

create index ix_rounds_user_id_date
on rounds (user_id, date);
```

`data` should contain the serialized `Round` object.

Later, if advanced statistics or reporting become important, the data can be normalized into separate tables for rounds, holes, and shots.

## App integration

Keep using the existing repository boundary:

```text
IRoundRepository
  -> FileRoundRepository
  -> ApiRoundRepository
```

The new `ApiRoundRepository` should call the ASP.NET Core API with `HttpClient`. The existing viewmodels can continue depending on `IRoundRepository`.

This keeps the UI and scoring logic independent from the storage implementation.

## Authentication

The API needs a way to identify the user. Options:

- ASP.NET Core Identity
- Firebase Auth
- Supabase Auth
- Auth0
- Microsoft account login

For a simple MVP, ASP.NET Core Identity with JWT tokens is a reasonable .NET-native option. If you want less auth code to maintain, Firebase Auth or Supabase Auth can also work well.

## When to move away from SQLite

Move to PostgreSQL, for example Supabase or Neon, if:

- many users write data at the same time
- the API needs to run as multiple instances
- you want managed backups and database hosting
- reporting and analytics become more important
- you want fewer risks around local database files

The preferred long-term architecture would be:

```text
GolfSG MAUI app
  -> ASP.NET Core API
     -> PostgreSQL
```

## Practical recommendation

Start with:

```text
MAUI app -> ASP.NET Core API -> SQLite
```

Design the API storage behind an internal interface:

```text
IRoundStore
  -> SqliteRoundStore
  -> PostgresRoundStore later
```

That gives a cheap and simple MVP now, while keeping the path open for PostgreSQL later without rewriting the MAUI app.
