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
cp .env.example .env        # then set POSTGRES_PASSWORD and JWT__SIGNINGKEY (≥ 32 chars)
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
dotnet run --project src/TrackManagement.Api          # http://localhost:8080, Development
```

The API refuses to start when the connection string is missing or the JWT signing key is shorter
than 32 characters.

## Configuration

| Key (env var form) | Default | Notes |
|---|---|---|
| `ConnectionStrings__Default` | – | required |
| `Jwt__SigningKey` | – | required, ≥ 32 chars (HS256) |
| `Jwt__Issuer` / `Jwt__Audience` | `trackmgmt-api` / `trackmgmt-web` | |
| `Jwt__ExpirationMinutes` | 60 | 5–1440 |
| `Database__MigrateOnStartup` | `false` (`true` in Development and compose) | |
| `Cors__AllowedOrigins__0..n` | none (dev: `localhost:5173`, `localhost:8081`) | explicit allow-list |

No secrets are committed: `appsettings*.json` contain none and `.env` is gitignored.

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
