# Implementation Plan: Foundation & Architecture (API service)

**Branch**: `001-project-foundation-and-health-check` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-project-foundation-and-health-check/spec.md`

**Skills applied**: dotnet-clean-architecture, git-workflow, decisions-log

## Summary

Scaffold the .NET 10 Clean Architecture solution with the shared CQRS building blocks (markers,
handlers, Logging/Validation/UnitOfWork behaviors, non-saving Ardalis repository, Unit of Work,
single Result → HTTP map), ProblemDetails error handling, built-in OpenAPI + Scalar (Development
only), validated configuration, a structured database-aware health check on `/health` and
`/api/health`, an empty baseline migration applied on startup behind a flag, Docker (non-root API
+ Postgres) on the `trackmgmt-net` network, a PR test workflow, and the docs skeleton
(README, docs/ai-log.md, Memory.md). No business features.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.403 pinned, runtime 10.0.14)

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR 12.5.0, FluentValidation 12.2.0,
Ardalis.Result 10.1.0, Ardalis.Specification(.EntityFrameworkCore) 9.3.1, Ardalis.GuardClauses 5.0.0,
EF Core 10.0.14 + Npgsql 10.0.3 + EFCore.NamingConventions 10.0.1, Microsoft.AspNetCore.OpenApi 10.0.14,
Scalar.AspNetCore 2.18.7, Serilog.AspNetCore 10.0.0 (full table: research.md R2)

**Storage**: PostgreSQL 17 (snake_case); EF Core migrations in Infrastructure

**Testing**: xUnit v3 4.0.2, NSubstitute 6.2.0, Shouldly 4.3.0, Microsoft.AspNetCore.Mvc.Testing + EF InMemory for API-level tests, coverlet.MTP + TrxReport (MTP extensions)

**Target Platform**: Linux containers (alpine) locally via Docker Compose; AWS EC2 later (Phase 6)

**Project Type**: web-service (API) — frontend lives in a separate repo

**Performance Goals**: health response < 1 s locally; CI < 10 min (SC-005)

**Constraints**: no secrets committed; docs only in Development; non-root container; versions pinned

**Scale/Scope**: take-home scale (tens of records), 4 production projects + 1 test project

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|---|---|---|
| I. Clean Architecture & CQRS | 4 projects, inward deps; markers + handler interfaces; UoW behavior for commands only; repos don't save | ✅ planned (Application/Common/Messaging, Behaviors, Persistence) |
| II. Thin HTTP edge | Minimal API groups; one `ToHttpResult`; ProblemDetails for all errors; `/api/...` routes | ✅ with one justified exception (health, see Complexity Tracking) |
| III. Tests ship with features | Behaviors, Result mapping, repository no-save, health + ProblemDetails API tests | ✅ |
| IV. Security by default | Config-only secrets with ValidateOnStart; CORS allow-list; non-root image; docs Development-only; JWT validation params prepared | ✅ (auth enforcement starts Phase 1) |
| V. Spec-driven delivery | spec/plan/tasks before code; contract exported to `contracts/` | ✅ |
| VI. Simplicity & license hygiene | Only packages in R2, all permissive, exact pins via CPM | ✅ |

Post-design re-check (after data-model/contracts/quickstart): ✅ no new violations.

## Project Structure

### Documentation (this feature)

```text
specs/001-project-foundation-and-health-check/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/openapi.json
├── checklists/requirements.md
└── tasks.md            (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── TrackManagement.slnx
├── global.json  Directory.Build.props  Directory.Packages.props  .editorconfig
├── .config/dotnet-tools.json            # dotnet-ef 10.0.14
├── .gitattributes  .gitignore  .dockerignore  .env.example
├── Dockerfile  docker-compose.yml
├── .github/workflows/backend-tests.yml
├── scripts/export-contract.ps1
├── README.md  Memory.md  CLAUDE.md  docs/ai-log.md
├── src/
│   ├── TrackManagement.Domain/
│   │   ├── Common/ (BaseEntity<TKey>, IAuditableEntity)
│   │   └── Constants/Roles.cs
│   ├── TrackManagement.Application/
│   │   ├── Common/Messaging/ (ICommandMarker, ICommand, IQuery, ICommandHandler, IQueryHandler)
│   │   ├── Common/Behaviors/ (LoggingBehavior, ValidationBehavior, UnitOfWorkBehavior)
│   │   ├── Common/Persistence/ (IRepository, IReadRepository, IUnitOfWork)
│   │   ├── Common/Models/PagedResult.cs
│   │   └── DependencyInjection.cs
│   ├── TrackManagement.Infrastructure/
│   │   ├── Persistence/ (AppDbContext, EfRepository, UnitOfWork, DatabaseInitializer, ISeeder,
│   │   │                 DatabaseOptions, Configurations/, Migrations/)
│   │   ├── Auth/JwtOptions.cs
│   │   └── DependencyInjection.cs
│   └── TrackManagement.Api/
│       ├── Program.cs
│       ├── Endpoints/HealthEndpoints.cs  (+ IEndpointGroup convention)
│       ├── Health/ (HealthReportDto, HealthResponseWriter)
│       ├── Extensions/ResultExtensions.cs
│       ├── ExceptionHandlers/GlobalExceptionHandler.cs
│       ├── OpenApi/BearerSecuritySchemeTransformer.cs
│       └── appsettings.json  appsettings.Development.json (no secrets)
└── tests/
    └── TrackManagement.UnitTests/
        ├── Application/Common/Behaviors/  (Validation, UnitOfWork)
        ├── Infrastructure/Persistence/    (EfRepository does not save; UnitOfWork saves)
        └── Api/ (ResultExtensions mapping, Health endpoint, ProblemDetails for 404/500)
```

**Structure Decision**: Single backend repo with `src/` (4 projects) + `tests/` (one unit/API test
project with folders mirroring `src`). Mirrors the reference's project split but moves tests under
`tests/` and uses Central Package Management so every version is pinned in one file.

## Key design decisions (details in research.md)

1. **Pipeline**: Logging → Validation (→ `Result.Invalid`) → UnitOfWork (`where TRequest : ICommandMarker`,
   commits only on Ok/Created/NoContent).
2. **Repository**: `EfRepository<T>` overrides `SaveChangesAsync` → no-op; `UnitOfWork` saves.
3. **Result → HTTP**: `ToHttpResult()` / `ToCreatedHttpResult(locationFactory)`; Invalid →
   `ValidationProblem` (field names camelCased), others → `Problem(statusCode, title, detail)`.
4. **Health**: `/health` (infra) and `/api/health` (contract) share `HealthReportDto`; 503 when not Healthy.
5. **Docs**: `MapOpenApi` + `MapScalarApiReference` inside `if (app.Environment.IsDevelopment())`;
   Bearer scheme added by `IOpenApiDocumentTransformer`; contract exported from the running app by a contract test (research R6, revised).
6. **Config**: options + `ValidateOnStart`; startup fails on missing connection string or short JWT key.
7. **Startup DB**: `Database:MigrateOnStartup` (true in compose/Development, false default);
   failure → exit non-zero.
8. **JSON**: camelCase, `JsonStringEnumConverter(camelCase, allowIntegerValues: false)`.
9. **CORS**: named policy from `Cors:AllowedOrigins` (compose: `http://localhost:8081,http://localhost:5173`).
10. **Logging**: Serilog console, request logging; no file sink in containers.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `/api/health` endpoint calls `HealthCheckService` directly instead of going through `ISender` | Health is an infrastructure concern provided by ASP.NET Core; wrapping it in a query adds an Application interface with no business value | A `GetHealthQuery` would need an `IHealthReporter` abstraction in Application purely to forward the framework call |
| Empty `InitialCreate` migration | Proves tool manifest → migration → startup migrate path in Phase 0 | Waiting until Phase 1 would leave FR-002 (schema prepared automatically) untestable now |
