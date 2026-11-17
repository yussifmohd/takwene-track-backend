# Research: Foundation & Architecture (API service)

All versions checked on 2026-11-17 against nuget.org / MCR / Docker Hub. Rule applied: newest
stable release that is **at least two weeks old** (published on or before 2026-11-03), on the
required major line, with a permissive license.

## R1. Toolchain

- **Decision**: .NET SDK `10.0.403` in `global.json` with `rollForward: latestFeature`
  (10.0.404 installed locally will satisfy it). Runtime/ASP.NET 10.0.14. dotnet-ef `10.0.14`
  via `.config/dotnet-tools.json`.
- **Rationale**: 10.0.403 shipped 2026-10-25 with runtime 10.0.14; 10.0.404/10.0.15 shipped
  2026-11-10 (too new under the two-week rule).
- **Alternatives**: 10.0.404 (too new); floating `latestMajor` (forbidden: no floating).

## R2. Package pins (central package management, `Directory.Packages.props`)

| Package | Version | License | Published | Notes |
|---|---|---|---|---|
| MediatR | 12.5.0 | Apache-2.0 | 2025-04-01 | exact pin; 13+ commercial |
| FluentValidation (+ .DependencyInjectionExtensions) | 12.2.0 | Apache-2.0 | 2026-10-10 | |
| Ardalis.Result | 10.1.0 | MIT | 2024-10-28 | |
| Ardalis.Specification (+ .EntityFrameworkCore) | 9.3.1 | MIT | 2025-08-24 | targets net8/net9, EF ≥ 9.0.8 → resolves against EF 10 (R3) |
| Ardalis.GuardClauses | 5.0.0 | MIT | 2024-09-30 | |
| Microsoft.EntityFrameworkCore (+ .Design, .Relational, .InMemory for tests) | 10.0.14 | MIT | 2026-10-25 | |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL | 2026-07-10 | |
| EFCore.NamingConventions | 10.0.1 | Apache-2.0 | 2026-01-22 | snake_case like reference |
| Microsoft.AspNetCore.OpenApi | 10.0.14 | MIT | 2026-10-25 | built-in OpenAPI |
| ~~Microsoft.Extensions.ApiDescription.Server~~ | removed | | | build-time export returned empty paths (see R6) |
| Scalar.AspNetCore | 2.18.7 | MIT | 2026-10-30 | 2.19+/3.0 too new |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.14 | MIT | 2026-10-25 | registered now, used from Phase 1 |
| Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore | 10.0.14 | MIT | 2026-10-25 | `AddDbContextCheck` |
| Serilog.AspNetCore | 10.0.0 | Apache-2.0 | 2025-11-28 | console sink |
| xunit.v3 | 4.0.2 | Apache-2.0 | 2026-10-09 | 4.0.3 too new |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 | 2026-10-09 | |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | 2026-10-13 | |
| NSubstitute | 6.2.0 | BSD-3-Clause | 2026-08-11 | |
| Shouldly | 4.3.0 | BSD-3-Clause | 2025-01-23 | no FluentAssertions |
| ~~coverlet.collector~~ → coverlet.MTP | 10.1.0 | MIT | 2026-09-27 | collector is VSTest-only; MTP extension `--coverlet` |
| Microsoft.Testing.Extensions.TrxReport | 2.5.1 | MIT | 2026-10-07 | `--report-trx`; matches Microsoft.Testing.Platform 2.5.1 pinned by xunit.v3 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.14 | MIT | 2026-10-25 | API-level tests |

Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14, MIT) is added in Phase 1.

## R3. Ardalis.Specification 9.3.1 on EF Core 10

- **Decision**: use 9.3.1 (latest; no 10.x line exists). NuGet selects its net9.0 asset with
  `Microsoft.EntityFrameworkCore >= 9.0.8`, satisfied by 10.0.14.
- **Risk & mitigation**: a binary break would show as `MissingMethodException`. Phase 0 tests
  exercise `RepositoryBase` Add/List/Count against the EF InMemory provider, so a break fails CI
  immediately. Fallback: hand-written `SpecificationEvaluator` usage (still MIT, same package).

## R4. Unit of Work with Ardalis repositories

- **Decision**: `EfRepository<T> : RepositoryBase<T>` overrides `SaveChangesAsync` as a no-op;
  `UnitOfWork` wraps `DbContext.SaveChangesAsync`; `UnitOfWorkBehavior<TRequest,TResponse>`
  constrained to `ICommandMarker` commits once when the handler returns a success status.
- **Rationale**: approved deviation from the reference (which saves per repository call and only
  logs "transactions"). Makes multi-step commands atomic; queries can never write.
- **Alternatives**: explicit `BeginTransaction` per command (heavier, unnecessary for one
  `SaveChanges`); custom repository without Ardalis base (loses spec evaluation for free).

## R5. Health endpoints

