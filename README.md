# Takwene Track Management – API

.NET 10 API for managing artists, tracks and their distribution to DSPs (Spotify, Apple Music,
YouTube Music). The web app lives in a separate repo:
[takwene-track-frontend](https://github.com/yussifmohd/takwene-track-frontend).

- Design decisions and AI usage: `DECISIONS.md` (Phase 5) and [docs/ai-log.md](docs/ai-log.md)
- Specs per phase: [specs/](specs/)

> Status: Phase 0 (foundation + health check). Business features arrive in later phases.

## Stack

.NET 10 Minimal APIs · Clean Architecture (Domain / Application / Infrastructure / Api) ·
MediatR 12.5.0 CQRS with a command-only Unit of Work · Ardalis Result / Specification ·
FluentValidation · EF Core 10 + PostgreSQL 17 (snake_case) · ProblemDetails for every error ·
built-in OpenAPI + Scalar (Development only) · Serilog · xUnit v3 + NSubstitute + Shouldly.

## Prerequisites

- Docker Desktop (for the one-command run), **or**
- .NET SDK 10.0.403+ (see `global.json`) and a PostgreSQL 17 instance.

## Run with Docker (recommended)

```bash
cp .env.example .env        # then set POSTGRES_PASSWORD, JWT__SIGNINGKEY (≥ 32 chars) and ADMIN__PASSWORD
docker compose up --build
```

| URL | What |
|---|---|
| http://localhost:8080/health | infrastructure health (Docker / load balancer) |
| http://localhost:8080/api/health | health for the web app (same JSON) |
| http://localhost:8080/scalar | API reference (Development only) |

- Postgres is published on `127.0.0.1:${POSTGRES_HOST_PORT:-5432}` only. If 5432 is taken by a
  local Postgres, set `POSTGRES_HOST_PORT=5433` in `.env`.
- The compose file creates the Docker network `trackmgmt-net`; start the frontend stack after this
  one and it joins the same network.
- Migrations run on startup because compose sets `Database__MigrateOnStartup=true`.
  If a migration fails the API exits with a non-zero code instead of running on a broken schema.

## Run without Docker

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=trackmgmt;Username=...;Password=..." --project src/TrackManagement.Api
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 characters>" --project src/TrackManagement.Api
dotnet user-secrets set "Admin:Email" "admin@takwene.local" --project src/TrackManagement.Api
dotnet user-secrets set "Admin:Password" "<12+ chars, upper, lower, digit, symbol>" --project src/TrackManagement.Api
dotnet run --project src/TrackManagement.Api          # http://localhost:8080, Development
```

The API refuses to start when the connection string is missing, the JWT signing key is shorter
than 32 characters, or (with migrate-on-startup on) the admin email/password is missing or weak.

## Configuration

| Key (env var form) | Default | Notes |
|---|---|---|
| `ConnectionStrings__Default` | – | required |
| `Jwt__SigningKey` | – | required, ≥ 32 chars (HS256) |
| `Jwt__Issuer` / `Jwt__Audience` | `trackmgmt-api` / `trackmgmt-web` | |
| `Jwt__ExpirationMinutes` | 60 | 5–1440 |
| `Database__MigrateOnStartup` | `false` (`true` in Development and compose) | |
| `Cors__AllowedOrigins__0..n` | none (dev: `localhost:5173`, `localhost:8081`) | explicit allow-list |
| `Admin__Email` / `Admin__Password` | – | the one admin account; created on startup if missing, never changed afterwards |
| `ReverseProxy__KnownNetworks__0..n` | none (compose: `172.16.0.0/12`) | CIDR ranges of proxies whose `X-Forwarded-For` is trusted |
| `RateLimiting__Login__PermitLimit` / `__WindowSeconds` | 10 / 60 | sign-in attempts per client IP per window |

No secrets are committed: `appsettings*.json` contain none and `.env` is gitignored.

## Authentication

Reads (`GET`) are public. Every endpoint that changes data requires the **Admin** role. There is no
registration: one admin account is provisioned from `Admin__Email` / `Admin__Password` on startup.

```bash
curl -s -X POST localhost:8080/api/auth/login -H "Content-Type: application/json"   -d '{"email":"admin@takwene.local","password":"<ADMIN__PASSWORD from .env>"}'
# → { "accessToken": "...", "tokenType": "Bearer", "expiresAt": "...", "user": { "email", "roles", "expiresAt" } }

curl -s localhost:8080/api/auth/me -H "Authorization: Bearer <accessToken>"
```

In Scalar (`/scalar`, Development) call **Login**, then paste the token into the Bearer auth field.

- **Tokens**: HS256 JWT, 60 minutes, no refresh token (sign in again). The API checks issuer,
  audience, lifetime (30 s clock skew) and signature, and accepts only HS256 (`alg: none` and other
  algorithms are rejected).
- **Failures** all return the same `401` "Invalid email or password." (unknown email, wrong
  password, locked account). Unknown emails still run a password hash check, so response times match.
- **Lockout**: 5 wrong passwords lock the account for 15 minutes.
- **Rate limit**: 10 sign-in attempts per minute per client IP → `429` ProblemDetails with `Retry-After`.
- **Errors**: `401` (missing/invalid/expired token) and `403` (signed in without the Admin role) are
  ProblemDetails.
- **Changing the admin password**: the seeder never modifies an existing account. Delete the user
  row (or the database volume in development) and restart with the new value.

### Trusted proxies (real client IP)

The rate limit is per client IP, so the API must know the real IP behind a proxy, without letting
anyone fake it. `X-Forwarded-For` is honoured **only** when the request comes from a network listed in
`ReverseProxy:KnownNetworks` (CIDR, configuration only, nothing hard-coded), and only the last hop is used
(`ForwardLimit = 1`).

| Environment | Value |
|---|---|
| `dotnet run` | empty (no proxy; loopback only) |
| Docker compose | `REVERSE_PROXY_KNOWN_NETWORK=172.16.0.0/12` (Docker's private range, where the web nginx runs) |
| AWS (Phase 6) | the VPC CIDR of the load balancer, e.g. `ReverseProxy__KnownNetworks__0=10.0.0.0/16` |

An invalid CIDR stops startup with a message naming the setting.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/TrackManagement.Infrastructure -s src/TrackManagement.Api -o Persistence/Migrations
# apply manually (otherwise the API applies them on startup when the flag is on):
ConnectionStrings__Default="Host=localhost;..." dotnet ef database update -p src/TrackManagement.Infrastructure -s src/TrackManagement.Api
```

## Tests

```bash
dotnet test --solution TrackManagement.slnx
```

Tests run on Microsoft.Testing.Platform (configured in `global.json`). API-level tests host the
app in memory with EF InMemory, so no database is needed.

### API contract

`specs/<feature>/contracts/openapi.json` is the contract the web app generates its types from.
A test compares it with the document the API serves and fails on drift. To update it:

```powershell
./scripts/export-contract.ps1 -Feature 001-project-foundation-and-health-check
```

## CI

`.github/workflows/backend-tests.yml` runs on every pull request to `main`: locked restore,
Release build, tests with TRX and Cobertura coverage (uploaded as artifacts).

To make it a merge gate: **Settings → Branches → Add rule** for `main` → *Require status checks to
pass* → select `test` (from `backend-tests`).

## Project structure

```text
src/
  TrackManagement.Domain/          entities, constants (no dependencies)
  TrackManagement.Application/     CQRS messages, behaviors, repository/UoW abstractions
  TrackManagement.Infrastructure/  EF Core, repositories, Unit of Work, options, migrations
  TrackManagement.Api/             Minimal API endpoints, ProblemDetails, OpenAPI, health
tests/TrackManagement.UnitTests/   unit + in-memory API tests (folders mirror src)
specs/                             Spec Kit spec / plan / tasks per phase
```
