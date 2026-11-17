# Implementation Plan: Admin Sign-in & Access Control (API service)

**Branch**: `002-jwt-authentication-and-authorization` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-jwt-authentication-and-authorization/spec.md`

**Skills applied**: dotnet-clean-architecture, git-workflow, decisions-log

## Summary

Add ASP.NET Core Identity (one seeded Admin, no registration) and JWT bearer authentication:
`POST /api/auth/login` (generic 401 for every failure, constant-time-ish unknown-email path,
Identity lockout 5 / 15 min, fixed-window rate limit 10 / min per client IP behind trusted
forwarded headers) and `GET /api/auth/me`. Tokens are HS256, 60 min, validated for issuer,
audience, lifetime, key and algorithm. An `Admin` authorization policy is defined once and exposed
as `RequireAdmin()` for every write endpoint from Phase 2. 401/403/429 are ProblemDetails. The
OpenAPI document marks protected operations with the Bearer requirement.
Housekeeping requested with this phase: GitHub Actions upgraded to Node 24 majors, and Npgsql's GSS
probe disabled so the container log no longer shows `libgssapi_krb5.so.2` errors.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.403, runtime 10.0.14)

**Primary Dependencies**: as Phase 0 + `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14 (MIT).
JWT handling uses `Microsoft.IdentityModel.JsonWebTokens` 8.19.2, already transitive via JwtBearer.
Rate limiting and forwarded headers are built into ASP.NET Core (no package).

**Storage**: PostgreSQL 17; Identity tables (snake_case) added by migration `AddIdentity`

**Testing**: xUnit v3 + NSubstitute + Shouldly; in-memory API tests (EF InMemory + real Identity)

**Target Platform**: Linux containers (compose); AWS later

**Project Type**: web-service (API)

**Performance Goals**: login < 1 s locally (SC-001); password hashing at Identity defaults (PBKDF2, 100k iterations)

**Constraints**: no secrets committed; generic failures; rate limit before credential check; tokens never logged

**Scale/Scope**: one admin; 2 endpoints; 1 policy

## Constitution Check

| Principle | Gate | Status |
|---|---|---|
| I. Clean Architecture & CQRS | `LoginCommand` (ICommand, state change: failed-attempt counter) and `GetCurrentUserQuery` (IQuery) in `Features/Auth`; Identity + JWT behind Application interfaces (`IIdentityService`, `IJwtTokenService`, `ICurrentUser`) | ✅ |
| II. Thin HTTP edge | `AuthEndpoints` group maps to ISender; Results → `ToHttpResult`; 401/403/429 ProblemDetails | ✅ |
| III. Tests ship with features | handler, validator, token service, seeder, policy, rate limit, lockout, token-validation API tests | ✅ |
| IV. Security by default | Admin policy, full JWT validation incl. algorithm, generic failures + lockout, secrets env-only, no enumeration (also timing) | ✅ |
| V. Spec-driven | spec → clarify → plan → tasks → analyze; contract exported to `contracts/openapi.json` at implementation | ✅ |
| VI. Simplicity & licenses | one new MIT package; no refresh tokens/revocation (out of scope) | ✅ |

