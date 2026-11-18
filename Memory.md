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
- Auth (Phase 1): write endpoints use `.RequireAdmin()` (policy `AdminOnly`); reads stay anonymous. Current user via `ICurrentUser`.
  JWT HS256 only, iss/aud/lifetime/signature validated, skew 30 s, claims `sub/email/role/jti`. Rules live in `JwtValidation`.
- Trusted proxies: `ReverseProxy:KnownNetworks` (CIDR list, config only). Compose `172.16.0.0/12`; AWS → ALB VPC CIDR (Phase 6).
- Admin: `Admin:Email`/`Admin:Password` (env `ADMIN__*`), seeded if missing, never modified. Identity schema pinned `Version2` (no passkeys).
- Artists/DSPs (Phase 2): reads public, email only for admins (`ArtistDto.Email` omitted when null; OpenAPI transformer keeps it optional). Validation conflicts use error code `conflict` → 409 with `errors.<field>`; unique-index races → `UniqueConstraintExceptionHandler`. Lengths count code points (`TextRules`). Country list `CountryCodes` (249, `ListHash` shared with the frontend). DSP ids fixed in `Dsp` + migration `HasData`.
- `IAuditableEntity` is filled by `AuditableEntityInterceptor` (TimeProvider); set `CreatedAtUtc` in the factory when the response needs it before save.
- Errors: `BadRequestExceptionHandler` (malformed input → 400/413 in every env) → `UniqueConstraintExceptionHandler` → `GlobalExceptionHandler`. Body cap `Api:MaxRequestBodyBytes` 32 KB.
- Logging: `UseSerilog(..., preserveStaticLogger: true)` (one logger per host) and request logging first in the pipeline with the host logger, so it logs the status the client got.
- Startup refuses a non-UTF8 database (`DatabaseEncodingCheck`).
- Tracks (Phase 3): rules only in `TrackStatusRules` (`Track.ChangeStatus` returns `StatusChange`, handler → `Result.Conflict`). Detail lists `allowedNextStatuses`. `Track.Version` = `xmin` (`IsRowVersion`), lost race → `ConcurrencyExceptionHandler` 409 (generic message). ISRC `char(12)` + `ix_tracks_isrc` (unique handler → `errors.isrc`). Genre/status/date bound as strings → validator 400 per field; `FixedValuesSchemaTransformer` adds enums only on track request/query members. Interceptor keeps a domain-set `UpdatedAtUtc`.
- Distribution (Phase 4): `TrackDistribution` is a child of the `Track` aggregate (PK `pk_track_distributions` track+DSP, checks on status and "answered exactly when not pending"). `Track.Distribute` (new → pending, rejected → re-sent, pending/live untouched, submitted → distributed) and `RecordAnswer` (pending → live|rejected); every delivery change stamps `UpdatedAtUtc` so `xmin` serialises races. PATCH → distributed is 409 without a delivery (`TrackStatusRules.Check(from, to, hasDistributions)`), so in practice only Distribute reaches it. Distribute: 1+ ids, ≤ 20 distinct, unknown → 400 `dspIds`, always 200 + track. Detail: `canDistribute`, deliveries by name with `allowedAnswers`. Command responses get DSP names via `DspNames.LoadAsync`.
- **Local databases created before Phase 4** may hold distributed tracks without deliveries: reset with `docker compose down -v` (no data migration, user decision). Phase 5's seed creates consistent data.
- README rule (constitution v1.2.0): README describes the finished product (fixed structure, no phase numbers or changelogs), updated only where the product changed.
- Artists: `?search=` (substring, `strpos`) and `GET /api/artists/{id}` (email admin-only).
- Integration tests: `tests/.../Integration`, `[Collection(PostgresDatabase.Name)]` + `[Trait("Category","Integration")]`, Testcontainers `postgres:17.11-alpine3.24`; fixture must pin the Identity schema like design time. Skip with `-- --filter-not-trait "Category=Integration"`; CI runs all.
- EF migrations are generated code (`.editorconfig`); the Dockerfile copies `.editorconfig` so the image builds with the same analyzer rules.
- Sample data (Phase 5): `Persistence/Seeding/SampleCatalog` (17 artists, 32 tracks, built only through the domain; ISRC prefix `XXSMP26`, `@example.com`) + `SampleCatalogSeeder` (Order 20, only when no artist and no track, transaction + `pg_advisory_xact_lock`, inside `CreateExecutionStrategy`). Real pairings and sources: `specs/006-…/data-model.md`. `TestApiFactory(withSampleCatalog: true)` to include it in API tests.
- Security (Phase 5): change routes use `.RequireAdminWrite()` (Admin + `writes` limit); `ChangeRoutesRequireAdminTests` enumerates the route table. Limits `RateLimiting:Login` 10, `Api` 300 per IP (health exempt), `Writes` 60 per user, all per 60 s, validated on start; `UseRateLimiter` after `UseAuthentication`. `UseSecurityHeaders()` (nosniff, DENY, no-referrer, CSP deny-all except Development `/scalar` + `/openapi`, `no-store` on `/api`). `WebAppCors` validates origins, GET/POST/PATCH only. Review + evidence: `docs/security-review.md`; re-scan images with Trivy on every rebuild.
- Docs: `DECISIONS.md` (both repos), `docs/security-review.md`, `docs/study-guide.md`. Branch protection is NOT enabled on GitHub (README says so).
- API tests: `TestApiFactory` seeds the admin; test-only `/test/admin-only` + `/test/public` live there (excluded from OpenAPI); `X-Test-Client-IP` sets the remote IP.

