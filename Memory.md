# Memory – backend (repo snapshot)

> Synced from the master Memory.md at the end of each phase. Master lives outside the repos.

## Conventions
- .NET 10 Minimal APIs, Clean Architecture (Domain/Application/Infrastructure/Api), tests in `tests/TrackManagement.UnitTests` mirroring `src`.
- MediatR **12.5.0 exactly** (last Apache-2.0). `ICommand<T>`/`IQuery<T>` + `ICommandHandler`/`IQueryHandler`.
- Pipeline: Logging → Validation (`Result.Invalid`) → UnitOfWork (**commands only**, `where TRequest : ICommandMarker`; commits on Ok/Created/NoContent).
- Repositories (`EfRepository<T>`, Ardalis 9.3.1) never save; `IUnitOfWork` does.
- Endpoints return plain DTOs; every error is ProblemDetails (`ResultExtensions.ToHttpResult`).
- Routes under `/api/...`; reads public, writes Admin-only (from Phase 1).
- Tests: xUnit v3 + NSubstitute + **Shouldly** (no FluentAssertions); run `dotnet test --solution TrackManagement.slnx` (MTP).
- License check before any package; versions pinned in `Directory.Packages.props` with lock files.
- Contract: `specs/<feature>/contracts/openapi.json`, refreshed with `scripts/export-contract.ps1`; drift fails tests.
- Commits: `type(scope): what and why`; never commit to main, never squash/force-push.

## Pinned versions
SDK 10.0.403 (rollForward latestFeature) · ASP.NET/EF Core 10.0.14 · Npgsql EF 10.0.3 · EFCore.NamingConventions 10.0.1 ·
MediatR 12.5.0 · FluentValidation 12.2.0 · Ardalis.Result 10.1.0 / Specification 9.3.1 / GuardClauses 5.0.0 ·
Scalar.AspNetCore 2.18.7 · Serilog.AspNetCore 10.0.0 · xunit.v3 4.0.2 · NSubstitute 6.2.0 · Shouldly 4.3.0 ·
coverlet.MTP 10.1.0 · Microsoft.Testing.Extensions.TrxReport 2.5.1 · images sdk 10.0.403-alpine3.24, aspnet 10.0.14-alpine3.24, postgres 17.11-alpine3.24.

## State
- Phase 0 (`001-project-foundation-and-health-check`): implemented; health on `/health` + `/api/health`, empty InitialCreate migration, Docker, CI.
- Next: Phase 1 – JWT authentication & authorization (`002-jwt-authentication-and-authorization`).