Post-design re-check: ✅ (Login as ICommand saves through Identity's own store; see Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/002-jwt-authentication-and-authorization/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/auth-api.md        # human-readable contract; openapi.json exported during implementation
├── checklists/requirements.md
└── tasks.md
```

### Source Code (additions/changes)

```text
src/TrackManagement.Domain/Constants/Roles.cs                      (exists: Admin)
src/TrackManagement.Application/
├── Common/Security/ (IIdentityService, IJwtTokenService, ICurrentUser, AuthenticatedUser, AccessToken)
└── Features/Auth/
    ├── AuthErrors.cs                                   (generic message constant)
    ├── Commands/Login/ (LoginCommand, LoginCommandHandler, LoginCommandValidator, LoginResponse)
    └── Queries/GetCurrentUser/ (GetCurrentUserQuery, GetCurrentUserQueryHandler, CurrentUserDto)
src/TrackManagement.Infrastructure/
├── Identity/ (ApplicationUser, IdentityService, AdminOptions, AdminSeeder)
├── Auth/ (JwtOptions (exists), JwtTokenService, JwtBearerOptionsSetup)
├── Persistence/AppDbContext.cs  → IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
├── Persistence/Configurations/IdentityTableNames.cs (snake_case table names)
├── Persistence/Migrations/<ts>_AddIdentity.cs
├── Persistence/NpgsqlConnectionStringDefaults.cs   (GSS encryption disabled unless set)
└── DependencyInjection.cs (Identity core, lockout/password options, JWT bearer, seeder)
src/TrackManagement.Api/
├── Endpoints/AuthEndpoints.cs (POST /api/auth/login, GET /api/auth/me)
├── Security/ (AuthPolicies, EndpointConventionExtensions.RequireAdmin, HttpCurrentUser,
│              LoginRateLimiting, ForwardedHeadersSetup)
├── OpenApi/BearerSecuritySchemeTransformer.cs (+ operation transformer for protected endpoints)
└── Program.cs (UseForwardedHeaders → … → UseRateLimiter → UseAuthentication → UseAuthorization)
tests/TrackManagement.UnitTests/
├── Application/Auth/ (LoginCommandHandlerTests, LoginCommandValidatorTests, GetCurrentUserQueryHandlerTests)
├── Infrastructure/Auth/JwtTokenServiceTests.cs, Infrastructure/Identity/AdminSeederTests.cs,
│   Infrastructure/Persistence/NpgsqlConnectionStringDefaultsTests.cs
└── Api/Auth/ (LoginEndpointTests, TokenValidationTests, AuthorizationPolicyTests, LoginRateLimitTests)
.github/workflows/backend-tests.yml (checkout@v7, setup-dotnet@v6, upload-artifact@v7)
docker-compose.yml / .env.example (ADMIN__EMAIL, ADMIN__PASSWORD placeholder, ReverseProxy__KnownNetworks__0)
```

**Structure Decision**: unchanged layout; new `Features/Auth` slice and `Security` folders.

## Key design decisions (details in research.md)

1. **Identity core only** (`AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>()
   .AddEntityFrameworkStores<AppDbContext>().AddSignInManager()`), no cookies.
   Password: length ≥ 12, upper, lower, digit, symbol. Lockout: 5 failures → 15 min, enabled for new users.
   Email unique; lookups normalised (trim + Identity upper-case normalisation).
2. **Login** (`LoginCommand`): validator → `IIdentityService.AuthenticateAsync(email, password)` →
   `SignInManager.CheckPasswordSignInAsync(user, pwd, lockoutOnFailure: true)`. Unknown email runs
   the password hasher against a dummy hash so its timing matches a wrong password. Every failure
   → `Result.Unauthorized(AuthErrors.InvalidCredentials)` ("Invalid email or password.").
   Success → `IJwtTokenService.Create(user)` → `LoginResponse(accessToken, "Bearer", expiresAt, user{email, roles})`.
3. **Tokens**: `JsonWebTokenHandler`, HS256, claims `sub`, `email`, `role` (one per role), `jti`, `iat`;
   `exp` = now + `Jwt:ExpirationMinutes` (60) from `TimeProvider`. Validation: issuer, audience,
   lifetime, signing key, `ValidAlgorithms = [HS256]`, `ClockSkew = 30 s`, `MapInboundClaims = false`,
   `RoleClaimType = "role"`, `NameClaimType = "email"`.
4. **Authorization**: policy `AuthPolicies.Admin` = authenticated + role `Admin`; `RequireAdmin()`
   extension for endpoint groups (used from Phase 2). No fallback policy (reads stay public).
   401/403 bodies come from the existing `UseStatusCodePages()` + ProblemDetails service (verified by tests);
   `WWW-Authenticate: Bearer` kept.
5. **Rate limiting**: `AddRateLimiter` named policy `login`: fixed window, permit 10, window 60 s,
   queue 0, partition = client IP (after forwarded headers); `OnRejected` → 429 ProblemDetails +
   `Retry-After`. Values bound from `RateLimiting:Login` so tests can lower them.
6. **Forwarded headers**: `X-Forwarded-For`/`Proto`, `ForwardLimit = 1`, trusted networks from
   `ReverseProxy:KnownNetworks` (CIDR list; compose sets the Docker private range `172.16.0.0/12`;
   default trusts loopback only) so throttling sees the real client behind nginx (spec edge case).
7. **Current user**: `ICurrentUser` (Api, from `HttpContext.User`) → `GetCurrentUserQuery` returns
   `{ email, roles, expiresAt }` from validated claims; endpoint `RequireAuthorization()`.
8. **Admin seeding**: `AdminSeeder : ISeeder` (runs with `Database:MigrateOnStartup`). Ensures role
   `Admin`; if no user with `Admin:Email`, creates it with `Admin:Password` and adds the role;
   existing user untouched. Missing email/password or Identity password-rule failures → exception
   naming the setting / failed rules (never the value) → startup fails (non-zero exit).
9. **Logging**: `LoggerMessage` events for login succeeded (user id), failed (reason: unknown,
   wrong password, locked out; masked email `a***@domain`), lockout started. Never password/token.
10. **OpenAPI**: operation transformer adds the Bearer security requirement + 401/403 responses to
    endpoints carrying `IAuthorizeData` without `IAllowAnonymous`; login documents 400/401/429.
11. **GSS noise**: `NpgsqlConnectionStringDefaults.Apply(cs)` sets `GssEncryptionMode=Disable` unless the
    connection string sets it (verified manually: log clean, health OK). Used by DI and the design-time factory.
12. **CI**: `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7` (all `node24`).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| `LoginCommand` persists through Identity's `UserStore` (auto-save) rather than only through the Unit of Work | Identity's lockout counters are written by `SignInManager`/`UserManager`, which save immediately; the UoW commit after it finds nothing extra to save | Disabling Identity auto-save would mean re-implementing lockout bookkeeping for one command |
| Unknown-email path computes a dummy password hash | Prevents timing-based user enumeration (spec FR-004) | A fast "not found" return leaks account existence through response time |
