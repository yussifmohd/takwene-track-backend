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
- Developer wrote/changed: _Developer notes (Yousef):_ see the Phase 0 developer notes above (they cover both repos).

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
- Developer wrote/changed: _Developer notes (Yousef):_ I reviewed the Phase 1 plan before approving it and asked for four changes: IdentityServiceTests in Phase 1 instead of Phase 4, trusted proxy ranges moved into configuration so the AWS load balancer works later, the test-only Admin endpoint kept out of production builds, and stricter JWT validation (HS256 only, 30 s clock skew). I reviewed both Phase 1 PRs and their CI checks on GitHub and merged them with merge commits. I questioned why the admin password lives in .env and not in appsettings.json, and confirmed it is there so no secret is committed. I tested sign-out in the browser, reported it as a bug, and after investigation we found sign-out worked but landed on a public page that looked unchanged, so I decided it should redirect to the login page.

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
  trade-off; sign-out from a protected page goes to the status page). After using it, changed the
  second one: every sign-out now lands on the sign-in page (frontend PR #4, see below).
- Developer wrote/changed: _Developer notes (Yousef):_ see the Phase 1 developer notes above (they cover both repos).

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
- **After the merge: a reported sign-out "bug", my overclaimed diagnosis, and what it really was.**
  Yousef reported that after Sign out the header looked wrong and the state after a reload seemed
  inconsistent. I did **not** reproduce a failure before writing the fix. While investigating in real
  Edge I found four genuine edge cases and fixed them in frontend PR #3
  (`fix-sign-out-session-state-after-reload`, commits b521cdc, 7b369b3, dc9a70a):
  1. the persist `merge` step dropped an expired/malformed stored session in memory only, so the
     rejected entry stayed in `localStorage` until a later write;
  2. `useIsAuthenticated`/`useIsAdmin` compared `expiresAt` with `Date.now()` inside zustand selectors,
     so components rendered at different moments could disagree near expiry;
  3. the `storage` listener ignored `key === null` (another tab clearing all site data);
  4. the top-right toast covered the header's Sign in / Sign out controls for ~4 s.
  These are real and their tests fail on the old code (11 of 17), but **in the PR and in this log I
  presented them as the cause of the reported bug, which I had not shown.** That is the actual
  mistake: a diagnosis stated as fact from suspected causes.
  Yousef then reported the problem again on `main` after PR #3. This time I tried to reproduce it
  against the full compose stack in headless and headed Edge, headed Chrome 157 (his browser), the
  Vite dev server, widths 400–1920 px and with slow/racing requests, tracing the click handler,
  store, storage, query cache, router and guard. Sign-out worked every time: session, storage
  entry and account cache were cleared and the header switched to "Sign in". What Yousef saw was a
  **UX expectation, not a failure**: signing out on the status page left you on that public page,
  which looks almost the same signed in and signed out. He decided sign-out should always land on
  the sign-in page; frontend PR #4 (`fix-sign-out-redirect-and-screen-state`) does that, and after an
  automatic sign-out (expiry/401) the sign-in page explains why and returns you to the page you
  were on (own pages only). That change is a product decision, not an AI mistake.
  Lesson: reproduce first; if a report cannot be reproduced, say so and ask what was expected
  instead of fixing the nearest suspicious code and calling it the cause.

## Phase 2 – Artists & DSPs (003-artists-and-dsps-management / 003-artists-and-dsps-management) – 2026-11-17

### (a) Generated vs written
- AI generated (Spec Kit): spec, clarifications (recommended defaults 1–5), plan, research R1–R13,
  data model, contract intent, quickstart, tasks T001–T038 and the analyze report.
- AI generated (code): ISO 3166-1 country list (249 codes, hash shared with the web app), `Artist`
  and `Dsp` entities, the fixed DSP seed in the migration, create/list artists and list DSPs use cases,
  shared text/paging rules, `AuditableEntityInterceptor`, UTF-8 startup check, conflict → 409 mapping,
  `BadRequestExceptionHandler`, `UniqueConstraintExceptionHandler`, 32 KB body limit, OpenAPI schema
  transformer for fields omitted when null, Testcontainers PostgreSQL fixture, all new tests (239 total,
  13 of them against real PostgreSQL).
- Developer decided: artist email required (assignment's Artist entity); Testcontainers tests in the
  same test project behind `Category=Integration`, with CI running both; the two earlier bugs logged as
  mistakes below; accepted the other defaults.
- Developer wrote/changed: _Developer notes (Yousef):_ I reviewed the plan before approving it and changed three things: the artist email is required to match the assignment's Artist entity, Testcontainers tests stay in the same test project behind a test category so unit tests run without Docker, and the two earlier bugs must be logged as mistakes. I accepted the other defaults (paging, UTF-8 startup check, 32 KB body cap, add-artist page, country dropdown).

### (b) Security
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| Artist emails (personal data) exposed by a public read | `GET /api/artists` | AI design (clarification) | email selected in SQL only for a signed-in admin; omitted from JSON otherwise; tests for anonymous, non-admin token and admin; README states it | done |
| Duplicate email via concurrent requests | create artist | AI design | validator check + unique index on the normalised email; 23505 on that index → same 409 `errors.email`; compose run: 20 parallel creates → 1×201, 19×409, 1 row | done |
| Oversized bodies | all endpoints | AI design | `Api:MaxRequestBodyBytes` 32 KB (Kestrel limit + Content-Length guard) → 413 ProblemDetails | done |
| Malformed input answered with 500 (and a stack trace in the log as an "error") | Development | Phase 0 code, found in Phase 2 planning | see mistake 1; parser details never in the response body (test) | fixed |
| Over-posting | create artist | AI design | request record with three strings → command → `Artist.Create`; entity never bound | done |
| Corrupted Arabic names on a non-UTF-8 database | startup | AI design (Phase 0 analysis) | `DatabaseEncodingCheck` after migrations: anything but UTF8 stops startup with the reason; tested on a `SQL_ASCII` database | done |
| Request log showing the wrong status | logging | found by AI in the compose run | see mistake 4 | fixed |

### (c) Mistakes & fixes
1. **Malformed JSON returned 500 in Development** (since Phase 0). Minimal APIs set
   `RouteHandlerOptions.ThrowOnBadRequest = true` in Development, so a body that is not valid JSON, a
   wrong JSON type or a non-numeric query value throws `BadHttpRequestException` instead of writing 400.
   The only registered exception handler was `GlobalExceptionHandler`, which turns every exception
   into 500. In Testing/Production the framework wrote 400 itself, which is why no earlier test caught
   it (they all ran in "Testing"). Fix: `BadRequestExceptionHandler`, registered before the global
   one, answers with the exception's own status (400/413) as ProblemDetails without parser details.
   `MalformedRequestTests` run in Development, Testing and Production; with the new handler removed,
   exactly the three Development cases fail (500).
2. **Created/updated timestamps were never filled** (latent since Phase 0). `IAuditableEntity`
   (`CreatedAtUtc`, `UpdatedAtUtc`) existed in the Domain, but no entity implemented it and nothing
   set the values: no interceptor, no `SaveChanges` override. The first auditable entity would have
   been stored with `0001-01-01` and no update time. Fix: `AuditableEntityInterceptor` (uses
   `TimeProvider`) sets `CreatedAtUtc` on insert when unset and `UpdatedAtUtc` on update, registered on
   the `DbContext`; `Artist.Create` takes the creation time from the handler's `TimeProvider` because the
   201 body needs it before the Unit of Work saves. Tested with EF InMemory and against PostgreSQL.
3. **Test hosts wrote into each other's logs** (Phase 1 code, surfaced now). `UseSerilog(...)` without
   `preserveStaticLogger: true` assigns the host logger to the static `Log.Logger`, and the logger
   factory writes through that static at log time, so with several in-process hosts every host logged
   into the one started last. Phase 2 starts many more hosts in parallel and the Phase 1 test
   `FailedSignIn_LogsNeitherPasswordNorFullEmail` failed in every full run (passed alone). Fix:
   `preserveStaticLogger: true`. My first version of the fix had a gap: Serilog's request-logging
   middleware then fell back to the static *startup* logger, so request lines skipped the host's sinks
   and configuration. Caught by a new test looking for the request line in the host's own sink; fix:
   pass the host logger to `UseSerilogRequestLogging`. `LogIsolationTests` fails without the fix.
4. **Request log said "responded 500" for requests that got 400/413.** `UseSerilogRequestLogging` sat
   inside `UseExceptionHandler`, so it saw the exception pass and logged 500 before the handler wrote
   400. Found in the compose logs during the quickstart run (the client had received 400). Fix: request
   logging first in the pipeline; test asserts the logged status.
5. Smaller ones, all caught before commit: the Docker image did not copy `.editorconfig`, so it built
   with different analyzer rules than local/CI (the new migration failed CA1861 only in Docker; the
   file is now copied and EF migrations are marked generated code); the PostgreSQL test fixture first
   missed the Identity schema pin (EF "pending model changes"); the first contract export had `Page`/
   `PageSize` query names and listed `email` as required although visitors never get it (fixed with
   explicit query names and a schema transformer); a `PagedResult` name clash with Ardalis.Result.
- **Known log lines left as-is:** a duplicate-email race that reaches the database is logged by EF as
  `ERR Failed executing DbCommand` + "exception while saving changes" before the handler turns it into
  409. It only happens when two creates with the same email race; hiding EF errors globally would hide
  real ones.

### Quickstart results (compose, `down -v` + `up --build`)
DSPs: Apple Music `…0002`, Spotify `…0001`, YouTube Music `…0003`; restart → still 3, same ids,
`server_encoding` UTF8. Four Arabic/mixed names created and listed unchanged; `eg` → `EG`;
duplicate ` Q0@EXAMPLE.com ` → 409 `errors.email`; invalid fields → 400 naming name, countryCode,
email; anonymous create → 401; malformed JSON and `page=abc` → 400; `pageSize=51` → 400; 40 KB body
→ 413; anonymous list without `email`, admin list with. 1,000 artists: pages 1, 25 and 50 in 8, 25 and
9 ms (target < 1 s).

### Frontend (same phase)

**(a) Generated vs written**
- AI generated: spec, plan, research, tasks (T001–T023) and, after the API was merged, the required-email
  update; the country list module (API's 249 codes, shared SHA-256, browser names), artists API module,
  paging hook, list/skeleton/pagination components, Artists page, role-aware `RequireAuth`, Add artist
  page with country select and zod schema, sign-out cache drop, README/walkthrough sections, all new
  tests (132 total, was 95), and a throwaway real-browser script (not committed).
- Developer decided: artist email required (form field required as in the API); accepted the add-artist
  page and the country dropdown defaults.
- Developer wrote/changed: _Developer notes (Yousef):_ "I tested the artists list, paging, the add-artist form (validation and duplicate email) and the sign-out redirect in the browser, and I tested the Phase 2 API in Scalar before merging both PRs."

**(b) Security**
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| Artist emails shown to visitors | artists list | AI design | API omits them; column rendered only for admins (also for a non-admin token that receives them); tested | done |
| Admin emails left in the query cache after sign-out | TanStack Query cache | AI design (R4) | `SessionWatcher` removes `["artists"]` on every sign-out; test fails without it | done |
| Add artist reachable by non-admins | `/artists/new` | AI design | `RequireAuth` + `handle.requiresRole`; the API still enforces Admin (403) | done |
| Script injection via artist names | list, toast | Phase 0 rule | names only as text through `UserText` (no HTML rendering; lint rule) | done |

**(c) Mistakes & fixes**
- **"Required" message lost to a later check.** In zod 4 the refinements after `min(1)` still run, so an
  empty email showed "Enter a valid email address." instead of "Enter the artist's email." Caught by
  the schema test before commit; fix: `min(1, { error, abort: true })`.
- **A test assumed the wrong thing about sign-out.** My first cache test expected no `["artists"]`
  queries after sign-out, but the still-mounted page re-creates its query and refetches *without* a
  token (public data, no emails). The property that matters is "no email survives sign-out"; the test
  now checks that and returning to the list as a visitor, and it fails without the fix.
- **Country label for PS.** The browser's built-in name is engine-dependent ("Palestinian Authority" in
  Edge, "Palestinian Territories" in Node). Found in the real-browser screenshots; fixed with one
  override ("Palestine", following ISO 3166-1 "Palestine, State of") and a test.
- **Walkthrough overclaim corrected:** the walkthrough section for frontend PR #3 now carries the same
  correction as the ai-log entry above (real edge cases, not shown to be the reported cause).

## Phase 3 – Tracks (004-track-catalog-with-filters-and-status / 004-track-catalog-with-filters-and-status) – 2026-11-17

### (a) Generated vs written
- AI generated (Spec Kit): both specs (one batched clarify round, Q1–Q5), plans, research, data
  models, contract intent, quickstarts, tasks (backend T001–T031, frontend T001–T024), analyze report
  and its fixes.
- AI generated (code): `Track` entity, `TrackStatusRules`, `Isrc`, `Genres`, track configuration and
  migration (`xmin` concurrency token, check constraints), create/list/get/change-status use cases,
  artist search and GET by id, `ConcurrencyExceptionHandler`, ISRC case in the unique-index handler,
  OpenAPI value lists for genre/status, interceptor change, all new tests (397 total, was 239; 24 on
  real PostgreSQL).
- Developer decided: Q1–Q5 as recommended; all analyze fixes; how a manual "distributed" fits Phase 4
  (PATCH unchanged; such a track has no distribution rows and the detail says so).
- Developer wrote/changed: _Developer notes (Yousef):_ <!-- to be written by Yousef -->

### (b) Security
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| Lost update: two admins change one track, a distributed track goes back to draft | `PATCH /api/tracks/{id}/status` | AI design (spec FR-008) | domain check on the loaded row + `xmin` row version → 409; PostgreSQL test and compose run (10 parallel withdraw/distribute pairs → always one 200, one 409) | done |
| Duplicate ISRC via concurrent requests | create track | AI design | validator pre-check + unique index → same 409 `errors.isrc`; compose: 20 parallel → 1×201, 19×409 | done |
| Wildcard injection in artist search | `GET /api/artists?search=` | AI design | `Contains` → `strpos` (no LIKE); `%`/`_` literal (tests); 100-character cap | done |
| Artist email leak through the new GET by id | `GET /api/artists/{id}` | AI design | same admin-only projection as the list; test with and without an admin token | done |
| Invalid data written past the validators | `tracks` | AI design | check constraints on ISRC shape, genre and status; FK restrict (PostgreSQL tests) | done |

### (c) Mistakes & fixes
1. **Contract listed track statuses on the distribution's `status`.** My first OpenAPI transformer
   added the enum to every string property named `status`, so `TrackDistributionDto.status` (pending/
   live/rejected in Phase 4) was typed as draft/submitted/distributed. Found by reading the exported
   contract. Fix: limit "status" to the track request/query types; new test
   `ServedDocument_ListsTrackValuesOnlyWhereTheyApply` asserts where the lists appear and where not.
2. **A wrong test input.** The check-constraint test for the ISRC used a 13-character value, so
   PostgreSQL refused it as too long for `char(12)` before the check ran (`ConstraintName` was null).
   Fix: a 12-character lower-case value that only the check constraint refuses.
3. **Unchecked parse result.** The create handler called `TryParseReleaseDate` without checking it;
   the analyzer (CA1806, warnings as errors) stopped the build. Fix: refuse explicitly instead of
   storing a default date.
4. **Process: tests were not always written before the code.** The tasks list tests first; for the
   Application and API layers I wrote each layer's tests right after its code, in the same step.
   All tests were run and passed before committing, but test-first was not followed literally.
5. **Process: one commit does not build on its own.** The PostgreSQL test commit uses the list
   specifications that arrive in the next commit. The branch tip builds and passes; history was not
   rewritten.
- Found while implementing (design gap, not shipped): the audit interceptor overwrote every
  `UpdatedAtUtc` on save, so the PATCH response (built before the save) would differ from the stored
  time. Fix: the interceptor keeps a value the domain set (test added).

### Quickstart results (compose, `down -v` + `up --build`)
- Create with `eg-a1b-26-00001` → 201, `EG-A1B-26-00001`, draft, `allowedNextStatuses: [submitted]`,
  `distributions: []`; anonymous GET of the `Location` → 200.
- Duplicate in another form → 409 `errors.isrc`; 20 parallel creates with one new ISRC → 1×201, 19×409.
- Bad values → 400 naming title, artistId, isrc, genre, releaseDate.
- Status walk: draft→distributed 409, →submitted, →draft, →submitted, →distributed 200, →draft 409
  "final", "published" 400, no token 401.
- 10 rounds of parallel withdraw + distribute on a submitted track: always one 200 and one 409 (6 ended
  distributed, 4 draft – whichever saved first); never back from distributed.
- Filters: `status=published` 400, `artistId=nope` 400, unknown artist → total 0.
- 1,013 tracks: slowest filtered page 37 ms (SC-004 < 1 s).
- Arabic title unchanged after the walk.

### Frontend (same phase)

**(a) Generated vs written**
- AI generated: the frontend spec, plan, research, tasks (T001–T024); API types from the 004 contract;
  ISRC and date helpers, genre/status lists typed against the contract, tracks API module, filter hook
  (URL), queries and mutations, catalogue page with filters and list, `StatusBadge`, track page with
  `StatusControl` (confirm dialog) and `DistributionsSection`, Add track page with zod schema,
  `ArtistPicker` combobox, header `StatusDot`, `/status` page, hand-written shadcn alert dialog,
  README/walkthrough sections, all new tests (200 total, was 132), and throwaway seed and real-browser
  scripts (not committed).
- Developer decided: Q1–Q5 as recommended (all statuses public, no edit/delete, artist search, order,
  confirm before distributed); the wording for a track distributed without deliveries.
- Developer wrote/changed: _Developer notes (Yousef):_ <!-- to be written by Yousef -->

**(b) Security**
| Issue | Where | Introduced/found by | Handling | Status |
|---|---|---|---|---|
| Status changes offered to visitors | track page | AI design | control rendered only for admins; the API still requires Admin (401/403); tested | done |
| Add track reachable by non-admins | `/tracks/new` | AI design | `RequireAuth` + `handle.requiresRole` ("Only admins can add tracks"); tested | done |
| Unvalidated values from the address sent to the API | catalogue filters | AI design | `useTrackFilters` drops unknown status/genre and non-GUID artist ids before any request; tested | done |
| Script injection via titles / artist names | list, track page, picker, toasts | Phase 0 rule | rendered only as text through `UserText` (no HTML) | done |
| Sign-in page promised edit/delete of tracks that don't exist | login page | AI (Phase 1 copy) | list rewritten to what admins can do today | done |

**(c) Mistakes & fixes**
- **Commits that did not build on their own (caught before pushing).** File renames made with
  `git mv` earlier were already staged, so they rode along in the first commit (API types), leaving it
  importing a renamed page. The branch was not pushed yet; I soft-reset to the last planning commit and
  recommitted in buildable steps. Lesson: check `git status` for staged renames before the first commit.
- **Setting state inside an effect** in the artist picker (to show the selected name) – the
  react-hooks lint rule refused it; replaced with adjusting state during render. That replacement was
  itself wrong (next entry).
- **Clear filters left the artist in the field (found by the developer, after the PR was opened).**
  With a status and an artist selected, Clear filters emptied the address and the other filters, but
  the Artist field still showed the name. Cause: the picker kept its own copy of the selected name and
  synced it from props. Clicking Clear first blurred the field, whose handler queued "show the selected
  name"; the address change runs as a router transition, and React replayed the queued update over the
  cleared value. My tests missed it because the sample artist ids were not GUIDs, so the filter never
  took the artist in tests; the browser run never pressed Clear with an artist chosen. Fix: no copy –
  the field shows typed text only while typing, otherwise the selected name derived on each render;
  the filters remember a just-picked name. Regression test with GUID ids fails on the old picker;
  re-checked in the browser (clear, Back restores the artist, × on the field).
- **"Tracks" not highlighted on a track page.** `NavLink to="/"` never matches `/tracks/:id`; a test
  caught it, and the link now computes its own current state.
- **Two timing-sensitive tests** after `/` started loading the catalogue: the session-expiry test
  (300 ms session, header checked before React had drawn `/login`) failed about one run in three under
  the full suite. Not an app bug; the test got more margin and waits for the render. Found by running
  the suite three times, as planned.
- **My first browser script used an ISRC that wasn't a duplicate** (the seed gave that track year 96),
  so it created a stray track instead of testing the 409; script fixed, the stray row stayed in the
  local data only.

**Real-browser results** (compose, 28 artists, 24 tracks; Edge over DevTools, desktop light + 400 px
dark): catalogue, filters in the address (invalid ones ignored), artist search in Arabic and pick,
reload keeps the artist's name, track page, unknown track, sign-in from Add track and back, empty submit
(focus on Title), picker lists the first 10, duplicate ISRC under ISRC, create → track page + toast,
submit, confirm dialog, distributed (final, "without platform deliveries recorded"), paging, status
dot → `/status`, sign-out → `/login`. No horizontal overflow; no app console errors. Bundle 200.7 kB gzip.
