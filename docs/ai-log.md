# AI usage log

Running evidence for `DECISIONS.md` (written in Phase 5). Covers **both** repos:
[backend](https://github.com/yussifmohd/takwene-track-backend) and
[frontend](https://github.com/yussifmohd/takwene-track-frontend).
Entries are appended per phase and never rewritten.

## Phase 0 – Foundation & health check (001-project-foundation-and-health-check / 001-project-foundation-and-health-check) – 2026-11-17

### (a) Generated vs written
- AI generated (Claude Code, Spec Kit flow): constitution, spec, clarifications, plan, research,
  data model, contract, quickstart and tasks in both repos; the reference-project summary; the
  custom skills (dotnet-clean-architecture, react-frontend-stack, git-workflow, aws-infra-scripts,
  decisions-log).
- AI generated (backend code): solution layout, central package management, CQRS markers and
  handler interfaces, Logging/Validation/UnitOfWork behaviors, non-saving `EfRepository` + `UnitOfWork`,
  validated options, startup migrator, health endpoints, Result→HTTP mapping, ProblemDetails +
  global exception handler, OpenAPI/Scalar, Dockerfile, compose, CI workflow, all tests (48).
- Developer decided (interview, 29 answers): Admin-only writes with public reads, no register
  endpoint, MediatR pinned to 12.5.0, Shouldly instead of FluentAssertions, license check per
  package, exactly 3 DSPs, genre list, branch naming scheme, and the four approved deviations from
  the reference (handler interfaces, real Unit of Work, AsNoTracking projections, ProblemDetails).
  The developer also asked for a test proving the Unit of Work never runs for queries and an early
  Ardalis.Specification + EF Core 10 compatibility test.
- Developer wrote/changed: _Developer notes: (to be filled in by Yousef)_

### (b) Security
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| CORS `AllowAnyOrigin` | reference project | found by AI while reading the reference | not copied: named policy with an explicit origin allow-list from config | fixed by design |
| Account lockout disabled (`lockoutOnFailure: false`) | reference project | found by AI | not copied; Identity lockout to be enabled in Phase 1 | planned (Phase 1) |
| Login message reveals unconfirmed accounts (user enumeration) | reference project | found by AI | not copied; Phase 1 login returns one generic 401 | planned (Phase 1) |
| Migrate-on-startup swallows exceptions | reference project | found by AI | not copied: migration failure propagates and the process exits non-zero | fixed by design |
| Secrets in config | both | constitution rule | `ValidateOnStart` refuses to start without a connection string or with a JWT key < 32 chars; `.env` gitignored; `.env.example` placeholders only | done |
| Exception text in responses | API | AI design + tests | global handler returns a generic 500 ProblemDetails with traceId; `Result.Error` messages never echoed; health body has no exception text (tests assert a secret string is absent) | done |
| Exposed DB port | compose | AI design | Postgres published on `127.0.0.1` only | done |
| Container runs as root | Dockerfile | AI design | `USER $APP_UID` (verified `uid=1654(app)`) | done |
| API docs exposed in production | API | AI design | OpenAPI + Scalar mapped only in Development; test asserts 404 in Production | done |
| Health probe hanging ~60 s when DB down | health check | found by AI during manual quickstart | DB check capped at 3 s → fast 503 (avoids probe pile-up) | fixed (1c08f2f) |

### (c) Mistakes & fixes
- **Wrong description of the pipeline.** The Phase 0 summary said Logging → Validation → Unit of
  Work "run on every request". The Unit of Work behavior is constrained to `ICommandMarker` and
  never runs for queries. The developer caught it. Cause: I summarised the behavior registration
  order without restating the generic constraint. Fix: wording corrected, and
  `UnitOfWorkBehaviorTests` now prove the query pipeline is exactly [Logging, Validation] and that
  a query never calls `SaveChangesAsync` (c2675d2).
- **Compatibility test failed because of my test, not the library.** The first Ardalis 9.3.1 +
  EF Core 10 run failed because Arrange left entities tracked, so an "AsNoTracking" assertion saw
  tracked instances. Fix: `ChangeTracker.Clear()` after seeding; the library passed (ca9f2af).
- **Build-time OpenAPI export produced an empty contract.** The plan used
  `Microsoft.Extensions.ApiDescription.Server`; first it failed the build ("Unable to find service
  type IDocumentProvider"), and once made opt-in it produced a document with `"paths": {}`
  because generation stops the app before Minimal API endpoints are mapped. Running the export
  even overwrote the hand-written contract with the empty one (reverted). Fix: package removed;
  the contract is now exported from the running app by `OpenApiContractTests`, which also fails CI
  on drift (0aa0912).
- **Contract typed numbers as `number | string`.** ASP.NET's web JSON defaults allow numbers in
  strings, so the generated schema for `durationMs` was a union with a regex. Fix:
  `JsonNumberHandling.Strict` (6249a79).
- **VSTest assumptions under .NET 10.** The plan's `dotnet test --logger trx --collect "XPlat Code
  Coverage"` and `coverlet.collector` are VSTest-only; xunit.v3 runs on Microsoft.Testing.Platform.
  Fix: MTP runner in `global.json`, `coverlet.MTP` + `Microsoft.Testing.Extensions.TrxReport`
  (both MIT), CI uses `--report-trx --coverlet` (d545198).
- **Serilog bootstrap logger broke multiple test hosts.** `CreateBootstrapLogger()` can only be
  frozen once per process, so the second in-memory host failed with "The logger is already frozen".
  Fix: a plain startup logger (ebe1bd9).
- **`.gitignore` pattern `openapi/` also ignored `src/.../OpenApi/`** on the case-insensitive
  Windows filesystem, so source files were silently left out of a commit. Fix: anchored to
  `/openapi/` (5bc5d58).
- **Port 5432 already in use** by a local Postgres on the dev machine; compose failed to start.
  Fix: host port configurable via `POSTGRES_HOST_PORT` (default 5432).

### Frontend
_Appended when the frontend Phase 0 implementation is complete._
