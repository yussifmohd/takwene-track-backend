# Research: Admin Sign-in & Access Control (API service)

Checked 2026-11-17. Version rule unchanged (newest stable ≥ 2 weeks old, permissive license).

## R1. New package

| Package | Version | License | Published | Notes |
|---|---|---|---|---|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.14 | MIT | 2026-10-25 | Identity stores; depends on Identity.Stores + EF Relational 10.0.14 |

Already present: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.14 (brings
`Microsoft.IdentityModel.JsonWebTokens` 8.19.2). Rate limiting (`Microsoft.AspNetCore.RateLimiting`)
and forwarded headers are part of the shared framework. Test time control uses a small
`TimeProvider` subclass in the test project instead of adding `Microsoft.Extensions.TimeProvider.Testing`.

## R2. Identity setup

- **Decision**: `AddIdentityCore` + roles + EF stores + SignInManager (no cookie schemes).
  `ApplicationUser : IdentityUser<Guid>` (Guid keys like the rest of the model).
  `AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`; tables renamed to
  `users, roles, user_roles, user_claims, user_logins, user_tokens, role_claims` (columns already
  snake_case via the naming convention), as in the reference project.
- **Lockout**: `MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`, `AllowedForNewUsers = true`.
  The reference disabled lockout (`lockoutOnFailure: false`); not copied.
- **Alternatives**: hand-rolled user table + BCrypt (more code, more risk); full `AddIdentity`
  (adds cookie auth we don't use).

## R3. Generic failures and timing

- **Decision**: one `Result.Unauthorized("Invalid email or password.")` for unknown email, wrong
  password and locked-out account → identical 401 ProblemDetails (title "Unauthorized", same
  detail; only `traceId` differs). For unknown emails, `IPasswordHasher.VerifyHashedPassword`
  runs against a precomputed dummy hash so response time matches a real check.
- The reference returned a message revealing unconfirmed accounts; not copied.
- Locked-out logins are logged (warning) for the operator; the caller sees the generic error.

## R4. JWT

- **Decision**: `JsonWebTokenHandler` (recommended over `JwtSecurityTokenHandler`), HS256 with the
  ≥ 32-char key from `Jwt:SigningKey`, 60-minute lifetime, `ClockSkew` 30 s, `ValidAlgorithms = [HS256]`
  (rejects `alg: none` and algorithm confusion), `MapInboundClaims = false` so claim names stay
  `sub`/`email`/`role`. Bearer options configured via `IConfigureNamedOptions<JwtBearerOptions>` that
  reads validated `JwtOptions`.
- No refresh tokens or revocation (interview 3); sign-out is client-side; documented trade-off.

## R5. Rate limiting behind a proxy

- **Decision**: built-in rate limiter, fixed window 10/min per client IP for the `login` policy,
  rejection → 429 ProblemDetails with `Retry-After` (seconds, from lease metadata).
- In compose, requests from the web app arrive from the nginx container. `UseForwardedHeaders`
  with `KnownIPNetworks` from config (`ReverseProxy:KnownNetworks`, compose: `172.16.0.0/12`)
  restores the original client IP; requests from untrusted sources keep their socket address
  (spoofed `X-Forwarded-For` ignored). AWS (Phase 6) will set the VPC CIDR for the ALB.
- In-memory limiter state is per instance; with 2 EC2 instances the effective limit is ≤ 20/min,
  acceptable and noted for Phase 6.

## R6. Admin seeding

- **Decision**: `AdminSeeder` implements the Phase 0 `ISeeder` hook (runs after migrations when
  `Database:MigrateOnStartup` is true). `AdminOptions { Email, Password }` bound from `Admin`
  (env `Admin__Email`, `Admin__Password`; `.env` keys `ADMIN__EMAIL`, `ADMIN__PASSWORD`).
  Validation happens inside the seeder (only needed when seeding), failing with the setting name.
- Existing user (by normalised email) is never modified (clarification).

## R7. Container log noise (`libgssapi_krb5.so.2`)

- **Finding**: reproduced on `docker compose up`: Npgsql defaults to `GssEncryptionMode=Prefer` on
  Linux and tries to load the Kerberos library, which the Alpine runtime image doesn't ship.
- **Options**: (a) `apk add krb5-libs` (+~3 MB, an unused native dependency in the image);
  (b) `Gss Encryption Mode=Disable` in the connection string.
- **Decision**: (b), applied in code by `NpgsqlConnectionStringDefaults` unless the connection
  string sets the keyword, so compose, local and AWS all benefit. Verified manually with an env
  override: no `gssapi` lines, `/health` 200. We don't use Kerberos/GSS authentication.

## R8. GitHub Actions (Node 20 deprecation warning)

| Action | Was | Now | Runtime | Latest release |
|---|---|---|---|---|
| actions/checkout | v4 | **v7** | node24 | v7.0.1 (2026-07-20) |
| actions/setup-dotnet | v4 | **v6** | node24 | v6.1.0 (2026-11-03) |
| actions/upload-artifact | v4 | **v7** | node24 | v7.0.2 (2026-10-07); v5 is still node20 |
| actions/setup-node (frontend) | v4 | **v7** | node24 | v7.1.0 (2026-10-08) |

All inputs used today (`global-json-file`, `cache`, `cache-dependency-path`, `node-version-file`,
`path`, `if-no-files-found`) exist in these majors. Major tags kept (as in Phase 0).

## R9. OpenAPI for protected endpoints

- **Decision**: `IOpenApiOperationTransformer` adds `security: [{ Bearer: [] }]` and 401/403
  responses when endpoint metadata contains `IAuthorizeData` and no `IAllowAnonymous`. Scalar then
  shows the lock and sends the token. Contract exported to `contracts/openapi.json` with
  `scripts/export-contract.ps1 -Feature 002-jwt-authentication-and-authorization`; the frontend
  regenerates its types from it.
