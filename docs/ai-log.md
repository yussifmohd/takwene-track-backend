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
- Developer wrote/changed: _Developer notes (Yousef):_ Local Postgres already used port 5432, so I set `POSTGRES_HOST_PORT=5433` in my `.env`. I ran both compose stacks and checked the health page myself, checked the CI checks on both PRs, and merged them with merge commits.

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

### Frontend (same phase)

**(a) Generated vs written**
- AI generated: Vite/TS/ESLint/Vitest config, API client + ProblemDetails helpers, generated
  contract types, health card (skeleton, healthy, unhealthy 503-as-data, unreachable + Retry,
  30 s refresh), app shell, not-found route, design tokens (frontend-design skill: indigo ink,
  saffron accent, Readex Pro for Latin + Arabic, level-meter motif), nginx/Docker/compose, CI with a
  bundle budget, 19 tests.
- Developer wrote/changed: _Developer notes: (to be filled in by Yousef)_

**(b) Security**
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| Raw HTML injection (XSS) | React components | constitution rule | ESLint rule forbids `dangerouslySetInnerHTML` | done |
| Missing browser security headers | nginx | AI design | CSP (self + Google Fonts only), `X-Frame-Options DENY`, `nosniff`, Referrer-Policy, Permissions-Policy, `server_tokens off` | done |
| Container runs as root | web image | AI design | `nginx-unprivileged` (verified `uid=101`) | done |
| Secrets in the bundle | Vite env | AI design | only `VITE_API_BASE_URL`; README warns `VITE_*` is public | done |
| Unpinned deps injected by tooling | `shadcn add` | found by AI on review of package.json | removed `cn` (unrelated npm package) and `next-themes` | fixed |
| Dev-only advisory (minimatch DoS) | eslint / typescript-eslint / shadcn / redocly | `npm audit` | accepted: build tooling only, our own glob patterns; production audit clean | accepted |
| Token in localStorage (XSS exposure) | auth store | planned trade-off | documented in Phase 1 | planned |

**(c) Mistakes & fixes**
- **shadcn generated broken imports.** Running `shadcn add` without `init` rewrote the `cn`
  helper import as `from "cn"` and installed the unrelated npm package `cn` plus `next-themes`,
  both unpinned. Caught by reviewing `package.json`; fixed imports to `@/lib/utils` and removed both
  packages (070ebaa).
- **Skeleton used the accent colour.** shadcn's Skeleton is `bg-accent`, which is saffron in this
  palette; changed to `bg-muted`.
- **MSW 3 option renamed.** I used `onUnhandledRequest` (MSW 2); MSW 3 calls it `onUnhandledFrame`
  (caught by `tsc`).
- **Retry test was wrong about the hook.** The test failed a single request, but the hook retries
  once, so the error state never appeared. Fixed by failing two attempts and waiting past the
  ~1 s backoff.
- **Mobile overflow at 360 px.** First screenshots showed the nav pushing the page wider than the
  screen; fixed with a scrollable nav and stacked card header (fb45bb0). Also learned headless Edge
  enforces a 496 px minimum viewport, so 360 px was verified inside an iframe.

## Phase 1 – JWT authentication & authorization (002-jwt-authentication-and-authorization / 002-jwt-authentication-and-authorization) – 2026-11-17

### (a) Generated vs written
- AI generated (Spec Kit): spec, clarifications (recommended defaults B1–B4, F1–F2), plan,
  research, data model, auth contract, quickstart, tasks and the analyze report (A1–A7) in both repos.
- AI generated (backend code): Identity store with snake_case tables + `AddIdentity` migration,
  `IdentityService` (generic failures, dummy-hash timing, masked-email logs), `JwtTokenService` +
  shared validation rules, `AdminSeeder`, `/api/auth/login` + `/api/auth/me`, Admin policy +
  `RequireAdmin()`, login rate limiter, trusted forwarded headers, OpenAPI Bearer requirement,
  CI action upgrades, Npgsql GSS fix, all new tests (127 total).
- Developer decided: approved the clarify defaults; required A2 (`IdentityServiceTests`) in this
  phase instead of deferring it; required the trusted proxy ranges to be configuration (for the AWS
  load balancer in Phase 6); required the Admin-policy proof endpoint to live only in the test
  project; fixed the JWT rules (issuer, audience, lifetime, signature, HS256 only, skew ≤ 30 s);
  asked for CI actions on Node 24 and a clean container log (no `libgssapi_krb5` noise).
- Developer wrote/changed: _Developer notes (Yousef):_ <!-- to be filled in by the developer -->

