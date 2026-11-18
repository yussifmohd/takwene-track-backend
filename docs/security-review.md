# Security review – API and web app

Review of both repositories ([API](https://github.com/yussifmohd/takwene-track-backend),
[web app](https://github.com/yussifmohd/takwene-track-frontend)) on 2026-11-18. Every row names its
evidence: a test in the repo, a command whose output is quoted, or a measurement. Nothing here claims more
than that evidence shows; known limitations are listed as such.

Severity: **High** = could expose data or allow unauthorised changes; **Medium** = weakens a defence or
availability; **Low** = hardening or hygiene. Status: **fixed** (changed in this review), **in place**
(already built earlier, re-checked), **accepted** (known, with the reason), **exception** (a documented
deviation from a project rule).

Tools: Trivy `aquasec/trivy:0.76.1` (Apache-2.0) and gitleaks `zricethezav/gitleaks:v8.30.1` (MIT), both run
from their pinned container images (not project dependencies); `dotnet list package --vulnerable`; `npm audit`.

## API

| # | Area | Finding | Severity | Status | Fix or reason | Evidence |
|---|---|---|---|---|---|---|
| A1 | Authorization | Admin policy was proven only on a test endpoint; nothing stopped a new change route from shipping open | High | fixed | every change route uses `.RequireAdminWrite()` (Admin policy + write limit) | `Api/Security/ChangeRoutesRequireAdminTests.cs` enumerates the real route table: every POST/PUT/PATCH/DELETE under `/api` except sign-in must carry the Admin policy and answer 401 anonymously; removing it from one route makes both tests fail (checked) |
| A2 | Over-posting | Clients could try to set ids, status, version, deliveries or timestamps | High | in place | bodies bind to request records with only the editable fields | `Api/Security/OverPostingTests.cs` (create artist, create track, change status with extra fields: ignored, track stays `draft` with a server id) |
| A3 | JWT | Forged or altered tokens | High | in place | HS256 only, signed tokens required, issuer/audience/lifetime/signature checked, 30 s skew, key ≥ 32 chars or no start | 10 rejected-token cases in `Infrastructure/Auth` + `Api/Auth` tests; `ConfigurationValidationTests` |
| A4 | Rate limiting | Only sign-in was limited; reads and writes could be flooded, a stolen token could bulk-change the catalogue | Medium | fixed | global 300/min per client IP (health exempt), writes 60/min per user, sign-in 10/min per IP; 429 + `Retry-After` + ProblemDetails; values validated on start | `Api/Security/RateLimitTests.cs`; live: 11th sign-in in a minute → `429`, `Retry-After: 60` |
| A5 | Security headers | API responses had no security headers | Medium | fixed | `nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, CSP `default-src 'none'; frame-ancestors 'none'` on every response including errors; `Cache-Control: no-store` on `/api`. Development-only Scalar/OpenAPI pages get no CSP (they could not load otherwise) | `Api/Security/SecurityHeadersTests.cs`; live `curl -D -` on `/api/tracks` and `/health` |
| A6 | CORS | Origins came from config unchecked (a `*` would be accepted); PUT and DELETE allowed though no such routes exist | Medium | fixed | origins must be exact http(s) origins (checked on start), methods `GET, POST, PATCH`, headers `Authorization, Content-Type` | `Api/Security/CorsTests.cs`; live preflight: listed origin allowed, `https://evil.example` gets no CORS headers |
| A7 | Size and length limits | Large inputs | Medium | in place | body 32 KB (413), validated field lengths in code points, `pageSize` ≤ 50, artist `search` ≤ 100, `dspIds` ≤ 20 distinct | `RequestBodyLimitTests`, validator tests per feature |
| A8 | Error leaks | Stack traces, type names or SQL in responses | Medium | in place + tests added | generic 500 ProblemDetails with `traceId`; malformed input → 400 without parser details; 405/415 bodies checked | `ProblemDetailsTests`, `MalformedRequestTests` (Development, Testing, Production), `Api/Security/LeakTests.cs` (405, 415) |
| A9 | Log leaks | Passwords, tokens, keys or connection strings in logs | Medium | in place + test added | sign-in logs a user id and a masked email only; EF command logging at Warning | `Api/Security/LeakTests.cs` captures the log over a failed sign-in, a sign-in, a create and `/me`: no password, token (or its signature), signing key, connection string or full email |
| A10 | Raw SQL | Injection through hand-written SQL | High | in place | two raw statements, both constants with no input: the UTF-8 check and the sample seeder's `pg_advisory_xact_lock(<const>)`; everything else is EF LINQ (parameters); artist search uses `strpos`, so `%`/`_` are literal | `grep -rnE "FromSql\|ExecuteSql\|SqlQuery\|NpgsqlCommand" src` → only those two lines; search tests |
| A11 | User enumeration | Telling registered emails apart | Medium | in place, measured | one `401` "Invalid email or password." for unknown email, wrong password and lockout; unknown emails verify a dummy hash | `LoginEndpointTests`; **timing sample** on the compose stack (100 attempts each, alternating, limits raised for the run): unknown email median **63.6 ms** (p90 78.3, min 56.9, max 137.3); wrong password median **70.1 ms** (p90 87.2, min 62.4, max 126.3). A gap of about 6.5 ms remains (the wrong-password path also records the failure); with 10 attempts per minute per IP and network jitter it is a weak signal, but it is not zero |
| A12 | Lockout | Password guessing on the one account | Medium | in place | 5 failures → 15 min lockout, answered like a wrong password | `LoginEndpointTests` |
| A13 | Dependencies | Known-vulnerable NuGet packages | – | none found | – | `dotnet list TrackManagement.slnx package --vulnerable --include-transitive` → "no vulnerable packages" for all 5 projects (2026-11-18) |
| A14 | Container image | .NET runtime 10.0.14 in `aspnet:10.0.14-alpine3.24`: CVE-2026-114146 (HIGH, elevation of privilege), CVE-2026-114147 and CVE-2026-114149 (HIGH, denial of service), CVE-2026-109476 (MEDIUM, information disclosure), CVE-2026-109475 (MEDIUM, .NET denial of service); zlib CVE-2026-4738 (MEDIUM) | High | **exception**, fixed | runtime base image → `aspnet:10.0.15-alpine3.24` (pinned by tag; the project pins no digests). 10.0.15 was published 2026-11-10, younger than the project's two-week rule; the developer approved the exception for a security patch on 2026-11-18. SDK image and NuGet packages stay on 10.0.403 / 10.0.14 (not shipped / not flagged); bump them once 10.0.15 is two weeks old (2026-11-24) | Trivy before: 6 findings (3 HIGH, 3 MEDIUM); after a `--no-cache` rebuild: **0**; container `uid=1654(app)`, `/health` 200, full compose stack checked |
| A15 | Docker hardening | Root user, secrets or source in the image, exposed database | Medium | in place | multi-stage build, non-root `USER $APP_UID`, `.dockerignore` excludes `.env`, tests and docs; Postgres published on `127.0.0.1` only | `docker image inspect` → `User=1654`; `ls -a /app` has no `.env` or source; `docker history --no-trunc` contains no secret (only `adduser --disabled-password`) |
| A16 | Secrets in git history | Committed credentials | High | none found | `.env` gitignored; `.env.example` has empty placeholders; admin password only from `ADMIN__PASSWORD` | gitleaks over the full history: 109 commits, 1 finding = the fake 32-character test key in `ConfigurationValidationTests.cs` (test fixture, not a secret). The real local admin password, signing key and database password: `git log --all -S` and `git grep` over every revision → 0 hits |
| A17 | Sample data | Seeding could create or change accounts, or mix with real data | Medium | in place | seeder runs only when there are no artists and no tracks, never touches Identity tables, one transaction under an advisory lock | `Integration/SampleCatalogSeederTests.cs` (admin unchanged, existing artist → skipped, parallel start → one set; without the lock the parallel test fails with a duplicate key – checked) |
| A18 | Transport | No HTTPS, so no HSTS | Medium | accepted | no domain/certificate in this project; TLS belongs at the load balancer when one exists | – |
| A19 | Token revocation | Sign-out is client-side; a stolen token stays valid until it expires | Medium | accepted | 60-minute tokens, no refresh tokens (interview decision); revocation would need server-side state | – |
| A20 | Data Protection keys | ASP.NET warns keys are kept inside the container | Low | accepted | nothing in the API uses Data Protection payloads (JWTs have their own key) | startup log |

## Web app

| # | Area | Finding | Severity | Status | Fix or reason | Evidence |
|---|---|---|---|---|---|---|
| W1 | Third-party requests | The font came from Google, so every visitor's address reached Google and the CSP had to allow two Google hosts | Medium | fixed | Readex Pro served by the app (`public/fonts`, four woff2 files + `OFL.txt`, SIL OFL 1.1) | real Edge over DevTools, six pages incl. Arabic text and signed-in pages: 48 requests, all to the app's own origin (plus inline `data:` icons), 0 to Google; `src/thirdPartyHosts.test.ts` fails if an external URL returns (checked on the old files) |
| W2 | Security headers | `location /assets/` set its own `Cache-Control`, which made nginx drop the server-level headers: scripts and styles were served with only `nosniff`; `/api/` responses got nginx copies on top of the API's own | Medium | fixed | one snippet `nginx/security-headers.conf` included in `/` and `/assets/`; `/api/` passes the API's headers; CSP `default-src 'self'` with no third-party host, `object-src 'none'`, `frame-ancestors 'none'`, `base-uri`/`form-action 'self'`; COOP `same-origin`; Permissions-Policy; duplicate `Cache-Control` on assets removed | `curl -D -` on the running container for `/`, an asset, a font, the license, a deep link and `/api/dsps` |
| W3 | Dependencies (shipped) | Known-vulnerable packages in the browser bundle | – | none | – | `npm audit --omit=dev` → 0; `npm ls --omit=dev picomatch minimatch` → empty |
| W4 | Dependencies (development tools) | `npm audit` listed **21** packages (13 high, 8 moderate), all build/lint/test tools, on the paths to two advisories: minimatch ReDoS-style DoS **GHSA-g3hv-75wg-m249** (< 11.0.1) and picomatch ReDoS **GHSA-fc5q-39xc-vjpf** (≤ 4.0.7) | Low | 9 fixed, 12 **accepted** | removed the unused `shadcn` CLI and `components.json` (clears `shadcn`, `@shadcn/registry`, `@dotenvx/dotenvx`, `ts-morph`, `@ts-morph/common`, `fast-glob`, `micromatch`); `overrides` moves eslint's and typescript-eslint's minimatch 10.2.6 → 11.0.4 (BlueOak-1.0.0, published 2026-11-02; clears `minimatch`, `eslint`). **Accepted (12)**: `picomatch` has no fixed release (4.0.7 is the newest); it reaches `vite`, `vitest`, `@vitejs/plugin-react`, `tinyglobby`, `fdir`, `typescript-eslint`, `@typescript-eslint/{eslint-plugin,parser,type-utils,typescript-estree,utils}`. It only matches the project's own file patterns at build/test time on a developer machine or CI, never user input, and none of it ships to browsers | `npm audit`: 21 → 12, all 12 via GHSA-fc5q-39xc-vjpf; `--omit=dev` 0; lint, typecheck, 219 tests and build pass with the override |
| W5 | Container image | `nginx-unprivileged:1.30.6-alpine` (newest push 2026-10-19; mainline 1.31.7 has the same base) ships Alpine packages with **61** known CVEs: 1 CRITICAL (brotli-libs CVE-2026-111497), 12 HIGH (libexpat CVE-2026-113351/-115506/-117610/-118370, libxslt CVE-2026-125350/-87636/-9947, curl and libcurl CVE-2026-92392, musl and musl-utils CVE-2026-107891, zlib CVE-2026-85091), 4 MEDIUM, 44 LOW (48 of the 61 in curl/libcurl) | High | **exception**, fixed | runtime stage: `apk del curl` (unused; the healthcheck uses busybox `wget`) and `apk upgrade --no-cache`, then back to uid 101; base tag unchanged. Package versions are **not pinned** because Alpine removes superseded versions from its repository, which would break the build; so **re-scan the image with Trivy on every rebuild**. Decided by the developer 2026-11-18 | Trivy before: 61; after a `--no-cache` rebuild: **0** (Alpine 3.24.4 packages); `uid=101(nginx)`, healthcheck healthy, SPA fallback 200, `/api` proxy and headers checked on the full compose stack |
| W6 | Image contents | Source, `.env` or tooling in the image | Low | in place | multi-stage; only `dist/` and the nginx config/snippet are copied; `.dockerignore` excludes `.env`, specs, docs | `find /usr/share/nginx/html` in the container: `index.html`, `favicon.svg`, `assets/`, `fonts/` (+ the base image's `50x.html`) |
| W7 | Script injection (XSS) | User-typed names and titles rendered as HTML | High | in place | text only through `UserText`; ESLint bans `dangerouslySetInnerHTML` | `eslint.config.js` rule; `grep -rnE "dangerouslySetInnerHTML\|innerHTML\|insertAdjacentHTML\|document.write\|eval\("` over `src` (non-test) → none |
| W8 | Error display | Raw response bodies or stack traces shown to users | Low | in place | `describeError` shows only ProblemDetails `title`/`detail` or fixed texts | `src/api/problem.ts`, `toast.test.ts`, `client.test.ts` |
| W9 | Token storage | The access token is in `localStorage`, readable by any script on the page | Medium | accepted | interview decision (no refresh tokens, no cookie session); mitigated by a CSP that allows only the app's own scripts, no raw HTML, 60-minute tokens, removal on sign-out and on any 401 | README; CSP in W2 |
| W10 | Open redirect | `?redirect=` after sign-in | Medium | in place | `safeRedirect` accepts same-origin paths only | 10 cases in its tests |
| W11 | Secrets in history | – | – | none found | – | gitleaks: 102 commits, 0 findings |
| W12 | Lock file | Only direct dependencies were age-checked when chosen; npm resolved some transitive versions newer than two weeks at install time (e.g. minimatch 11.0.9 under `@eslint/config-array`, published one day before the install) | Low | accepted | the lock file freezes every version (`npm ci`), so builds are reproducible; transitive ages were not part of the rule | `package-lock.json` history |

## How to repeat the checks

```bash
# API
dotnet list TrackManagement.slnx package --vulnerable --include-transitive
dotnet test --solution TrackManagement.slnx -- --filter-class "*Security*"
docker compose build --no-cache api
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:0.76.1 image trackmgmt-api:local
docker run --rm -v "$PWD:/repo:ro" zricethezav/gitleaks:v8.30.1 git /repo --redact

# Web app (frontend repo)
npm audit && npm audit --omit=dev
docker compose build --no-cache web
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:0.76.1 image trackmgmt-web:local
curl -sI http://localhost:8081/ ; curl -sI http://localhost:8081/api/dsps
```

The image findings change as new CVEs are published: rerun Trivy on every rebuild (W5) and before a release.
