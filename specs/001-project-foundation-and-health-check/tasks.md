---
description: "Task list for Phase 0 – Foundation & Architecture (API service)"
---

# Tasks: Foundation & Architecture (API service)

**Input**: Design documents from `specs/001-project-foundation-and-health-check/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.json, quickstart.md
**Tests**: REQUIRED by the constitution (Principle III). Every story includes its test tasks.
**Skills**: dotnet-clean-architecture (all code), git-workflow (commits), decisions-log (T040)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on unfinished tasks)
- Paths are relative to the backend repo root.

---

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 Add `.gitattributes` (LF default, CRLF for *.ps1), `.gitignore` (dotnet, `.env`, `openapi/` build output, coverage), `.editorconfig` at repo root
- [ ] T002 Add `global.json` (SDK 10.0.403, rollForward latestFeature) and `.config/dotnet-tools.json` (dotnet-ef 10.0.14)
- [ ] T003 Add `Directory.Build.props` (net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion latest) and `Directory.Packages.props` with every pin from research.md R2 (`ManagePackageVersionsCentrally`, `CentralPackageTransitivePinningEnabled`)
- [ ] T004 Create `TrackManagement.slnx` with `src/TrackManagement.{Domain,Application,Infrastructure,Api}` and `tests/TrackManagement.UnitTests`, project references Api→Infrastructure→Application→Domain, tests→all
- [ ] T005 Verify `dotnet restore` + `dotnet build` succeed with zero warnings; commit `chore(solution): ...`

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ No user-story work starts before this phase is done.**

- [ ] T006 [P] Domain base types in `src/TrackManagement.Domain/Common/BaseEntity.cs`, `IAuditableEntity.cs` and `Constants/Roles.cs` (Admin)
- [ ] T007 [P] CQRS abstractions in `src/TrackManagement.Application/Common/Messaging/` (ICommandMarker, ICommand, IQuery, ICommandHandler, IQueryHandler)
- [ ] T008 [P] Persistence abstractions in `src/TrackManagement.Application/Common/Persistence/` (IRepository, IReadRepository, IUnitOfWork) and `Common/Models/PagedResult.cs`
- [ ] T009 Behaviors in `src/TrackManagement.Application/Common/Behaviors/` (LoggingBehavior, ValidationBehavior → Result.Invalid, UnitOfWorkBehavior constrained to ICommandMarker, commits only on success) + `DependencyInjection.AddApplication()` registering MediatR 12.5.0, validators and behaviors in order
- [ ] T010 [P] Tests `tests/TrackManagement.UnitTests/Application/Common/Behaviors/ValidationBehaviorTests.cs` (invalid → Result.Invalid with field errors; valid → next called) and `UnitOfWorkBehaviorTests.cs` (command success commits once; command failure does not commit; **a query sent through a real MediatR container never invokes IUnitOfWork** — proves the ICommandMarker constraint selects commands only)
- [ ] T011 Infrastructure persistence in `src/TrackManagement.Infrastructure/Persistence/`: AppDbContext (snake_case, ApplyConfigurationsFromAssembly), EfRepository (SaveChangesAsync no-op), UnitOfWork, DatabaseOptions, ISeeder, DatabaseInitializer; `DependencyInjection.AddInfrastructure(config)` (Npgsql with retry, options with ValidateOnStart)
- [ ] T012 Tests (run EARLY, right after T011; if it fails, STOP and report options to the user) `tests/TrackManagement.UnitTests/Infrastructure/Persistence/EfRepositoryTests.cs` (AddAsync does not persist until UnitOfWork.SaveChangesAsync; spec List/Count work on EF Core 10 – guards research R3) using EF InMemory with a test-only entity
- [ ] T013 `src/TrackManagement.Api/Extensions/ResultExtensions.cs` (`ToHttpResult`, `ToCreatedHttpResult`) per dotnet-clean-architecture patterns
- [ ] T014 [P] Tests `tests/TrackManagement.UnitTests/Api/ResultExtensionsTests.cs` (each ResultStatus → status code; Invalid → ValidationProblem with camelCase keys; errors are ProblemDetails; success never returns the Result wrapper)
- [ ] T015 `src/TrackManagement.Api/Program.cs` skeleton: Serilog console, AddApplication/AddInfrastructure, JSON options (camelCase, string enums), endpoint group convention `Endpoints/IEndpointGroup` + `MapEndpoints()`; `appsettings.json` (no secrets) and `appsettings.Development.json` (no secrets)

**Checkpoint**: solution builds, foundational tests green.

---

## Phase 3: User Story 1 – Start the API locally with one command (P1) 🎯 MVP

**Goal**: one command starts db + api; schema prepared; health reports healthy/unhealthy.
**Independent Test**: quickstart.md "US1".

### Tests for User Story 1

- [ ] T016 [P] [US1] `tests/TrackManagement.UnitTests/Api/TestApiFactory.cs` (WebApplicationFactory, Testing environment, EF InMemory, test config values)
- [ ] T017 [P] [US1] `tests/TrackManagement.UnitTests/Api/HealthEndpointTests.cs`: `/health` and `/api/health` return 200 + contract body when healthy; 503 + `unhealthy` when a failing check is registered; body has no exception text
- [ ] T018 [P] [US1] `tests/TrackManagement.UnitTests/Infrastructure/ConfigurationValidationTests.cs`: missing connection string or JWT key < 32 chars fails options validation

### Implementation for User Story 1

- [ ] T019 [US1] `src/TrackManagement.Infrastructure/Auth/JwtOptions.cs` (Issuer, Audience, SigningKey MinLength 32, ExpirationMinutes 5–1440) bound + ValidateOnStart (auth middleware wiring deferred to Phase 1)
- [ ] T020 [US1] Health: `src/TrackManagement.Api/Health/HealthReportDto.cs`, `HealthResponseWriter.cs`; `AddHealthChecks().AddDbContextCheck<AppDbContext>("database")`; `MapHealthChecks("/health")` + `Endpoints/HealthEndpoints.cs` (`GET /api/health`, 200/503, `.Produces<HealthReportDto>(200).Produces<HealthReportDto>(503)`)
- [ ] T021 [US1] Startup migration: call DatabaseInitializer when `Database:MigrateOnStartup`; failure → log + rethrow (non-zero exit)
- [ ] T022 [US1] Create empty baseline migration `InitialCreate` in `src/TrackManagement.Infrastructure/Persistence/Migrations/` via `dotnet ef migrations add InitialCreate`
- [ ] T023 [US1] CORS named policy from `Cors:AllowedOrigins` in Program.cs (FR-012)
- [ ] T024 [US1] `Dockerfile` (sdk 10.0.403-alpine3.24 → aspnet 10.0.14-alpine3.24, restore layer, `USER $APP_UID`, `EXPOSE 8080`, wget HEALTHCHECK on /health) and `.dockerignore`
- [ ] T025 [US1] `docker-compose.yml` (db postgres:17.x-alpine on `127.0.0.1:5432`, volume, pg_isready healthcheck; api on 8080, `depends_on: db: condition: service_healthy`, env from `.env`, `Database__MigrateOnStartup=true`, `ASPNETCORE_ENVIRONMENT=Development`; db created with `POSTGRES_INITDB_ARGS=--encoding=UTF8 --locale=C.UTF-8` so Unicode (Arabic) text round-trips – FR-013, analyze E1; a Unicode round-trip test is added in Phase 2 with the first text entity; network `trackmgmt-net` with explicit name) and `.env.example`
- [ ] T026 [US1] Run quickstart US1 manually (compose up, 200, stop db → 503, missing key → exit); record evidence for the PR

**Checkpoint**: US1 independently demonstrable.

---

## Phase 4: User Story 2 – Pull requests are checked automatically (P1)

- [ ] T027 [US2] `.github/workflows/backend-tests.yml` per research R11 (pull_request → main; setup-dotnet from global.json; restore --locked-mode; build Release; test with TRX + XPlat coverage; upload `test-results` and `coverage` artifacts even on failure)
- [ ] T028 [US2] Enable NuGet lock files (`RestorePackagesWithLockFile`) and commit `packages.lock.json` files so CI restore is locked
- [ ] T029 [US2] README section "CI" incl. how to make `backend-tests` a required status check (branch protection)

**Checkpoint**: PR shows the check (verified when the Phase 0 PR is opened).

---

## Phase 5: User Story 3 – Interactive API documentation (P2)

### Tests for User Story 3

- [ ] T030 [P] [US3] `tests/TrackManagement.UnitTests/Api/OpenApiExposureTests.cs`: Development serves `/openapi/v1.json` (contains Bearer scheme + `/api/health`) and `/scalar`; Production returns 404 for both

### Implementation for User Story 3

- [ ] T031 [US3] `AddOpenApi("v1")` + `src/TrackManagement.Api/OpenApi/BearerSecuritySchemeTransformer.cs` + document info; `MapOpenApi()` and `MapScalarApiReference()` only in Development
- [ ] T032 [US3] Build-time export (`Microsoft.Extensions.ApiDescription.Server`, `OpenApiDocumentsDirectory=openapi`) + `scripts/export-contract.ps1 -Feature <dir>`; regenerate `specs/001-project-foundation-and-health-check/contracts/openapi.json` from the build and reconcile any diff with the hand-written contract

---

## Phase 6: User Story 4 – Errors are consistent and safe (P2)

### Tests for User Story 4

- [ ] T033 [P] [US4] `tests/TrackManagement.UnitTests/Api/ProblemDetailsTests.cs`: unknown route → 404 problem+json with traceId; test-only throwing endpoint → 500 generic detail, no exception message/stack; validation problem shape has `errors` dictionary

### Implementation for User Story 4

- [ ] T034 [US4] `AddProblemDetails` with traceId customisation, `src/TrackManagement.Api/ExceptionHandlers/GlobalExceptionHandler.cs`, `UseExceptionHandler()`, `UseStatusCodePages()`

---

## Phase 7: Polish & Cross-Cutting

- [ ] T035 [P] `README.md` skeleton: overview, links (frontend repo, DECISIONS.md placeholder), prerequisites, run with Docker / without Docker, migrations (`dotnet tool restore`, `dotnet ef migrations add <Name>`, `dotnet ef database update`), JWT (placeholder → Phase 1), tests, CI, project structure
- [ ] T036 [P] `docs/ai-log.md` with Phase 0 entry (decisions-log template)
- [ ] T037 [P] Repo-local `Memory.md` (conventions, pinned versions, current state, next action) and `CLAUDE.md` (imports Memory.md, keeps SPECKIT block)
- [ ] T038 Run full `dotnet test`, `docker compose up` smoke test, secret scan (`git grep -i` for password/key patterns), mark tasks done
- [ ] T039 Push branch and open PR with `gh pr create` (summary, test evidence, checklist) — after the user approves implementation
- [ ] T040 Update master Memory.md (phase table, versions) and append ai-log security/mistakes

---

## Dependencies & Execution Order

- Setup (T001–T005) → Foundational (T006–T015) → US1 (T016–T026) → US2 / US3 / US4 (independent of each other, all need Foundational; US2's check is only observable once a PR exists) → Polish.
- Within stories: tests are written alongside and must fail before the implementation task they cover.

## Parallel Example: Foundational

```text
T006 Domain base types   |  T007 CQRS abstractions  |  T008 Persistence abstractions
then T009 behaviors → T010 tests; T011 persistence → T012 tests; T013 → T014
```

## Implementation Strategy

MVP = Setup + Foundational + US1 (one-command healthy stack the frontend can call). Then US4 and
US3 (both small, used by every later phase), then US2 (CI). Commit after each task group with
descriptive Conventional Commits; stop at each checkpoint to verify.
