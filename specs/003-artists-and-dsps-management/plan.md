# Implementation Plan: Artists & Distribution Platforms (API service)

**Branch**: `003-artists-and-dsps-management` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-artists-and-dsps-management/spec.md`

**Skills applied**: dotnet-clean-architecture, git-workflow, decisions-log

## Summary

Add the first catalog slice: `Artist` and `Dsp` entities, migration `AddArtistsAndDsps` (DSPs
seeded with fixed ids via `HasData`), `POST /api/artists` (Admin policy from Phase 1),
`GET /api/artists` (public, paged, email only for admins) and `GET /api/dsps` (public). Close
Phase 0 FR-013: the service refuses to start on a non-UTF-8 database, name limits count code points,
and an integration test round-trips Arabic/mixed names through a real PostgreSQL 17 container.
Also fill audit timestamps (`IAuditableEntity`, unused since Phase 0), map malformed requests to
400/413 instead of 500, and cap request bodies.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.403, runtime 10.0.14)

**Primary Dependencies**: as Phase 1. New, test-only: `Testcontainers.PostgreSql` 4.16.0 (MIT,
published 2026-10-08; 4.17.0 is < 2 weeks old). Transitive: Testcontainers 4.16.0,
Docker.DotNet.Enhanced(.X509) 4.3.3, BouncyCastle.Cryptography 2.7.0, SSH.NET 2026.0.0,
SharpZipLib 1.4.2 — all MIT (checked on nuget.org).

**Storage**: PostgreSQL 17 (UTF8, `C.UTF-8`); tables `artists`, `dsps` (snake_case).

**Testing**: xUnit v3 + NSubstitute + Shouldly; API tests on EF InMemory (existing
`TestApiFactory`); new `Integration/` tests on a Testcontainers `postgres:17.11-alpine3.24`
(same image as compose) for Unicode round-trip, encoding check, unique index and ordering.

**Target Platform**: Linux containers (compose); AWS later.

**Project Type**: web-service (API)

**Performance Goals**: any artists page < 1 s with 1,000 artists (SC-003): one COUNT + one paged
projection query, `AsNoTracking`.

**Constraints**: Admin-only writes; email hidden from anonymous readers; no 500 for client
mistakes; bodies ≤ 32 KB.

**Scale/Scope**: 2 entities, 3 endpoints, 1 migration, 1 new test package.

## Constitution Check

| Principle | Gate | Status |
|---|---|---|
| I. Clean Architecture & CQRS | `CreateArtistCommand` (ICommand) and `ListArtistsQuery`/`ListDspsQuery` (IQuery) in `Features/Artists`, `Features/Dsps`; entities in Domain without packages; repositories + specs; UoW commits the create | ✅ |
| II. Thin HTTP edge | `ArtistEndpoints`, `DspEndpoints` only build requests and map Results; conflicts/validation as ProblemDetails | ✅ |
| III. Tests ship with features | domain, validator (each rule), handlers, specs, API (auth, paging, email visibility, 400/409/413), integration on real Postgres | ✅ |
| IV. Security by default | `.RequireAdmin()` on POST; email PII only for admins; body size cap; malformed input → 400 not 500; unique index guards races | ✅ |
| V. Spec-driven | spec → clarify → plan → tasks → analyze; contract exported to `contracts/openapi.json` at implementation; walkthrough + Memory updated in the PR (constitution 1.1.0) | ✅ |
| VI. Simplicity & licenses | one MIT test-only package; DSPs as migration data (no admin UI); country list as a static set | ✅ |

Post-design re-check: ✅ (see Complexity Tracking for the 409-with-field-errors mapping).

## Project Structure

### Documentation (this feature)

```text
specs/003-artists-and-dsps-management/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/artists-api.md        # reviewed intent; openapi.json exported at implementation
├── checklists/requirements.md
└── tasks.md                        # /speckit-tasks
```

### Source Code (additions/changes)

```text
src/TrackManagement.Domain/
  Artists/Artist.cs                    # Create(name, countryCode, email): trims, upper-cases, normalises email
  Countries/CountryCodes.cs            # 249 officially assigned ISO 3166-1 alpha-2 codes + IsAssigned()
  Dsps/Dsp.cs                          # Id, Name; Dsp.Seed (fixed ids Spotify/Apple Music/YouTube Music)
src/TrackManagement.Application/
  Common/Validation/TextRules.cs       # code-point length, email format (shared with LoginCommandValidator)
  Common/Validation/ValidationErrorCodes.cs   # "conflict"
  Common/Models/PageRequest.cs         # Page/PageSize defaults + validator rules
  Features/Artists/ArtistDto.cs, Specifications/{ArtistByNormalizedEmailSpec, ArtistsPageSpec, ArtistsCountSpec}.cs
  Features/Artists/Commands/CreateArtist/{CreateArtistCommand, …Validator, …Handler}.cs
  Features/Artists/Queries/ListArtists/{ListArtistsQuery, …Validator, …Handler}.cs
  Features/Dsps/DspDto.cs, Specifications/DspsSpec.cs, Queries/ListDsps/{ListDspsQuery, …Handler}.cs