### (b) Security
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| Account lockout disabled | reference project | AI (Phase 0 read) | Identity lockout: 5 failures → 15 min; tested at service and HTTP level | fixed |
| User enumeration via login message | reference project | AI (Phase 0 read) | one 401 "Invalid email or password." for unknown email, wrong password and lockout | fixed |
| User enumeration via timing | login | AI design | unknown emails verify a dummy hash (test counts the call); measured 138 ms vs 161 ms on compose | fixed |
| Brute force from one IP | login | AI design | fixed window 10/min per client IP, 429 ProblemDetails + `Retry-After`; throttled requests never reach the credential check | done |
| Spoofed `X-Forwarded-For` bypassing the limit | forwarded headers | AI design, developer made it config | trusted only from `ReverseProxy:KnownNetworks` (CIDR, validated on start), `ForwardLimit=1`; test proves a spoofing client stays throttled | done |
| Token forgery (`alg: none`, algorithm swap, tampering) | JWT validation | developer rule + AI tests | `ValidAlgorithms=[HS256]`, signed tokens required, issuer/audience/lifetime/key validated, skew 30 s; 10 rejected-token cases tested | done |
| Passwords/emails in logs | IdentityService | developer asked for a test | only user id + `a***@domain`; tests assert password and full email never appear (service and HTTP level) | done |
| Admin password in the repo | config | AI design | only in `.env` / user-secrets; `.env.example` has an empty placeholder; compose fails fast when unset; seeder errors name the setting, never the value | done |
| Test-only endpoint shipping to production | Admin policy tests | developer rule | defined in `TestApiFactory` via `IStartupFilter`; excluded from OpenAPI; guard test asserts the document has only `/api/` routes | done |
| Weak admin password | seeder | AI design | Identity rules (12+, upper, lower, digit, symbol) enforced on creation; startup fails otherwise | done |

### (c) Mistakes & fixes
- **Test-only endpoints leaked into the exported contract.** The first export listed
  `/admin-only` and `/public`: `UseEndpoints` inside the test branch registers into the app's shared
  endpoint data source, which OpenAPI reads. Caught by inspecting the exported paths. Fix:
  `ExcludeFromDescription()` plus a guard test that every documented path starts with `/api/` (715f565).
- **Identity in .NET 10 adds a passkeys table** that has a keyless owned type; the first migration
  attempt failed ("IdentityPasskeyData requires a primary key"), and I then tried to override a
  method that does not exist (`OnModelCreatingVersion` with a version argument on
  `IdentityDbContext`). Fix: pin the Identity schema version (`Version2`, no passkeys) through
  `IdentityOptions.Stores.SchemaVersion` at runtime and in the design-time factory (05a5be2).
- **Async test helper disposed the request too early.** `using var request` with a non-awaited
  `return client.SendAsync(...)` disposed the request mid-flight (17 `ObjectDisposedException`s).
  Fix: `async`/`await` in the helper.
- **Unreliable assumption about Identity and `TimeProvider`** (from analyze A1): lockout uses the
  system clock, so lockout expiry is tested by moving `LockoutEnd` into the past instead of faking time.
- **Known log lines left as-is:** on the very first start against an empty database EF logs one
  `ERR Failed executing DbCommand` for `SELECT … FROM "__EFMigrationsHistory"` before creating the
  table (benign, not repeated on restart; verified both migrations recorded once). ASP.NET Data
  Protection warns that keys are stored inside the container; nothing in this API uses Data
  Protection payloads (JWTs use their own key), so it is documented rather than configured.
  The `libgssapi_krb5` error no longer appears.

### Frontend (same phase)

**(a) Generated vs written**
- AI generated: persisted session store, API client wiring (`setupAuth`), redirect helper, login
  page, `RequireAuth`, account page, header menu, `SessionWatcher`, README section, all new tests (81
  total), and a throwaway Edge DevTools-protocol script (not committed) for real-browser checks and
  screenshots.
- Developer decided: approved F1/F2 clarifications (session in localStorage with the documented
  trade-off; sign-out from a protected page goes to the status page).
- Developer wrote/changed: _Developer notes (Yousef):_ <!-- to be filled in by the developer -->

**(b) Security**
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| Token readable by script (localStorage) | session store | planned trade-off (F1) | CSP without inline/third-party scripts, no `dangerouslySetInnerHTML` (lint), 60-min tokens, key removed on sign-out; documented in README | accepted |
| Open redirect via `?redirect=` | login page | AI design | `safeRedirect`: same-origin paths only; rejects `//`, backslash, schemes, control characters, `/login` (10 cases tested) | done |
| Stale session after token rejected | API client | AI design | any 401 on an authenticated call signs out and drops `["auth"]` queries | done |
| Wrong-password 401 treated as session loss | API client | AI design | `skipAuthHandling` on the login call | done |
| Sign-out not reaching other tabs | session | AI design | `storage` event → sign out / rehydrate | done |
| Stale session left in storage | session store | found by AI in the real-browser run | storage adapter removes the key when there is no session; regression test | fixed |

**(c) Mistakes & fixes**
- **Sign-out on `/account` landed on the sign-in page.** All jsdom tests passed; the real-browser
  run showed `/account → / → /login?redirect=%2Faccount`. React Router applies navigations as
  transitions, so the synchronous store update re-rendered the guard on the old page first. My first
  fix (`flushSync: true`) silently did nothing: the browser console said `RouterProvider` must come
  from `react-router/dom`. Fix: both, and the test renderer now uses the same provider (40c3c8f).
- **Stored session not removed on sign-out.** `signOut` cleared storage, but clearing the sign-out
  reason right after re-persisted `{session: null}` (9baf560).
- **shadcn injected the `cn` package again** (`from "cn"` imports), as in Phase 0; removed before commit.
- **Ran an unpinned tool.** I used `npx prettier` to reformat one file although prettier is not a
  project dependency, so npx fetched an unvetted copy. Nothing was installed into the project, but it
  broke my own license/pinning rule; noted in Memory.md to not repeat.
- **Header wrapped on desktop when signed in**, and the side note sat 16 px off the form; found in
  screenshots and fixed (e1509ff).
