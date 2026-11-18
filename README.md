# Takwene Track Management – API

A .NET 10 API for a music distributor's catalogue: **artists**, **tracks** (with ISRC, genre, release
date and a draft → submitted → distributed workflow) and their **deliveries to streaming platforms**
(Spotify, Apple Music, YouTube Music). Anyone can read; one admin account makes changes. Platform
answers (live or rejected) are simulated by the admin, because no real platform is connected.

- Web app: [takwene-track-frontend](https://github.com/yussifmohd/takwene-track-frontend)
- How the code works, feature by feature: [docs/walkthrough.md](docs/walkthrough.md)
- Design decisions and AI usage: [DECISIONS.md](DECISIONS.md) (detailed log: [docs/ai-log.md](docs/ai-log.md))
- Security review of both repos (findings, fixes, evidence): [docs/security-review.md](docs/security-review.md)
- Architecture in plain language, glossary and likely questions: [docs/study-guide.md](docs/study-guide.md)

## Tech stack

.NET 10 Minimal APIs · Clean Architecture (Domain / Application / Infrastructure / Api) ·
MediatR 12.5.0 CQRS with a command-only Unit of Work · Ardalis Result / Specification ·
FluentValidation · EF Core 10 + PostgreSQL 17 (snake_case) · ASP.NET Core Identity + JWT (HS256) ·
ProblemDetails for every error · built-in OpenAPI + Scalar (Development only) · Serilog ·
xUnit v3 + NSubstitute + Shouldly, Testcontainers for PostgreSQL tests.

## Quick start

### With Docker (recommended)

Prerequisite: Docker Desktop.

```bash
cp .env.example .env        # then set POSTGRES_PASSWORD, JWT__SIGNINGKEY (≥ 32 chars) and ADMIN__PASSWORD
docker compose up --build
```

| URL | What |
|---|---|
| http://localhost:8080/health | infrastructure health (Docker / load balancer) |
| http://localhost:8080/api/health | health for the web app (same JSON) |
| http://localhost:8080/scalar | API reference (Development only) |

- Migrations run on startup (compose sets `Database__MigrateOnStartup=true`). If a migration fails,
  the API exits with a non-zero code instead of running on a broken schema.
- Postgres is published on `127.0.0.1:${POSTGRES_HOST_PORT:-5432}` only. If 5432 is taken by a local
  Postgres, set `POSTGRES_HOST_PORT=5433` in `.env`.
- The compose file creates the Docker network `trackmgmt-net`; start the web app's stack after this
  one and it joins the same network.
- On the first start against an empty catalogue the API adds **sample data** (see [Sample data](#sample-data)).
- **Database created before platform deliveries existed?** Such a database can hold tracks marked
  distributed without any delivery, which the API no longer allows. No data migration repairs them:
  reset the local database with `docker compose down -v` and start again.

### Without Docker

Prerequisites: .NET SDK 10.0.403+ (see `global.json`) and a PostgreSQL 17 database created as UTF-8
(`CREATE DATABASE trackmgmt ENCODING 'UTF8' TEMPLATE template0;`).

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=trackmgmt;Username=...;Password=..." --project src/TrackManagement.Api
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 characters>" --project src/TrackManagement.Api
dotnet user-secrets set "Admin:Email" "admin@takwene.local" --project src/TrackManagement.Api
dotnet user-secrets set "Admin:Password" "<12+ chars, upper, lower, digit, symbol>" --project src/TrackManagement.Api
dotnet run --project src/TrackManagement.Api          # http://localhost:8080, Development (migrates on startup)
```

The API refuses to start when the connection string is missing, the JWT signing key is shorter than
32 characters, the database is not UTF-8, or (with migrate-on-startup on) the admin email/password is
missing or weak.

## Configuration and environment variables

| Key (env var form) | Default | Notes |
|---|---|---|
| `ConnectionStrings__Default` | – | required |
| `Jwt__SigningKey` | – | required, ≥ 32 chars (HS256) |
| `Jwt__Issuer` / `Jwt__Audience` | `trackmgmt-api` / `trackmgmt-web` | |
| `Jwt__ExpirationMinutes` | 60 | 5–1440 |
| `Database__MigrateOnStartup` | `false` (`true` in Development and compose) | applies migrations, seeds the admin, adds the sample catalogue to an empty database |
| `Admin__Email` / `Admin__Password` | – | the one admin account; created on startup if missing, never changed afterwards |
| `Cors__AllowedOrigins__0..n` | none (dev: `localhost:5173`, `localhost:8081`) | exact web app origins (`https://host[:port]`); `*`, paths or other schemes stop startup |
| `ReverseProxy__KnownNetworks__0..n` | none (compose: `172.16.0.0/12`) | CIDR ranges of proxies whose `X-Forwarded-For` is trusted |
| `RateLimiting__Login__PermitLimit` / `__WindowSeconds` | 10 / 60 | sign-in attempts per client IP per window |
| `RateLimiting__Api__PermitLimit` / `__WindowSeconds` | 300 / 60 | all requests per client IP per window (health checks not counted) |
| `RateLimiting__Writes__PermitLimit` / `__WindowSeconds` | 60 / 60 | change requests per signed-in admin per window |
| `Api__MaxRequestBodyBytes` | 32768 | larger request bodies get `413` ProblemDetails |
| `POSTGRES_PASSWORD`, `POSTGRES_HOST_PORT`, `REVERSE_PROXY_KNOWN_NETWORK`, `RATE_LIMIT_*` | – / 5432 / `172.16.0.0/12` / as above | compose only (`.env`, every variable listed in `.env.example`) |

No secrets are committed: `appsettings*.json` contain none and `.env` is gitignored.

**Trusted proxies.** The sign-in rate limit is per client IP. `X-Forwarded-For` is honoured only when
the request comes from a network in `ReverseProxy:KnownNetworks`, and only the last hop is used
(`ForwardLimit = 1`). Locally with `dotnet run` leave it empty; in compose it is Docker's private
range (where the web app's nginx runs); behind a cloud load balancer set its VPC CIDR, e.g.
`ReverseProxy__KnownNetworks__0=10.0.0.0/16`. An invalid CIDR stops startup with a message naming the setting.

## Migrations

Migrations live in `src/TrackManagement.Infrastructure/Persistence/Migrations`. With
`Database__MigrateOnStartup=true` the API applies them on startup; otherwise apply them yourself:

```bash
dotnet tool restore
ConnectionStrings__Default="Host=localhost;..." dotnet ef database update -p src/TrackManagement.Infrastructure -s src/TrackManagement.Api

# add a new one after changing the model
dotnet ef migrations add <Name> -p src/TrackManagement.Infrastructure -s src/TrackManagement.Api -o Persistence/Migrations
```

The three platforms are created by a migration with fixed ids, so they are the same in every environment.

## Getting a JWT

There is no registration: the admin account comes from `Admin__Email` / `Admin__Password`.

```bash
curl -s -X POST localhost:8080/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@takwene.local","password":"<ADMIN__PASSWORD from .env>"}'
# → { "accessToken": "...", "tokenType": "Bearer", "expiresAt": "...", "user": { "email", "roles", "expiresAt" } }

curl -s localhost:8080/api/auth/me -H "Authorization: Bearer <accessToken>"
```

In Scalar (`/scalar`, Development) call **Login**, then paste the token into the Bearer auth field.

- HS256, 60 minutes, no refresh token (sign in again). Issuer, audience, lifetime (30 s clock skew)
  and signature are checked; any other algorithm, including `none`, is rejected.
- Every failure is the same `401` "Invalid email or password." (unknown email, wrong password,
  locked account). Unknown emails still run a password hash, so response times are close (measured
  medians 64 ms vs 70 ms, see the security review).
- 5 wrong passwords lock the account for 15 minutes; 10 attempts per minute per client IP, then
  `429` with `Retry-After`.
- To change the admin password: the seeder never modifies an existing account, so delete the user
  row (or the database volume in development) and restart with the new value.

## Tests

```bash
dotnet test --solution TrackManagement.slnx                                               # everything (needs Docker)
dotnet test --solution TrackManagement.slnx -- --filter-not-trait "Category=Integration"  # without Docker
dotnet test --solution TrackManagement.slnx -- --filter-trait "Category=Integration"      # only the PostgreSQL tests
```

- Tests run on Microsoft.Testing.Platform (configured in `global.json`).
- Unit and API-level tests host the app in memory with EF InMemory, so they need no database.
- Tests marked `[Trait("Category", "Integration")]` start PostgreSQL 17 (same image as compose) with
  Testcontainers: Unicode round-trips, unique indexes and check constraints, SQL ordering, row-version
  conflicts between two admins, racing deliveries, and the UTF-8 startup check.
- **API contract**: `specs/<feature>/contracts/openapi.json` is what the web app generates its types
  from; a test fails when the served document drifts. Update it with
  `./scripts/export-contract.ps1 -Feature <spec folder>`.
- **CI**: `.github/workflows/backend-tests.yml` runs on every pull request to `main` (locked restore,
  Release build, all tests including PostgreSQL, TRX and coverage artifacts).
- **Branch protection**: not enabled on GitHub for this repository. Every change went through a pull
  request with a green CI run and was merged by the developer (merge commits, no force-push). For a
  team, protect `main`: require a pull request, require the `backend-tests` check, block force-pushes
  and deletions.

## API overview

Reads are public; every write needs the **Admin** role (`401` without a token, `403` without the
role). Successful responses are plain JSON; every error is ProblemDetails, with `errors.<field>`
for validation.

| Method | Route | Who | What |
|---|---|---|---|
| `GET` | `/health`, `/api/health` | anyone | health incl. a database check |
| `POST` | `/api/auth/login` | anyone | `{ email, password }` → access token |
| `GET` | `/api/auth/me` | signed in | the current user |
| `GET` | `/api/artists?page=&pageSize=&search=` | anyone | paged, by name; `search` = name contains (ignoring case); email only for admins |
| `GET` | `/api/artists/{id}` | anyone | one artist; email only for admins |
| `POST` | `/api/artists` | Admin | `{ name, countryCode, email }` → `201` |
| `GET` | `/api/dsps` | anyone | the platforms (id, name) |
| `GET` | `/api/tracks?artistId=&genre=&status=&page=&pageSize=` | anyone | paged, newest release first; filters combined with AND |
| `GET` | `/api/tracks/{id}` | anyone | track + `allowedNextStatuses`, `canDistribute`, `distributions` |
| `POST` | `/api/tracks` | Admin | `{ title, artistId, isrc, genre, releaseDate }` → `201`, starts as `draft` |
| `PATCH` | `/api/tracks/{id}/status` | Admin | `{ status }`: submit or withdraw |
| `POST` | `/api/tracks/{id}/distribute` | Admin | `{ dspIds: [...] }`: send to platforms |
| `PATCH` | `/api/tracks/{id}/distributions/{dspId}` | Admin | `{ status: "live" \| "rejected" }`: record a platform's answer |

Paging: `page` from 1, `pageSize` 1–50 (default 20). Request bodies are capped at 32 KB (`413`).

### Rules

**Artists.** Name 1–200 characters in any script; country an ISO 3166-1 alpha-2 code (`eg` → `EG`);
email required, unique ignoring case. A duplicate is `409` with `errors.email`, also when two requests race.

**Tracks.** ISRC per ISO 3901 (`CC-XXX-YY-NNNNN`), accepted with all hyphens or none in any case,
stored as 12 characters, unique (`409` `errors.isrc`), returned hyphenated. Genre one of Arabic Pop,
Mahraganat, Khaleeji, Hip-Hop, Electronic, Rap, R&B, Rock. Release date `YYYY-MM-DD`, 1900–2100.
Text is stored as entered (Arabic, emoji); lengths count characters.

**Track status** (`PATCH /status`, enforced by the `Track` entity):

| From \ To | draft | submitted | distributed |
|---|---|---|---|
| draft | 409 (same) | ✅ submit | 409 (submit first) |
| submitted | ✅ withdraw | 409 (same) | **409** – use Distribute |
| distributed | 409 (final) | 409 (final) | 409 (same) |

**`PATCH` to `distributed` is refused with `409`**: "A track becomes distributed when it is sent to at
least one platform. Use Distribute to send it." A distributed track must always say *where* it was
delivered, so the only way to reach that status is `POST /distribute`, which records at least one
delivery in the same change. `allowedNextStatuses` therefore never offers `distributed`.

**Distribute** (`POST /distribute`): the track must be submitted or distributed (draft → `409`). Send
one or more existing platform ids, at most 20 different ones per request; repeats count once; an
empty list or an unknown id is `400` on `errors.dspIds`. Per platform:

- not sent yet → a **pending** delivery with the time it was sent;
- **pending** or **live** → left unchanged (no duplicates);
- **rejected** → sent again: pending, new sent time, answer cleared.

A submitted track becomes distributed. The answer is always `200` with the updated track, also when
nothing changed.

**Platform answers** (`PATCH /distributions/{dspId}`): only a pending delivery can be answered, with
`live` or `rejected` (anything else is `400`); the answer time is recorded. Refusals are `409`:
"The delivery is already live.", "A live delivery is final.", "The delivery is already rejected.",
"A rejected delivery must be sent again with Distribute before the platform can answer." A platform
the track was never sent to is `404`. Answers never change the track's status.

**Two admins at once.** Every change to a track or its deliveries updates the track's PostgreSQL row
version (`xmin`): the second of two simultaneous changes gets `409` "This record was changed by someone
else. Reload it and try again." Two first deliveries to the same platform collide on the primary key
(`409` on `errors.dspIds`).

```bash
T="<accessToken>"
curl -s -X POST localhost:8080/api/tracks -H "Authorization: Bearer $T" -H "Content-Type: application/json" \
  -d '{"title":"تملي معاك","artistId":"<artist id>","isrc":"EG-A1B-26-00001","genre":"Arabic Pop","releaseDate":"2026-12-01"}'
curl -s -X PATCH localhost:8080/api/tracks/<id>/status -H "Authorization: Bearer $T" -H "Content-Type: application/json" -d '{"status":"submitted"}'
curl -s localhost:8080/api/dsps
curl -s -X POST localhost:8080/api/tracks/<id>/distribute -H "Authorization: Bearer $T" -H "Content-Type: application/json" \
  -d '{"dspIds":["<spotify id>","<apple music id>"]}'
curl -s -X PATCH localhost:8080/api/tracks/<id>/distributions/<spotify id> -H "Authorization: Bearer $T" -H "Content-Type: application/json" -d '{"status":"live"}'
curl -s localhost:8080/api/tracks/<id>
```

## Sample data

On the first start with `Database__MigrateOnStartup=true` and **no artists and no tracks**, the API adds
a sample catalogue: 17 artists (9 from Egypt, Lebanon, the UAE, Saudi Arabia and Palestine, some with
Arabic names, and 8 from the UK and the US) and 32 tracks covering every genre and status, with
deliveries to all three platforms in every state (pending, live, rejected, one sent again).

**Everything except the names is sample data.** Artist names, countries, song titles, which artist
sang which song, and genres are real (checked against public sources, listed in
`specs/006-seed-data-security-hardening-and-docs/data-model.md`). Every ISRC (`XX-SMP-26-9NNNN`; `XX` is
not a country), email (`@example.com`), release date, status, delivery and timestamp is invented and
does not describe a real release.

- It never touches existing data: if any artist or track exists, it is skipped (logged). Two API
  instances starting together add it once (a database lock).
- It never creates or changes accounts; the admin comes only from `Admin__*`.
- To start without it: set `Database__MigrateOnStartup=false` and apply migrations yourself, or create
  your own data first. To get it back locally: `docker compose down -v`, then `up`.

## Security

Summary; the full findings table with evidence is [docs/security-review.md](docs/security-review.md).

- Writes need the Admin role on every change route (a test reads the app's route table and fails if one
  is missing); request bodies bind to explicit records, so ids, statuses and deliveries cannot be posted.
- Rate limits: sign-in 10/min per IP, all requests 300/min per IP, changes 60/min per admin (`429` +
  `Retry-After`).
- Every response carries `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: no-referrer` and `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`
  (the Development-only API reference is left without the content policy so it can load); `/api`
  responses are `Cache-Control: no-store`. No HSTS: there is no HTTPS endpoint without a domain.
- CORS: exact origins from configuration, methods `GET, POST, PATCH`, headers `Authorization, Content-Type`.
- Errors are ProblemDetails without stack traces, type names or SQL; logs never contain passwords,
  tokens, the signing key or the connection string (tested).
- The container runs as a non-root user (uid 1654) on the `aspnet:10.0.15-alpine3.24` runtime image.

## Project structure

```text
src/
  TrackManagement.Domain/          entities and rules: Artist, Dsp, Track (+ TrackDistribution), status rules, ISRC, countries
  TrackManagement.Application/     CQRS commands/queries per feature, validators, specifications, behaviors, DTOs
  TrackManagement.Infrastructure/  EF Core (configurations, migrations), repositories, Unit of Work, Identity, JWT
  TrackManagement.Api/             Minimal API endpoint groups, ProblemDetails/exception handlers, OpenAPI, health
tests/TrackManagement.UnitTests/   unit + in-memory API tests (folders mirror src); Integration/ = PostgreSQL via Testcontainers
specs/                             spec, plan, tasks and API contract per feature
scripts/                           contract export
docs/                              walkthrough, AI log, security review, study guide
DECISIONS.md                       decisions and AI usage for both repos
```
