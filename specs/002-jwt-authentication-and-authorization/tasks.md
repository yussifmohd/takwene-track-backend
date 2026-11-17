---
description: "Task list for Phase 1 – Admin Sign-in & Access Control (API service)"
---

# Tasks: Admin Sign-in & Access Control (API service)

**Input**: Design documents from `specs/002-jwt-authentication-and-authorization/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/auth-api.md, quickstart.md
**Tests**: REQUIRED by the constitution (Principle III).
**Skills**: dotnet-clean-architecture (all code), git-workflow, decisions-log

## Format: `[ID] [P?] [Story] Description` — paths relative to the backend repo root.

---

## Phase 1: Setup & housekeeping

- [ ] T001 [P] Upgrade `.github/workflows/backend-tests.yml` to `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7` (research R8)
- [ ] T002 [P] Add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14 (MIT, checked) to `Directory.Packages.props` and `src/TrackManagement.Infrastructure`; refresh lock files
- [ ] T003 Test `tests/TrackManagement.UnitTests/Infrastructure/Persistence/NpgsqlConnectionStringDefaultsTests.cs` (adds `Gss Encryption Mode=Disable` when absent; keeps an explicit value; preserves other keywords), then `src/TrackManagement.Infrastructure/Persistence/NpgsqlConnectionStringDefaults.cs`, used by `AddInfrastructure` and `DesignTimeDbContextFactory` (research R7)

---

## Phase 2: Foundational (blocks all stories)

- [ ] T004 Application security abstractions in `src/TrackManagement.Application/Common/Security/`: `IIdentityService` (`AuthenticateAsync(email, password, ct)` → `Result<AuthenticatedUser>`), `IJwtTokenService` (`Create(AuthenticatedUser)` → `AccessToken(Token, ExpiresAt)`), `ICurrentUser` (`IsAuthenticated`, `UserId`, `Email`, `Roles`, `ExpiresAt`), records `AuthenticatedUser(Id, Email, Roles)`, `AccessToken`
- [ ] T005 Identity persistence: `src/TrackManagement.Infrastructure/Identity/ApplicationUser.cs` (`IdentityUser<Guid>`), `AppDbContext` → `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>` with snake_case table names in `Persistence/Configurations/IdentityTableNames.cs` (users, roles, user_roles, user_claims, user_logins, user_tokens, role_claims)
- [ ] T006 Identity registration in `src/TrackManagement.Infrastructure/DependencyInjection.cs`: `AddIdentityCore` + roles + EF stores + SignInManager; password (≥ 12, upper, lower, digit, symbol), lockout (5 / 15 min, new users), unique email
- [ ] T007 Migration `AddIdentity` via `dotnet ef migrations add AddIdentity -p src/TrackManagement.Infrastructure -s src/TrackManagement.Api -o Persistence/Migrations`; review generated table/column names
- [ ] T008 [P] Test `tests/TrackManagement.UnitTests/Infrastructure/Auth/JwtTokenServiceTests.cs`: claims sub/email/role/jti, iss/aud, exp = now + 60 min (test `TimeProvider`), HS256 header, validates with the configured parameters
- [ ] T009 `src/TrackManagement.Infrastructure/Auth/JwtTokenService.cs` (JsonWebTokenHandler, HS256) and `Auth/JwtBearerOptionsSetup.cs` (`IConfigureNamedOptions<JwtBearerOptions>`: validate issuer, audience, lifetime, key; `ValidAlgorithms=[HS256]`; `ClockSkew=30s`; `MapInboundClaims=false`; role/name claim types); register `AddAuthentication(JwtBearer)` + `AddAuthorization` with `AuthPolicies.Admin`
- [ ] T010 `src/TrackManagement.Api/Security/AuthPolicies.cs`, `EndpointConventionExtensions.RequireAdmin()`, `HttpCurrentUser : ICurrentUser`; Program.cs order `UseForwardedHeaders → UseExceptionHandler → UseStatusCodePages → UseSerilogRequestLogging → UseCors → UseRateLimiter → UseAuthentication → UseAuthorization`
- [ ] T011 Extend `tests/TrackManagement.UnitTests/Api/TestApiFactory.cs`: test JWT settings, `IssueToken(roles, lifetime, key, issuer, audience)` helper, test-only endpoints `/test/admin-only` (RequireAdmin) and `/test/public`, rate-limit settings overridable, seeded admin via `Admin` options

**Checkpoint**: solution builds; existing 48 tests still pass.

---

## Phase 3: User Story 1 – Admin signs in (P1) 🎯 MVP

### Tests
- [ ] T012 [P] [US1] `tests/TrackManagement.UnitTests/Application/Auth/LoginCommandValidatorTests.cs` (email required/format/≤256, password required/≤128; camelCase field names)
- [ ] T013 [P] [US1] `tests/TrackManagement.UnitTests/Application/Auth/LoginCommandHandlerTests.cs` (success → token + user; any identity failure → `Unauthorized` with the generic message; email trimmed)
- [ ] T014 [P] [US1] `tests/TrackManagement.UnitTests/Api/Auth/LoginEndpointTests.cs`: 200 contract shape (`accessToken`, `tokenType`, `expiresAt` ≈ +60 min, `user.email`, `user.roles=["Admin"]`); email case/space-insensitive; `/api/auth/me` with that token → 200 email/roles/expiresAt

### Implementation
- [ ] T015 [US1] `src/TrackManagement.Application/Features/Auth/` – `AuthErrors`, `Commands/Login/{LoginCommand, LoginCommandValidator, LoginCommandHandler, LoginResponse}`, `Queries/GetCurrentUser/{GetCurrentUserQuery, GetCurrentUserQueryHandler, CurrentUserDto}`
- [ ] T016 [US1] `src/TrackManagement.Infrastructure/Identity/IdentityService.cs` (normalised lookup, `CheckPasswordSignInAsync(lockoutOnFailure: true)`, dummy-hash path for unknown email, LoggerMessage events with masked email)
- [ ] T017 [US1] `src/TrackManagement.Api/Endpoints/AuthEndpoints.cs` (`POST /api/auth/login` AllowAnonymous + `RequireRateLimiting("login")`, `GET /api/auth/me` RequireAuthorization; Produces 200/400/401/429) and register in `MapApiEndpoints`

---

## Phase 4: User Story 2 – Generic failures, lockout, throttling (P1)

### Tests
- [ ] T018 [P] [US2] In `LoginEndpointTests`: unknown email vs wrong password → both 401, identical `title`/`detail`/`type`; 400 field errors for bad input; 5 wrong then correct → 401; after lockout end (advance test `TimeProvider` used by Identity) → 200
- [ ] T019 [P] [US2] `tests/TrackManagement.UnitTests/Api/Auth/LoginRateLimitTests.cs`: limit+1th request → 429 ProblemDetails + `Retry-After`; other clients (different forwarded IP from a trusted proxy) unaffected; spoofed `X-Forwarded-For` from an untrusted source ignored
- [ ] T020 [P] [US2] Test that logs never contain the password or token (capture Serilog/ILogger output during a login)

### Implementation
- [ ] T021 [US2] `src/TrackManagement.Api/Security/LoginRateLimiting.cs` (`AddLoginRateLimiting(configuration)`: fixed window from `RateLimiting:Login`, partition by IP, 429 ProblemDetails + Retry-After) and `ForwardedHeadersSetup.cs` (`ReverseProxy:KnownNetworks`, ForwardLimit 1); appsettings defaults
- [ ] T022 [US2] Register Identity `TimeProvider` usage for lockout (Identity uses `TimeProvider` from DI in .NET 8+; confirm and document) 

---

## Phase 5: User Story 3 – Admin-only writes, public reads (P1)

### Tests
- [ ] T023 [P] [US3] `tests/TrackManagement.UnitTests/Api/Auth/TokenValidationTests.cs`: `/api/auth/me` 401 ProblemDetails + `WWW-Authenticate: Bearer` for missing, expired (beyond 30 s skew), not-yet-valid, wrong key, wrong issuer, wrong audience, tampered payload, `alg: none`
- [ ] T024 [P] [US3] `tests/TrackManagement.UnitTests/Api/Auth/AuthorizationPolicyTests.cs`: `/test/admin-only` → 401 no token, 403 ProblemDetails for a non-admin token, 200 for admin; `/test/public` and `/api/health` → 200 without token; no route matches `register`/`signup`
- [ ] T025 [P] [US3] Extend `OpenApiExposureTests`/contract: `/api/auth/me` has the Bearer security requirement; `/api/auth/login` does not

### Implementation
- [ ] T026 [US3] `src/TrackManagement.Api/OpenApi/AuthorizeOperationTransformer.cs` (security requirement + 401/403 responses for protected endpoints) registered in `AddOpenApi`
- [ ] T027 [US3] Export contract: `./scripts/export-contract.ps1 -Feature 002-jwt-authentication-and-authorization`; review `openapi.json` against `contracts/auth-api.md`

---

## Phase 6: User Story 4 – Admin provisioned from configuration (P1)

### Tests
- [ ] T028 [P] [US4] `tests/TrackManagement.UnitTests/Infrastructure/Identity/AdminSeederTests.cs` (EF InMemory + real UserManager): creates role + admin once; second run no duplicate and password unchanged; missing email/password → exception naming the setting without the value; weak password → exception listing failed rules

### Implementation
- [ ] T029 [US4] `src/TrackManagement.Infrastructure/Identity/AdminOptions.cs` + `AdminSeeder.cs` (`ISeeder`), registered in DI; appsettings `Admin` section with empty values
- [ ] T030 [US4] `docker-compose.yml` (`Admin__Email`, `Admin__Password: ${ADMIN__PASSWORD:?…}`, `ReverseProxy__KnownNetworks__0: 172.16.0.0/12`), `.env.example` (`ADMIN__EMAIL=admin@takwene.local`, `ADMIN__PASSWORD=` placeholder with rules comment), README configuration table

---

## Phase 7: Polish & cross-cutting

- [ ] T031 Run quickstart.md end to end on `docker compose up --build`; confirm `docker compose logs api | grep -i gssapi` is empty and the `IEntityTypeConfiguration` warning is gone
- [ ] T032 [P] README: auth section (sign in via Scalar/curl, admin provisioning, lockout/rate limit, sign-out trade-off)
- [ ] T033 [P] `docs/ai-log.md` Phase 1 entry (generated vs written, security table incl. reference issues fixed: lockout, enumeration; mistakes); add the user's Phase 0 developer notes when provided
- [ ] T034 [P] Repo `Memory.md` (versions, state) and master `Memory.md`
- [ ] T035 Full `dotnet test`, `docker compose up --build`, secret scan (`git grep` for password/key patterns, `.env` untracked); mark tasks [X]
- [ ] T036 Push branch, open PR with `gh pr create` (summary, evidence, checklist); CI green without Node 20 annotation

---

## Dependencies & Execution Order

- Setup (T001–T003) → Foundational (T004–T011) → US1 → US2/US3/US4 (US2–US4 independent after US1's endpoint exists) → Polish.
- T027 (contract export) must finish before the frontend regenerates its types.
- Within each story: tests first (fail), then implementation.

## Parallel Example

```text
T008 token tests | T012 validator tests | T013 handler tests      (after T004)
T023 token validation | T024 policy | T028 seeder                  (after T017)
```

## Implementation Strategy

MVP = Setup + Foundational + US1 (sign in + /me). Then US2 (abuse protection), US3 (policy proof
and OpenAPI), US4 (provisioning + compose). Small Conventional Commits per task group.