- **Decision**: ASP.NET Core health checks with `AddDbContextCheck<AppDbContext>("database")`.
  Two routes share one JSON writer:
  - `GET /health` (infrastructure: Docker healthcheck, ALB) via `MapHealthChecks`.
  - `GET /api/health` (web app, same origin through the `/api` proxy) as a Minimal API endpoint
    in a `Health` group that calls `HealthCheckService.CheckHealthAsync` and returns a typed
    `HealthReportDto`, so it appears in the OpenAPI contract.
  - Body: `{ status, checkedAt, checks: [{ name, status, durationMs }] }`; 200 when Healthy,
    503 otherwise. No exception messages in the body.
- **Rationale**: Q1 clarification; ALB checks `/health`; the SPA only proxies `/api/*`.
- **Alternatives**: `/health` only (web app would need a second proxy rule); Xabaril
  `AspNetCore.HealthChecks.NpgSql` (extra dependency, no benefit over DbContext check).

## R6. OpenAPI contract export

- **Decision (revised during implementation)**: built-in `AddOpenApi("v1")` with a document
  transformer (Bearer scheme, info). The contract is taken from the **running app**:
  `tests/.../Api/OpenApiContractTests.cs` fetches `/openapi/v1.json` from the in-memory test host
  (Development) and fails if it differs from `specs/<feature>/contracts/openapi.json`;
  `scripts/export-contract.ps1 -Feature <dir>` runs that test in update mode to rewrite the file.
  CI therefore catches contract drift.
- **Why revised**: `Microsoft.Extensions.ApiDescription.Server` build-time generation produced an
  empty `paths` object: it stops the app at host build, before Minimal API endpoints are mapped.
  The package was removed.
- JSON uses `JsonNumberHandling.Strict` so numeric fields are `number` (not `number | string`) in the contract.
- **Docs exposure**: `MapOpenApi()` + `MapScalarApiReference()` only when `IsDevelopment()`.

## R7. Error handling

- **Decision**: `AddProblemDetails` (adds `traceId` via `CustomizeProblemDetails`),
  `GlobalExceptionHandler : IExceptionHandler` (500, generic detail, full exception logged),
  `UseStatusCodePages()` so bare 404/405 become ProblemDetails, and
  `ResultExtensions.ToHttpResult()` as the single Result → HTTP map. A DbUpdateException → 409
  handler (as in the reference) is added in Phase 2 when unique indexes appear.

## R8. Configuration & secrets

- **Decision**: options classes with DataAnnotations + `ValidateOnStart()`:
  `ConnectionStrings:Default` (required), `Jwt` (Issuer, Audience, SigningKey ≥ 32 chars,
  ExpirationMinutes 5–1440), `Cors:AllowedOrigins` (array), `Database:MigrateOnStartup` (bool).
  `.env.example` documents them with placeholders; compose reads `.env`. No `appsettings.Development.json`
  secrets are committed (unlike the reference, which gitignores the file).

## R9. Database initialisation

- **Decision**: `DatabaseInitializer` hosted at startup: if `Database:MigrateOnStartup`, run
  `MigrateAsync` with a bounded retry (Npgsql `EnableRetryOnFailure` + compose `depends_on:
  service_healthy`), then call registered `ISeeder`s (none in Phase 0). Exceptions propagate →
  process exits non-zero (spec edge case). Phase 0 ships an empty `InitialCreate` baseline
  migration so the pipeline (tool manifest → migration → startup migrate) is proven.

## R10. Containers

- **Decision**: build `mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24`, runtime
  `mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24`, `USER $APP_UID` (non-root), port 8080,
  `HEALTHCHECK` with busybox `wget -qO- http://localhost:8080/health`. DB `postgres:17.9-alpine`
  (latest 17.x at implementation if newer and ≥ 2 weeks old), port bound to `127.0.0.1:5432`,
  named volume, `pg_isready` healthcheck. Compose defines network `trackmgmt-net` with an explicit
  `name:` so it is created if missing; the frontend compose joins it as `external`.

## R11. CI

- **Decision**: `.github/workflows/backend-tests.yml`, `on: pull_request: branches: [main]`,
  `actions/setup-dotnet` with `global-json-file: global.json`, `dotnet restore` (locked mode),
  `dotnet build -c Release --no-restore`, `dotnet test --solution TrackManagement.slnx --no-build -c Release
  --report-trx --coverlet --coverlet-output-format cobertura` (Microsoft.Testing.Platform options;
  revised during implementation because `--logger trx`/`--collect` are VSTest-only), `actions/upload-artifact` for TRX + coverage. Action
  versions pinned to major tags. README documents enabling it as a required status check.

## R12. Line endings

- **Decision**: add `.gitattributes` (`* text=auto eol=lf`, `*.ps1 eol=crlf`, `*.sh eol=lf`) to
  stop CRLF warnings and keep shell scripts runnable in Linux containers.