src/TrackManagement.Infrastructure/
  Persistence/Configurations/{ArtistConfiguration, DspConfiguration}.cs
  Persistence/AuditableEntityInterceptor.cs   # CreatedAtUtc/UpdatedAtUtc from TimeProvider
  Persistence/DatabaseEncodingCheck.cs        # SHOW server_encoding → must be UTF8 (relational only)
  Persistence/Migrations/<ts>_AddArtistsAndDsps.cs
src/TrackManagement.Api/
  Endpoints/{ArtistEndpoints, DspEndpoints}.cs
  ExceptionHandlers/BadRequestExceptionHandler.cs     # BadHttpRequestException → its 400/413 ProblemDetails
  ExceptionHandlers/UniqueConstraintExceptionHandler.cs  # 23505 on known index → 409 with errors.{field}
  Security/RequestBodyLimit.cs                         # Kestrel limit + Content-Length guard → 413
  Extensions/ResultExtensions.cs                       # Invalid with only "conflict" codes → 409 ValidationProblem
tests/TrackManagement.UnitTests/
  Domain/{ArtistTests, CountryCodesTests}.cs
  Application/Artists/…, Application/Dsps/…
  Api/Artists/{CreateArtistEndpointTests, ListArtistsEndpointTests}.cs, Api/Dsps/ListDspsEndpointTests.cs
  Api/{MalformedRequestTests, RequestBodyLimitTests}.cs
  Integration/{PostgresFixture, UnicodeRoundTripTests, ArtistPersistenceTests, DatabaseEncodingCheckTests}.cs
```

**Structure Decision**: unchanged layout; new `Features/Artists`, `Features/Dsps` slices and Domain
folders per aggregate.

## Key design decisions (details in research.md)

1. **Email uniqueness**: `NormalizedEmail` column (`Trim().ToUpperInvariant()`, like Identity) with a
   unique index; validator pre-check via `ArtistByNormalizedEmailSpec` returns a `conflict`-coded
   error; the index catches races (R1).
2. **409 with field errors**: `ResultExtensions` maps an Invalid result whose errors are all
   `conflict`-coded to 409 ValidationProblem (`errors.email`); `UniqueConstraintExceptionHandler`
   maps index `ix_artists_normalized_email` violations to the same shape (R2).
3. **Email visibility**: endpoint passes `IncludeEmail = user is Admin` into `ListArtistsQuery`; the
   projection spec selects email or `null`; `ArtistDto.Email` is omitted from JSON when null (R3).
4. **Ordering/paging**: `ORDER BY lower(name), created_at_utc, id`; `PageRequest` validator
   (page ≥ 1, 1 ≤ pageSize ≤ 50 → 400); count spec + paged projection spec; `PagedResult<ArtistDto>` (R4).
5. **Countries**: static `CountryCodes` set (249 codes, ISO 3166-1 as of 2026); input trimmed +
   upper-cased; `char(2)` column. A SHA-256 of the sorted list is asserted in tests and in the
   frontend's copy to keep both in sync (R5).
6. **Text limits in code points** (`string.EnumerateRunes().Count()`), matching PostgreSQL
   `varchar(n)`; names 200, email 254 (R6).
7. **UTF-8**: `DatabaseEncodingCheck` runs in `DatabaseInitializer` after migrations (Npgsql only) and
   throws "Database encoding is X; UTF8 is required" → startup fails. Compose already sets UTF8; the
   Phase 6 RDS script must too (R7).
8. **Real-Postgres tests** with Testcontainers 4.16.0 using the compose image; marked
   `[Trait("Category","Integration")]`; CI (ubuntu, Docker available) runs them; locally they need
   Docker Desktop (R8).
9. **DSPs as migration data** (`HasData`) with fixed GUIDs and unique `name`: present after every
   migrate, never duplicated, same ids everywhere (R9).
10. **Audit timestamps**: `AuditableEntityInterceptor` (scoped, `TimeProvider`) sets
    `CreatedAtUtc` on Added and `UpdatedAtUtc` on Modified (R10).
11. **Malformed input**: `BadRequestExceptionHandler` (registered before the global handler) turns
    `BadHttpRequestException` into a ProblemDetails with its status (400, 413) — today Development
    returns 500 for invalid JSON (R11).
12. **Body limit**: `Api:MaxRequestBodyBytes` (default 32 KB) applied to Kestrel and checked early on
    `Content-Length` → 413 ProblemDetails (TestServer does not enforce Kestrel limits) (R12).
13. **Create response**: `201` + `Location: /api/artists/{id}` + full `ArtistDto` (with email; the caller
    is an admin). There is no GET-by-id yet; the Location is reserved for Phase 3 (R13).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Duplicate email as 409 that still carries `errors.email` (ValidationProblem shape with 409) | Clarified behaviour: conflict status + field-level message for the form | Plain 409 detail loses the field; 400 contradicts the clarification |
| `Location` header points to a route that arrives in Phase 3 | REST convention for 201 | Adding GET-by-id now is unrequested scope |
| Test-only dependency on Docker (Testcontainers) | FR-013/SC-002 must be proven on the real engine; InMemory ignores encoding and collation | A skipped env-var-based test proves nothing when skipped |
