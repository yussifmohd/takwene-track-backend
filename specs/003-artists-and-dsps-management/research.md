# Research: Artists & Distribution Platforms (API service)

## R1 – Case-insensitive unique email
- **Decision**: store `email` as entered (trimmed) and `normalized_email = upper(trim(email))` with a
  unique index `ix_artists_normalized_email` (NULLs allowed many times in PostgreSQL).
- **Why**: same pattern as Identity's `NormalizedEmail`; works identically on EF InMemory and Postgres;
  no extension needed.
- **Rejected**: `citext` (extension + InMemory mismatch); expression index on `lower(email)` (EF
  fluent API cannot model it without raw SQL, and the validator query would need the same expression).

## R2 – Conflict response that names the field
- **Decision**: `CreateArtistCommandValidator` checks `AnyAsync(ArtistByNormalizedEmailSpec)` and
  emits a failure with `ErrorCode = ValidationErrorCodes.Conflict`. `ResultExtensions`: an Invalid
  result whose errors are *all* conflict-coded → `Results.ValidationProblem(errors, statusCode: 409,
  title: "Conflict")`; mixed with other errors → normal 400 with all of them.
  `UniqueConstraintExceptionHandler`: `DbUpdateException` with `PostgresException { SqlState: "23505",
  ConstraintName: "ix_artists_normalized_email" }` → the same 409 body (race between two creates).
  Unknown constraint names → 409 without `errors`.
- **Why**: one shape for the web form (`getFieldErrors` already reads `errors`), correct status for a
  conflict, and the index remains the real guard.
- **Rejected**: returning `Result.Conflict(string)` from the handler (Ardalis Conflict carries only
  strings; the form could not place it).

## R3 – Hiding email from anonymous readers
- **Decision**: `ArtistEndpoints` reads `ICurrentUser`; `IncludeEmail = IsAuthenticated && Roles contains Admin`.
  `ArtistsPageSpec(query)` projects `Email = includeEmail ? a.Email : null`; `ArtistDto.Email` has
  `[JsonIgnore(Condition = WhenWritingNull)]`. The endpoint stays anonymous; an invalid token is
  rejected by authentication only on endpoints that require it, so a bad token here simply means
  "anonymous" (documented).
- **Rejected**: separate admin endpoint (duplicate paging code for one field).

## R4 – Ordering and paging
- `Query.OrderBy(a => a.Name.ToLower()).ThenBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)` (Npgsql →
  `lower(name)`; with `C.UTF-8` lower() folds Latin case; Arabic has no case).
- `PageRequest(int Page = 1, int PageSize = 20)` + shared validator rules (`PagedResult.MaxPageSize` = 50).
- Count spec and page spec share a filter base (no filters yet). Page past the end: empty `items`, real totals.
- Non-numeric query values fail model binding → `BadHttpRequestException` → 400 ProblemDetails (R11).

## R5 – Country codes
- **Decision**: `Domain/Countries/CountryCodes` – a `FrozenSet<string>` of the 249 officially
  assigned alpha-2 codes (ISO 3166-1, current as of 2026). `IsAssigned(code)` after trim + upper.
- **Why**: `RegionInfo` depends on ICU data and accepts some non-ISO names; a static list is
  deterministic and reviewable.
- **Cross-repo sync**: tests assert count = 249, spot checks (EG, SA, AE, PS, SS accepted; UK, EU,
  XK, ZZ refused) and `SHA-256("AD,AE,…,ZW")`; the frontend's list asserts the same hash.

## R6 – Length in characters
- .NET `string.Length` counts UTF-16 units (emoji = 2); PostgreSQL `varchar(n)` counts code points.
  Validators use `value.EnumerateRunes().Count()` so the API limit equals the column limit
  (an Arabic or emoji name exactly at 200 code points is accepted and stored).

## R7 – UTF-8 database
- Compose already initialises with `--encoding=UTF8 --locale=C.UTF-8` (Phase 0). New
  `DatabaseEncodingCheck.EnsureUtf8Async(DbContext)` runs `SHOW server_encoding` after migrations when
  the provider is Npgsql; anything other than `UTF8` throws `InvalidOperationException("Database
  encoding is {x}; UTF8 is required for Arabic and other scripts.")` → process exits 1.
- Npgsql always uses UTF-8 as client encoding. Phase 6: RDS `create-db-instance` must not override the
  default (UTF8) — noted for the aws-infra-scripts skill.

## R8 – Real-database tests
- `Testcontainers.PostgreSql` 4.16.0 (MIT) with image `postgres:17.11-alpine3.24` and the compose initdb
  args. One `PostgresFixture` (xUnit v3 assembly fixture) starts one container, applies migrations once;
  each test uses its own transaction or truncates `artists`.
- Tests: Unicode round-trip (create via `IRepository` + UoW, read via the page spec, compare with
  `string.Equals(..., Ordinal)` and code points), name at 200 code points, unique index violation →
  `DbUpdateException 23505`, `lower(name)` ordering, encoding check passes on UTF8 and fails on a
  `SQL_ASCII` database created in the same container.
- CI: `ubuntu-latest` has Docker; no workflow change beyond the longer run.

## R9 – DSP seed
- `HasData` with fixed GUIDs in `Dsp.Seed`: Spotify `0d1f6b9e-5c2a-4c7e-9f4b-2b8a7c1e0001`, Apple Music
  `…0002`, YouTube Music `…0003` (constants in Domain). Unique index on `name`.
- **Why**: part of the reviewed migration, applied by `MigrateAsync` and by `EnsureCreated` in tests;
  idempotent by construction.
- **Rejected**: an `ISeeder` (more code, same result; seeders stay for environment-specific data like the admin).

## R10 – Audit timestamps
- `AuditableEntityInterceptor : SaveChangesInterceptor` (scoped, injected `TimeProvider`) sets
  `CreatedAtUtc` (Added) and `UpdatedAtUtc` (Modified) as UTC `DateTime`; registered with
  `AddDbContext((sp, o) => o.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()))`.

## R11 – Malformed requests
- Minimal APIs throw `BadHttpRequestException` in Development (`ThrowOnBadRequest`) and write a bare
  400 elsewhere. New `BadRequestExceptionHandler : IExceptionHandler` registered before
  `GlobalExceptionHandler` writes ProblemDetails with `exception.StatusCode` and a generic detail
  ("The request body or parameters are malformed."). Tests in Development and Testing environments.

## R12 – Body size
- `Api:MaxRequestBodyBytes` (default 32768, validated > 0). Kestrel `Limits.MaxRequestBodySize` set
  from it; a small middleware returns 413 ProblemDetails when `Content-Length` exceeds it (works in
  TestServer, which ignores Kestrel limits). Chunked bodies are bounded by Kestrel.

## R13 – Create response
- `201 Created`, `Location: /api/artists/{id}`, body `ArtistDto` (id, name, countryCode, email?,
  createdAtUtc). `ToCreatedHttpResult` is extended with an overload that returns the DTO instead of
  `{ id }`.
