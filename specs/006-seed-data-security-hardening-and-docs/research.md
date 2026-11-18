# Research: Sample Data, Security Hardening and Documentation (API)

## R1 – How the sample is added (Q1)
- **Decision**: New `SampleCatalogSeeder : ISeeder` (Order 20, after `AdminSeeder` = 10), run by the
  existing `DatabaseInitializer` only when `Database:MigrateOnStartup` is on. It opens one transaction,
  takes a PostgreSQL transaction-level advisory lock (`pg_advisory_xact_lock` with a constant key, so a
  second instance waits instead of inserting twice), then checks "no artists and no tracks". Not empty
  → log "Sample catalogue skipped: the catalogue already has data" and return. Empty → build everything
  through the **domain factories and methods** (`Artist.Create`, `Track.Create`, `ChangeStatus`,
  `Distribute`, `RecordAnswer`), save once, commit.
- **Why**: "only into an empty catalogue" is the simplest rule that can never touch real data and makes
  re-runs no-ops. Going through the domain means the sample cannot break an invariant (a distributed
  track without a delivery cannot be built). The lock + transaction cover FR-005.
- **Rejected**: upsert-by-key (would mix sample rows into a real database); EF `HasData` (bypasses the
  domain, can't express deliveries/answers, and every edit becomes a migration); a separate switch (the
  user asked for the existing flag).
- **Raw SQL**: the advisory lock is the second raw SQL statement in the code base (after the UTF-8 check);
  both are constants with no user input – listed in the security review.
- **Test provider**: the EF InMemory path (used by a few tests) has no advisory lock; the seeder skips
  the lock when the provider is not relational.

## R2 – What the sample contains
- 17 artists (9 MENA, 8 international), 32 tracks, deliveries on all three platforms in every state.
  Full list in `data-model.md`. Names, countries, titles, pairings and genres are true and verified by web
  search (sources cited per row); **release dates, ISRCs, statuses, deliveries and emails are invented**.
- **ISRC**: `XX-SMP-26-9NNNN`. `XX` is in ISO 3166's user-assigned range (not a country), `SMP` marks
  "sample", designations start at 90001. Valid format, recognisable as invented, not checked against
  any registry.
- **Email**: `<slug>@example.com` (RFC 2606 reserved domain).
- **Times**: a fixed base (2026-09-01 00:00 UTC) plus per-item offsets, so the data is the same on every
  machine and "answered" is always after "sent".

## R3 – Rate limits beyond sign-in (Q2)
- **Decision**: ASP.NET Core rate limiter (already used for sign-in):
  - a **global** fixed-window limiter partitioned by client IP (after trusted forwarded headers):
    `RateLimiting:Api` PermitLimit 300 / WindowSeconds 60; `/health` and `/api/health` exempt;
  - a **writes** policy on every change route, partitioned by the signed-in user id (`sub`), falling back
    to IP: `RateLimiting:Writes` 60 / 60 s;
  - sign-in keeps its own stricter policy. All answer 429 + `Retry-After` + ProblemDetails (same
    `OnRejected` as sign-in).
- **Why**: per-IP global limit stops floods (including reads, which are public); per-user write limit
  stops a stolen token from bulk-changing the catalogue. Numbers are generous for a human admin and
  configurable.

## R4 – Security headers (API)
- **Decision**: one middleware `SecurityHeaders` early in the pipeline:
  `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`,
  `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (JSON needs nothing),
  `Cache-Control: no-store` on every `/api` response (tokens and admin-only emails must not be cached;
  the data changes anyway). The API's restrictive CSP is **not applied** to the Development-only
  Scalar and OpenAPI pages, so they keep working; every other response gets it – documented.
- **HSTS**: not enabled – there is no HTTPS without a domain (AWS decision 23). Documented.

## R5 – Authorization on every change route
- Today the Admin policy is proven only through test-only endpoints. **New test**: enumerate the real
  `EndpointDataSource`; every route under `/api` with POST/PUT/PATCH/DELETE, except `POST /api/auth/login`,
  must carry the `AdminOnly` policy (metadata), and an anonymous call to each must answer 401. A route
  added later without `.RequireAdmin()` fails the suite.

## R6 – Over-posting
- Bodies bind to explicit request records (`CreateArtistRequest`, `CreateTrackRequest`,
  `ChangeTrackStatusRequest`, `DistributeTrackRequest`, `RecordDistributionAnswerRequest`); ids, status,
  timestamps and deliveries are never bindable. **New tests** send extra fields (`id`, `status`,
  `createdAtUtc`, `distributions`, `version`) and assert they are ignored (new track is still draft with a
  server id).

## R7 – Errors, logs, enumeration
- Errors: `GlobalExceptionHandler` already returns a generic message + traceId. **Check**: malformed JSON,
  unknown route, 405, 413, DB down → no stack, type name or SQL in the body (tests exist for some; add the
  missing cases).
- Logs: sign-in logs only a masked email; EF command logging is at Warning. **New test** captures the log
  during sign-in, a failed sign-in and a create, and asserts no password, token, signing key or
  connection string appears.
- Enumeration: unknown email already runs a dummy hash; lockout answers like a wrong password. **Evidence**:
  existing tests + a measured timing sample (100 attempts each: median and p90 for unknown email vs
  wrong password) whose numbers are recorded in the review (A6: measured, not an automated test,
  because timing tests are flaky).

## R8 – CORS
- Already an allow-list from `Cors:AllowedOrigins`. **Tighten**: methods to `GET, POST, PATCH` (no PUT or
  DELETE routes exist), validate at startup that each origin is an absolute http(s) origin and not `*`
  (fail start otherwise). Test: allowed origin gets `Access-Control-Allow-Origin`, another does not.

## R9 – Input limits
- Body cap 32 KB (existing), validators on all fields (existing), `pageSize` ≤ 50 (existing).
  **Check** `search` on artists: add a 100-character limit (400 above) if missing.

## R10 – Dependencies, images, history (evidence commands)
- `dotnet list package --vulnerable --include-transitive` → none today (2026-11-17).
- Image scan: Trivy run from its official container image, version pinned (newest ≥ 2 weeks old, Apache-2.0),
  against `trackmgmt-api:local` and `trackmgmt-web:local`. A tool run, not a project dependency.
- Secret scan: gitleaks from its official container image, version pinned (MIT), over the full history of
  both repos (`git` mode). Findings triaged in the review.
- Docker: API image already multi-stage, non-root (`$APP_UID`), `.dockerignore` excludes `.env`, tests,
  docs; Postgres bound to 127.0.0.1. Check `docker history` for secrets in layers.

## R11 – Documentation
- `docs/security-review.md`: the findings table for both repos (area, finding, severity, status, fix or
  reason, evidence). README links it.
- `DECISIONS.md` (root): exactly three sections answering the three questions, built only from
  `docs/ai-log.md` and the developer's notes; Q3 example per Q5.
- `docs/study-guide.md`: architecture, glossary, 20 questions with honest answers, AI vs developer.
- Link check: a small script run in the scratchpad (not committed) resolving every relative link and
  anchor in the README, DECISIONS.md, study guide and walkthrough; external GitHub links checked after
  merge paths are known.

## R12 – Clean clone
- `git clone` both repos into a new folder in the scratchpad, then `dotnet test --solution
  TrackManagement.slnx` (Docker running for the PostgreSQL tests) and `npm ci && npm run lint && npm run
  typecheck && npx --no-install vitest run && npm run build`. Nothing is created inside the working repos.
