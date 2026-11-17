# Walkthrough – backend

How the API is built, phase by phase. Each section describes what is in the code at the end of
that phase. For *how* the work was done (AI vs developer, mistakes) see [ai-log.md](ai-log.md).
Frontend counterpart: [takwene-track-frontend/docs/walkthrough.md](https://github.com/yussifmohd/takwene-track-frontend/blob/main/docs/walkthrough.md).

---

## Phase 0 – Foundation & health check (`001-project-foundation-and-health-check`)

### What was built
A .NET 10 Minimal API solution in four layers (Domain, Application, Infrastructure, Api) plus one
test project, wired for PostgreSQL, with one real feature: a health endpoint the web app and
Docker/load-balancer probes can call. Also Docker, docker compose, CI, and an exported OpenAPI contract.

### Important files
| Path | What it does |
|---|---|
| `src/TrackManagement.Domain/Common/BaseEntity.cs` | Base for entities whose `Id` is set in code (no DB-generated ids). |
| `src/TrackManagement.Application/Common/Messaging/` | `ICommand<T>`/`IQuery<T>` and their handler interfaces on top of MediatR 12.5.0; commands carry `ICommandMarker`. |
| `src/TrackManagement.Application/Common/Behaviors/` | MediatR pipeline: `LoggingBehavior` (name, status, ms; never payloads) → `ValidationBehavior` (FluentValidation → `Result.Invalid`) → `UnitOfWorkBehavior` (commands only, saves once on success). |
| `src/TrackManagement.Application/Common/Persistence/` | `IRepository<T>`, `IReadRepository<T>`, `IUnitOfWork` abstractions. |
| `src/TrackManagement.Application/Common/Models/PagedResult.cs` | Paging envelope (default 20, max 50); not used by an endpoint yet. |
| `src/TrackManagement.Infrastructure/Persistence/EfRepository.cs` | Ardalis `RepositoryBase` with `SaveChangesAsync` overridden as a no-op, so only the Unit of Work commits. |
| `src/TrackManagement.Infrastructure/Persistence/UnitOfWork.cs` | Calls `AppDbContext.SaveChangesAsync` once. |
| `src/TrackManagement.Infrastructure/Persistence/DatabaseInitializer.cs` | On startup, if `Database:MigrateOnStartup`, applies migrations then runs `ISeeder`s in `Order`; exceptions are not swallowed. |
| `src/TrackManagement.Infrastructure/DependencyInjection.cs` | Options validated on start, Npgsql + snake_case naming, repositories, UoW, DB health check capped at 3 s. |
| `src/TrackManagement.Api/Program.cs` | Serilog, DI, startup migration, middleware order, OpenAPI + Scalar in Development only, `/health`. |
| `src/TrackManagement.Api/Extensions/ResultExtensions.cs` | The single mapping from Ardalis `Result` to HTTP: plain DTO on success, ProblemDetails on every failure (400 as ValidationProblem). |
| `src/TrackManagement.Api/ExceptionHandlers/GlobalExceptionHandler.cs` | Unhandled exceptions → generic 500 ProblemDetails with `traceId`, no exception text. |
| `src/TrackManagement.Api/Endpoints/HealthEndpoints.cs` + `Health/` | `GET /api/health` (200 healthy, 503 otherwise, same JSON body as `/health`). |
| `tests/TrackManagement.UnitTests/Api/OpenApiContractTests.cs` | Compares the served `/openapi/v1.json` with `specs/<newest>/contracts/openapi.json`; `scripts/export-contract.ps1` refreshes it. |
| `Dockerfile`, `docker-compose.yml`, `.github/workflows/backend-tests.yml` | Non-root image, Postgres 17 + API stack on network `trackmgmt-net`, CI build + tests + coverage. |

### Request flow: `GET /api/health`
1. `HealthEndpoints` maps the route; the handler receives `HealthCheckService` and `TimeProvider`.
2. `CheckHealthAsync` runs the `database` check (`AddDbContextCheck<AppDbContext>`, 3 s timeout).
3. `HealthReportDto.From` builds `{ status, checkedAt, checks[] }` with no exception text.
4. Healthy → `200`; degraded/unhealthy → `503` with the same body (the web app treats it as data).

Health is the one deliberate exception to "endpoints call MediatR" (it is a framework concern;
recorded in the plan's Complexity Tracking). A feature endpoint flows: endpoint → `ISender.Send`
→ Logging → Validation → (UnitOfWork for commands) → handler → repository → `Result` →
`ToHttpResult()`; Phase 1 shows the first one.

### Key decisions and why
- **Real Unit of Work** instead of the reference project's save-per-repository-call: a command
  with several writes commits once or not at all. `UnitOfWorkBehaviorTests` prove queries never save.
- **ProblemDetails everywhere, plain DTOs on success**: one error shape for the frontend to parse.
- **Migrate on startup behind a flag** (on in Development/compose) that fails loudly.
- **Contract exported from the running app**, because the build-time OpenAPI tool produced an empty document.
- **Ardalis.Specification 9.3.1 on EF Core 10** checked first with a compatibility test.

### How to run and test
`cp .env.example .env` (set the secrets), `docker compose up --build`, then
`http://localhost:8080/health` and `/scalar`. Tests: `dotnet test --solution TrackManagement.slnx`.

### Known limitations
- `IAuditableEntity` exists but nothing fills its timestamps yet; `PagedResult` is unused until Phase 2.
- `InitialCreate` is an empty migration (no tables until Phase 1).
- API docs only in Development.

---

## Phase 1 – JWT authentication & authorization (`002-jwt-authentication-and-authorization`)

### What was built
Sign-in for the single admin, 60-minute JWTs, an Admin-only policy for every future write,
account lockout, a per-IP rate limit on sign-in, trusted reverse-proxy handling, and the admin
account provisioned from configuration. Reads stay public; there is no registration.

### Important files
| Path | What it does |
|---|---|
| `src/TrackManagement.Application/Common/Security/SecurityContracts.cs` | `IIdentityService`, `IJwtTokenService`, `ICurrentUser`, `AuthenticatedUser`, `AccessToken`. |
| `src/TrackManagement.Application/Features/Auth/Commands/Login/` | `LoginCommand` (a command: a failed attempt changes lockout state), validator, handler that turns any failure into the one generic 401. |
| `src/TrackManagement.Application/Features/Auth/Queries/GetCurrentUser/` | `GET /api/auth/me` data from `ICurrentUser`. |
| `src/TrackManagement.Infrastructure/Identity/IdentitySetup.cs` | Identity core with Guid keys: password ≥ 12 with complexity, lockout 5 failures / 15 min, schema pinned to `Version2` (no passkeys). |
| `src/TrackManagement.Infrastructure/Identity/IdentityService.cs` | Credential check: lockout → password → reset or count failure; unknown emails verify a dummy hash (same timing); logs masked email `a***@domain` only. |
| `src/TrackManagement.Infrastructure/Identity/AdminSeeder.cs` + `AdminOptions.cs` | Creates the Admin role and the admin from `Admin:Email`/`Admin:Password` if missing; never modifies an existing account; fails startup naming a missing or weak setting. |
| `src/TrackManagement.Infrastructure/Auth/JwtTokenService.cs` | Issues HS256 tokens (`sub`, `email`, `role`, `jti`, iss/aud, exp); `JwtValidation` holds the validation rules. |
| `src/TrackManagement.Infrastructure/Auth/JwtBearerOptionsSetup.cs` | Applies those rules to the bearer handler (no inbound claim mapping). |
| `src/TrackManagement.Infrastructure/Persistence/Configurations/IdentityConfigurations.cs` | Identity tables named `users`, `roles`, `user_roles`, … (migration `AddIdentity`). |
| `src/TrackManagement.Api/Security/AuthPolicies.cs` | Policy `AdminOnly` and the `.RequireAdmin()` extension for write endpoints. |
| `src/TrackManagement.Api/Security/LoginRateLimiting.cs` | Fixed window per client IP (`RateLimiting:Login`, default 10/min) → 429 ProblemDetails + `Retry-After`. |
| `src/TrackManagement.Api/Security/ForwardedHeadersSetup.cs` | Trusts `X-Forwarded-For` only from `ReverseProxy:KnownNetworks` (CIDR list from config), last hop only. |
| `src/TrackManagement.Api/Security/HttpCurrentUser.cs` | `ICurrentUser` from the validated token's claims. |
| `src/TrackManagement.Api/Endpoints/AuthEndpoints.cs` | `POST /api/auth/login` (anonymous, rate-limited), `GET /api/auth/me` (authenticated). |
| `src/TrackManagement.Api/OpenApi/AuthorizeOperationTransformer.cs` | Adds the Bearer requirement and 401/403 responses to protected operations. |
| `tests/TrackManagement.UnitTests/Api/TestApiFactory.cs` | Test host: EF InMemory, seeded admin, test-only `/test/admin-only` + `/test/public` (excluded from OpenAPI). |

### Request flow: `POST /api/auth/login`
1. `UseForwardedHeaders` sets the client IP (only if the caller is a trusted proxy).
2. `UseRateLimiter` applies policy `login` for that IP; over the limit → 429, nothing else runs.
3. `AuthEndpoints` binds `LoginRequest` and sends `LoginCommand` via `ISender`.
4. `LoggingBehavior` → `ValidationBehavior` (`LoginCommandValidator`: email format/length,
   password required/length; failure → 400 ValidationProblem) → `UnitOfWorkBehavior`.
5. `LoginCommandHandler` trims the email and calls `IdentityService.AuthenticateAsync`, which
   uses `UserManager` (lockout, password check, failure counter).
6. Failure of any kind → `Result.Unauthorized("Invalid email or password.")` → 401 ProblemDetails.
7. Success → `JwtTokenService.Create` → `LoginResponse { accessToken, tokenType, expiresAt, user }`
   → 200. `UnitOfWorkBehavior` saves (UserManager already persisted the counter reset).

Protected request: bearer handler validates the token (`JwtValidation`) → `UseAuthorization`
checks the endpoint's policy (`AdminOnly` needs role `Admin`) → 401/403 ProblemDetails via
`UseStatusCodePages`, or the endpoint runs.

### Key decisions and why
- **UserManager directly, not SignInManager**: no cookie scheme needed for a token API.
- **Generic failure + dummy hash**: no account enumeration by message or by timing.
- **Token rules**: issuer, audience, lifetime, signature; HS256 only; 30 s clock skew (developer requirement).
- **Proxy ranges in configuration** so the AWS load balancer's VPC range can be set in Phase 6.
- **No refresh tokens** (interview): sign in again after 60 minutes.
- Admin password only in `.env`/user-secrets; `.env.example` has an empty placeholder.

### How to run and test
Set `ADMIN__EMAIL`/`ADMIN__PASSWORD` (and `JWT__SIGNINGKEY`) in `.env`, `docker compose up --build`,
then `curl -X POST localhost:8080/api/auth/login -H "Content-Type: application/json" -d '{"email":"…","password":"…"}'`
and `curl localhost:8080/api/auth/me -H "Authorization: Bearer <token>"`. In Scalar: call Login, paste
the token. Tests: `dotnet test --solution TrackManagement.slnx` (127 tests at the end of Phase 1).

### Known limitations
- One admin, no password change endpoint (delete the user row and restart to re-provision).
- Identity lockout uses the system clock (tests move `LockoutEnd` instead of faking time).
- Rate-limit counters are in memory per instance (two EC2 instances in Phase 6 → per-instance limits).
- First start on an empty database logs one benign EF `ERR` for the migrations-history query;
  ASP.NET Data Protection warns about container key storage (not used by this API).

## Phase 2 – Artists & DSPs (`003-artists-and-dsps-management`)

### What was built
Artists (name in any script, ISO country code, required unique email) that only an admin can add
and anyone can list page by page; the three distribution platforms created by the migration with
fixed ids; a UTF-8 startup check; timestamps filled automatically; 400 for malformed requests in
every environment; a 32 KB body cap; integration tests on real PostgreSQL.

### Important files
| Path | What it does |
|---|---|
| `src/TrackManagement.Domain/Countries/CountryCodes.cs` | The 249 assigned ISO 3166-1 alpha-2 codes, `Normalize`, `IsAssigned`, and the list hash the web app checks too. |
| `src/TrackManagement.Domain/Artists/Artist.cs` | Entity + `Create` (trims, upper-cases the country, normalised email for the unique index). |
| `src/TrackManagement.Domain/Dsps/Dsp.cs` | The fixed platform ids and `Dsp.Seed`. |
| `src/TrackManagement.Application/Common/Validation/` | `TextRules` (code-point length, email format shared with login), `PagingRules`, `ValidationErrorCodes.Conflict`. |
| `src/TrackManagement.Application/Features/Artists/` | `ArtistDto` (email omitted when null), specs (`ArtistsPageSpec` projects and orders by `lower(name)`), `CreateArtist` and `ListArtists`. |
| `src/TrackManagement.Application/Features/Dsps/` | `ListDspsQuery` + spec. |
| `src/TrackManagement.Infrastructure/Persistence/Configurations/{Artist,Dsp}Configuration.cs` | Columns, unique indexes, `HasData` for the platforms. |
| `src/TrackManagement.Infrastructure/Persistence/AuditableEntityInterceptor.cs` | Fills `CreatedAtUtc`/`UpdatedAtUtc` on save. |
| `src/TrackManagement.Infrastructure/Persistence/DatabaseEncodingCheck.cs` | Stops startup unless the database is UTF8. |
| `src/TrackManagement.Infrastructure/Persistence/Migrations/*_AddArtistsAndDsps.cs` | `artists`, `dsps`, indexes, the three platforms. |
| `src/TrackManagement.Api/Endpoints/{Artist,Dsp}Endpoints.cs` | `GET/POST /api/artists`, `GET /api/dsps`. |
| `src/TrackManagement.Api/Extensions/ResultExtensions.cs` | Conflict-only validation errors → 409; `ToCreatedAtHttpResult` (201 + Location + body). |
| `src/TrackManagement.Api/ExceptionHandlers/{BadRequest,UniqueConstraint}ExceptionHandler.cs` | Malformed input → 400/413; unique-index race → 409 `errors.email`. |
| `src/TrackManagement.Api/Security/RequestBodyLimit.cs` | `Api:MaxRequestBodyBytes` (32 KB) in Kestrel + a Content-Length guard. |
| `src/TrackManagement.Api/OpenApi/OmittedWhenNullSchemaTransformer.cs` | Keeps omitted-when-null fields (artist email) optional in the contract. |
| `tests/TrackManagement.UnitTests/Integration/` | `PostgresFixture` (Testcontainers, same image as compose) and `ArtistPersistenceTests` (`Category=Integration`). |

### Request flow: `POST /api/artists`
1. Request logging (outermost, logs the final status) → exception handler → body limit → CORS →
   authentication → authorization: no token 401, no Admin role 403.
2. `ArtistEndpoints` binds `CreateArtistRequest` (malformed JSON → `BadRequestExceptionHandler` → 400)
   and sends `CreateArtistCommand`.
3. `ValidationBehavior` runs `CreateArtistCommandValidator`: name, country, email, then the duplicate
   check (`ArtistByNormalizedEmailSpec`, error code `conflict`). Field errors → 400; only a conflict → 409.
4. `CreateArtistCommandHandler` builds the artist with the current time and adds it (no save).
5. `UnitOfWorkBehavior` saves; the interceptor fills timestamps; a racing duplicate hits the unique
   index → `UniqueConstraintExceptionHandler` → 409.
6. 201 with `Location: /api/artists/{id}` and the artist.

`GET /api/artists`: paging validated (page ≥ 1, size 1–50) → count + one projected page ordered by
`lower(name)`, creation time, id; `email` is selected only when `ICurrentUser` is an admin.

### Key decisions and why
- **Email hidden from the public list**: artists are real people and reads are public.
- **Duplicate email is 409 that still names the field**, so the form shows it next to the input;
  the unique index is the real guarantee, the validator check gives the friendly message.
- **Lengths count code points** (as PostgreSQL `varchar(n)` does), so Arabic and emoji get the full 200.
- **Static country list** instead of `RegionInfo`: same on every machine, reviewable, shared with the
  web app by hash.
- **DSPs in the migration** (`HasData`): same ids everywhere, no duplicates on restart.
- **Integration tests in the same project** behind a trait: plain unit tests need no Docker; CI runs all.

### How to run and test
`docker compose up --build`, sign in (Phase 1), then
`curl -X POST localhost:8080/api/artists -H "Authorization: Bearer <token>" -H "Content-Type: application/json" -d '{"name":"عمرو دياب","countryCode":"EG","email":"booking@example.com"}'`,
`curl "localhost:8080/api/artists?page=1&pageSize=20"`, `curl localhost:8080/api/dsps`.
Tests: `dotnet test --solution TrackManagement.slnx` (239, needs Docker) or add
`-- --filter-not-trait "Category=Integration"` to skip the 13 PostgreSQL tests.

### Known limitations
- No edit/delete/detail for artists and no search (not in this phase).
- A duplicate-email race that reaches the database logs an EF error line before the 409.
- Rate limiting still applies to sign-in only.

---

## Phase 3 – Tracks (`004-track-catalog-with-filters-and-status`)

### What was built
Tracks (title in any script, one artist, ISO 3901 ISRC, fixed genre, release date, status) that an
admin adds and moves through draft → submitted → distributed, and anyone lists (filters by artist,
genre, status) or opens. Status rules live in the `Track` entity; two admins changing one track at
once cannot both win. The artist picker in the web app gets a name search and GET by id.

### Important files
| Path | What it does |
|---|---|
| `src/TrackManagement.Domain/Tracks/Track.cs` | Entity: `Create` (draft, trimmed title, normalised ISRC, guards), `ChangeStatus` (asks the rules, returns the outcome), `Version` row version. |
| `src/TrackManagement.Domain/Tracks/TrackStatusRules.cs` | The transition table, `AllowedNext`, `Check` with the refusal messages. The only copy of the rules. |
| `src/TrackManagement.Domain/Tracks/{Isrc,Genres,TrackStatus}.cs` | ISRC normalise/format, the 8 genres, status names on the wire and in the database. |
| `src/TrackManagement.Application/Features/Tracks/TrackDtos.cs` | List item, detail (allowed next statuses, empty distributions), distribution placeholder. |
| `src/TrackManagement.Application/Features/Tracks/Specifications/TrackSpecs.cs` | `TrackFilter` + one shared WHERE for count and page; ordering; projections with the artist name. |
| `src/TrackManagement.Application/Features/Tracks/Commands/{CreateTrack,ChangeTrackStatus}/` | Validators (per-field messages, ISRC conflict code) and handlers (domain decides, handler maps to 409/404). |
| `src/TrackManagement.Application/Features/Tracks/Queries/{ListTracks,GetTrack}/` | Public reads. |
| `src/TrackManagement.Application/Features/Artists/Queries/GetArtist/`, `ArtistsPageSpec.cs` | Artist by id; name search (`strpos`, no wildcards). |
| `src/TrackManagement.Infrastructure/Persistence/Configurations/TrackConfiguration.cs` | `char(12)` ISRC unique, check constraints, FK restrict, indexes, `xmin` concurrency token. |
| `src/TrackManagement.Infrastructure/Persistence/Migrations/*_AddTracks.cs` | `tracks` table (no `xmin` column: it is a system column). |
| `src/TrackManagement.Api/Endpoints/TrackEndpoints.cs` | The four track routes; strings for genre/status/date so bad values are a 400 per field. |
| `src/TrackManagement.Api/ExceptionHandlers/ConcurrencyExceptionHandler.cs` | Lost race → 409 "reload". |
| `src/TrackManagement.Api/OpenApi/FixedValuesSchemaTransformer.cs` | Genre/status value lists in the contract (only on the track request/query members). |
| `tests/.../Integration/{TrackPersistenceTests,TrackConcurrencyTests}.cs` | Real PostgreSQL: Unicode, checks, FK, ISRC race, SQL filters, `xmin`. |

### Request flow: `PATCH /api/tracks/{id}/status`
1. Pipeline as before (logging, exception handler, body limit, auth) → `.RequireAdmin()`.
2. `TrackEndpoints` builds `ChangeTrackStatusCommand(id, status)`.
3. Validation behavior: unknown/missing status → 400 `errors.status`.
4. Handler loads the track (tracked, with artist); none → 404. `track.ChangeStatus(target, now)`:
   the rules refuse → `Result.Conflict(message)` → 409 ProblemDetails; allowed → status and
   `UpdatedAtUtc` change and the handler returns the detail DTO.
5. Unit of Work behavior saves once: `UPDATE … WHERE id = @id AND xmin = @loaded`. Someone saved first
   → `DbUpdateConcurrencyException` → `ConcurrencyExceptionHandler` → 409.

### Key decisions and why
- **Rules in the domain, outcome not exception**: the requirement; refusals are normal answers.
- **The API lists the allowed next statuses**: the web app shows only valid controls and keeps no copy.
- **`xmin` row version**: free in PostgreSQL, no extra column; a domain check alone races.
- **Strings at the edge** for genre, status and date: unknown values must be a 400 naming the field.
- **Interceptor keeps a domain-set `UpdatedAtUtc`**: the PATCH response is built before the save and
  must match what is stored.
- **PATCH to distributed creates no distribution rows**; Phase 4 adds rows (spec "Status and distributions").

### How to run and test
`docker compose up --build -d`, sign in, then the curl examples in the README.
`dotnet test --solution TrackManagement.slnx` (397 tests, 24 on PostgreSQL via Docker).
Quickstart results are in `docs/ai-log.md` (Phase 3).

### Known limitations
- Tracks cannot be edited or deleted yet (out of scope, clarified).
- Distributions are always empty in this phase (deliveries arrive with Phase 4).
- Artist search is a substring match on the name (no accent/diacritic folding).

---

## Phase 4 – Distribution (`005-track-distribution-to-dsps`)

### What was built
Deliveries of a track to platforms. An admin distributes a submitted (or already distributed) track
to one or more platforms; each platform gets one delivery (pending, live or rejected) with the time
it was sent and the time the platform answered. Because no real platform is connected, the admin
records the answer (live or rejected) for pending deliveries. A rejected delivery can be sent again.
The status change endpoint no longer moves a track to distributed: only Distribute does, so a
distributed track always has at least one delivery.

### Important files
| Path | What it does |
|---|---|
| `src/TrackManagement.Domain/Tracks/TrackDistribution.cs` | Child entity of `Track` (key track + platform): `Send`, `SendAgain`, `Answer`; `DistributeResult`. |
| `src/TrackManagement.Domain/Tracks/DistributionStatus.cs` | Delivery statuses, wire names, `DistributionStatusRules` (pending → live/rejected only, refusal messages). |
| `src/TrackManagement.Domain/Tracks/Track.cs` | `Distribute` (new → pending, rejected → re-sent, pending/live untouched; submitted → distributed), `RecordAnswer`, `AllowedNextStatuses`, `CanDistribute`. Every change stamps `UpdatedAtUtc`. |
| `src/TrackManagement.Domain/Tracks/TrackStatusRules.cs` | `Check(from, to, hasDistributions)`: submitted → distributed refused without a delivery. |
| `src/TrackManagement.Application/Features/Tracks/Commands/DistributeTrack/` | Validator (not empty, ≤ 20 distinct ids) and handler (unknown platform → 400 `dspIds`, draft → 409, no-op → 200). |
| `src/TrackManagement.Application/Features/Tracks/Commands/RecordDistributionAnswer/` | Validator (`live`/`rejected` only) and handler (404 when never sent there, refusals → 409). |
| `src/TrackManagement.Application/Features/Tracks/TrackDtos.cs` | `TrackDistributionDto` (name, status, times, `allowedAnswers`), `TrackDetailDto.canDistribute`; one `Create` shared by handlers and the SQL projection. |
| `src/TrackManagement.Application/Features/Dsps/DspNames.cs` | Platform names by id (one small query) for command responses. |
| `src/TrackManagement.Infrastructure/Persistence/Configurations/TrackDistributionConfiguration.cs` | `pk_track_distributions`, status check, "answered exactly when not pending" check, FK to `dsps`. |
| `src/TrackManagement.Infrastructure/Persistence/Migrations/*_AddTrackDistributions.cs` | The `track_distributions` table (schema only; no data changes). |
| `src/TrackManagement.Api/Endpoints/TrackEndpoints.cs` | `POST /distribute`, `PATCH /distributions/{dspId}`. |
| `src/TrackManagement.Api/ExceptionHandlers/UniqueConstraintExceptionHandler.cs` | Two racing first deliveries to one platform → 409 `errors.dspIds`. |
| `tests/.../Integration/DistributionPersistenceTests.cs` | Real PostgreSQL: checks, FK, three races between admins, detail read time with 1,000 tracks. |

### Request flow: `POST /api/tracks/{id}/distribute`
1. Pipeline as before → `.RequireAdmin()`; body `{ dspIds }` (32 KB cap).
2. Validation behavior: missing/empty list or more than 20 distinct ids → 400 `errors.dspIds`.
3. Handler loads the track with its artist and deliveries (tracked) → 404 if missing; loads all
   platforms once; unknown ids → `Result.Invalid` on `DspIds` → 400.
4. `track.Distribute(ids, now)`: draft → `NotAllowed` → 409; otherwise new platforms get pending
   deliveries, rejected ones are re-sent, the track becomes distributed and `UpdatedAtUtc` is set.
   Nothing to send → unchanged, still 200.
5. Unit of Work saves once: `INSERT`/`UPDATE track_distributions` plus `UPDATE tracks … WHERE xmin = @loaded`.
   A concurrent change to the same track → 409 (row version); two first deliveries to one platform → 409 (primary key).

### Key decisions and why
- **Deliveries belong to the Track aggregate**: the "distributed needs a delivery" rule spans both,
  and stamping the track on every delivery change lets `xmin` serialise all races on one track.
- **PATCH to distributed is a 409** (user decision): a distributed track must say where it went.
- **No data migration** (user decision): no real data existed; local databases from before are reset
  with `docker compose down -v` (README).
- **Platform count is data**: requests accept up to 20 distinct ids (a request limit, not a rule);
  ids are checked against the stored platforms.
- **The API lists what is allowed** (`allowedNextStatuses`, `canDistribute`, `allowedAnswers`), so the
  web app keeps no copy of the rules.
- **Distribute always answers 200 with the track**, also when nothing changed: the result is the
  state the admin asked for.

### How to run and test
`docker compose down -v && docker compose up --build -d`, sign in, then the curl examples in the README.
`dotnet test --solution TrackManagement.slnx` (471 tests, 31 on PostgreSQL via Docker).
Quickstart results are in `docs/ai-log.md` (Phase 4).

### Known limitations
- Platform answers are simulated by the admin; no platform is called.
- Deliveries cannot be withdrawn and a live delivery is final.
- Seed data with varied deliveries is not there yet (local data is created through the API or web app).