## Pinned versions
SDK 10.0.403 (rollForward latestFeature) · ASP.NET/EF Core 10.0.14 · Npgsql EF 10.0.3 · EFCore.NamingConventions 10.0.1 ·
MediatR 12.5.0 · FluentValidation 12.2.0 · Ardalis.Result 10.1.0 / Specification 9.3.1 / GuardClauses 5.0.0 ·
Scalar.AspNetCore 2.18.7 · Serilog.AspNetCore 10.0.0 · xunit.v3 4.0.2 · NSubstitute 6.2.0 · Shouldly 4.3.0 ·
Identity.EntityFrameworkCore 10.0.14 · Testcontainers.PostgreSql 4.16.0 (MIT; transitives Testcontainers 4.16.0, Docker.DotNet.Enhanced 4.3.3, BouncyCastle 2.7.0, SSH.NET 2026.0.0, SharpZipLib 1.4.2 – all MIT) · coverlet.MTP 10.1.0 · Microsoft.Testing.Extensions.TrxReport 2.5.1 · images sdk 10.0.403-alpine3.24, **aspnet 10.0.15-alpine3.24**, postgres 17.11-alpine3.24.
- **Exception to the two-week rule (security patch, user decision 2026-11-18)**: runtime image aspnet 10.0.14 → 10.0.15-alpine3.24 (published 2026-11-10, tag pin; the project pins no digests). Trivy 0.76.1 before: 6 findings – CVE-2026-114146 (HIGH, elevation of privilege), CVE-2026-114147 and CVE-2026-114149 (HIGH, DoS), CVE-2026-109476 (MEDIUM, information disclosure), CVE-2026-109475 (MEDIUM, .NET DoS), zlib CVE-2026-4738 (MEDIUM). After (rebuilt `--no-cache`): 0. SDK image and NuGet packages unchanged (not shipped / not flagged). **To do if time before submission: bump SDK 10.0.404 and NuGet 10.0.15 once they pass two weeks on 2026-11-24.**
- Scan tools (run from images, not dependencies): Trivy `aquasec/trivy:0.76.1` (Apache-2.0, pushed 2026-10-21), gitleaks `zricethezav/gitleaks:v8.30.1` (MIT, 2026-03-21).

## State
- Phase 0 (`001-project-foundation-and-health-check`): implemented; health on `/health` + `/api/health`, empty InitialCreate migration, Docker, CI.
- Phase 1 (`002-jwt-authentication-and-authorization`): login + me, lockout, rate limit, Admin policy, admin seeding, CI on Node 24 actions, GSS log fix; 127 tests.
- Phase 2 (`003-artists-and-dsps-management`): artists (create admin-only, public paged list, email admin-only), DSPs (3, fixed ids), UTF-8 check, audit timestamps, malformed → 400, body cap, Serilog per-host fix; 239 tests (13 PostgreSQL).
- Phase 3 (`004-track-catalog-with-filters-and-status`): tracks (create admin, public filtered list + detail, status PATCH with domain rules + xmin), artist search/GET by id; 397 tests (24 PostgreSQL).
- Phase 4 (`005-track-distribution-to-dsps`): deliveries to platforms (distribute, simulated answers, re-send), PATCH → distributed refused without a delivery, README rewritten to the README rule; 471 tests (31 PostgreSQL).
- Phase 5 (`006-seed-data-security-hardening-and-docs`): sample catalogue, route-table admin test, global/write rate limits, security headers, CORS validation, runtime image 10.0.15 (exception), security review, DECISIONS.md, study guide, finished README; 523 tests.
- Next: Phase 6 (optional) – AWS scripts (`007-aws-infrastructure-scripts`); bump SDK 10.0.404 / NuGet 10.0.15 after 2026-11-24 if time.
