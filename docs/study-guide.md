# Study guide

A plain-language tour of both repositories, a glossary, and the questions an evaluator is likely to
ask, with honest answers. For the decisions themselves see [DECISIONS.md](../DECISIONS.md); for how each
feature works in code see [walkthrough.md](walkthrough.md).

## The system in one page

A music distributor keeps a **catalogue**: artists, their tracks, and where each track was delivered
(Spotify, Apple Music, YouTube Music). Anyone can browse; one admin makes changes.

```text
Browser ──► nginx (web container, port 8081)
              ├─ /            React app (static files)
              └─ /api/...  ──► .NET API (api container, port 8080) ──► PostgreSQL 17
```

**The API is four layers, each allowed to use only the ones inside it:**

| Layer | Holds | Knows about |
|---|---|---|
| Domain | Entities and their rules: `Artist`, `Track` (status changes, distribute, platform answers), `TrackDistribution`, ISRC and country rules | nothing else |
| Application | One class per use case ("create track", "list tracks"), validators, query specifications, DTOs, pipeline behaviors | Domain |
| Infrastructure | EF Core and PostgreSQL, repositories, Unit of Work, Identity, JWT, seeders | Application, Domain |
| Api | HTTP endpoints, error mapping, rate limits, security headers, OpenAPI | all of the above (composition) |

**What happens on `POST /api/tracks/{id}/distribute`:**
1. Middleware: request logging, security headers, error handler, body-size check, CORS, authentication
   (JWT), the global rate limit, then authorization: the route requires the Admin role and the
   per-admin write limit.
2. The endpoint turns the JSON body into a `DistributeTrackCommand` and sends it through MediatR.
3. Pipeline: **Logging** → **Validation** (FluentValidation; failure → 400 with field errors) →
   **Unit of Work** (commands only).
4. The handler loads the track with its deliveries, checks the platform ids exist, and calls
   `track.Distribute(...)`. The entity decides: new platform → pending; rejected → sent again;
   pending/live → unchanged; a submitted track becomes distributed.
5. The Unit of Work saves once. If another admin changed the same track meanwhile, PostgreSQL's row
   version (`xmin`) no longer matches and the save fails → 409.
6. The handler returns a `Result`; the endpoint maps it to `200` with the track, or ProblemDetails.

**The web app** is React. Pages fetch through TanStack Query (cache, loading, retries); the session
(token, expiry, roles) lives in a small zustand store saved in `localStorage`. All HTTP goes through
one `apiFetch` that adds the token, turns errors into ProblemDetails, and signs out on a 401. API types
are generated from the API's OpenAPI document, so a change in the API breaks the web app's type check
instead of failing at runtime.

## Glossary

- **CQRS (Command Query Responsibility Segregation).** Changes (commands) and reads (queries) are separate
  classes with separate paths. Here: commands go through the Unit of Work and load tracked entities;
  queries never save and project straight to DTOs without change tracking.
- **MediatR.** A small library that sends a command or query object to its one handler and runs
  "behaviors" (logging, validation, saving) around it. Pinned to 12.5.0, the last Apache-2.0 version.
- **Result pattern (Ardalis.Result).** Handlers return `Result<T>` (Ok, Invalid, NotFound, Conflict…)
  instead of throwing for expected outcomes. One place maps each kind to an HTTP status. Exceptions are
  left for the unexpected.
- **Unit of Work.** One object that saves all changes of a request in one transaction. Repositories only
  stage changes; the Unit of Work behavior saves once, only for commands, only when the result is a success.
- **Specification (Ardalis.Specification).** A reusable, named query ("tracks matching these filters,
  page 2, newest first") kept in the Application layer, so handlers do not build LINQ by hand and the
  count and the page use the same filter.
- **ProblemDetails (RFC 9457).** The standard JSON shape for HTTP errors: `type`, `title`, `status`,
  `detail`, plus `errors` per field for validation and `traceId`. Every error in the API uses it, and
  the web app shows `title`/`detail` from it.
- **Rate limiting.** Counting requests per key in a time window and answering `429 Too Many Requests`
  (with `Retry-After`) over the limit. Here: sign-in 10/min per IP, everything 300/min per IP, changes
  60/min per admin.
- **JWT (JSON Web Token).** A signed token the API gives after sign-in: who you are (`sub`, `email`),
  your roles, and when it expires. The API checks the signature (HS256, shared secret), issuer,
  audience and expiry on every request; it keeps no session.
- Also used: **ISRC** (International Standard Recording Code, `CC-XXX-YY-NNNNN`, one per recording);
  **DSP** (digital service provider = streaming platform); **row version / optimistic concurrency**
  (detect, don't lock: the second conflicting save fails); **Testcontainers** (tests start a real
  PostgreSQL in Docker); **CSP** (Content-Security-Policy: which sources a page may load).

## Likely questions, honest answers

1. **Why Clean Architecture for an app this size?** It was the given stack, mirroring a reference
   project. The real benefit here: the status and delivery rules live in two entity classes, testable
   without a database, and the API and sample data both go through them. The cost is more files per feature.
2. **Why MediatR 12.5.0 and not the latest?** Version 13 changed to a commercial license. 12.5.0 is the
   last Apache-2.0 release; it is pinned exactly.
3. **Why a Unit of Work when EF Core's DbContext already is one?** So that repositories never save on
   their own and a command saves exactly once, in one transaction, after validation and only on success;
   queries can never save (tested).
4. **How do you stop two admins overwriting each other?** PostgreSQL's `xmin` row version on the track.
   Every change to a track or its deliveries updates the track row; the second save sees a different
   version and gets 409 "This record was changed by someone else." Tested against real PostgreSQL.
5. **Why can't PATCH set "distributed"?** A distributed track must say where it was delivered. Only
   `POST /distribute` reaches that status, and it records at least one delivery. The AI first allowed the
   PATCH; the developer caught it (DECISIONS.md, "What the AI got wrong").
6. **How is the ISRC validated?** ISO 3901 shape (country letters, registrant, year, 5-digit number),
   accepted with or without hyphens in any case, stored as 12 uppercase characters with a unique index
   and a database check constraint, shown hyphenated.
7. **Why are reads public?** Product decision: the catalogue is meant to be browsed; only changes need
   the admin. Artist emails are personal data, so they are returned only to an admin.
8. **Why no refresh tokens, and is `localStorage` safe?** Decision for scope: 60-minute tokens, sign in
   again. `localStorage` is readable by any script on the page, so an XSS bug would expose the token. The
   mitigations are a strict content policy (only the app's own scripts), no raw HTML rendering (lint
   rule), short tokens and removal on sign-out or any 401. An httpOnly cookie would be safer and is the
   first change for production.
9. **Can someone find out which emails have accounts?** The message is identical for every failure, and
   unknown emails still run a password hash. Measured: medians 63.6 ms vs 70.1 ms over 100 tries each –
   close but not identical; with the 10/min limit it is a weak signal.
10. **What stops brute force?** Lockout after 5 wrong passwords for 15 minutes, and 10 sign-in attempts
    per minute per client IP. Forwarded client IPs are trusted only from configured proxy networks, so a
    client cannot fake its IP to dodge the limit (tested).
11. **What if someone posts extra fields like `id` or `status`?** Bodies bind to request records with only
    the editable fields; extras are ignored. Tests post them and check nothing changed.
12. **How do you know every write needs the admin?** A test reads the app's real route table and fails if
    any change route lacks the Admin policy, and calls each one anonymously expecting 401.
13. **Is there raw SQL?** Two constant statements: reading the database encoding, and the seeder's
    advisory lock. No user input reaches SQL outside EF Core parameters; artist search uses `strpos`, so
    `%` and `_` are literal.
14. **How is the sample data kept apart from real data?** It is added only when there are no artists and
    no tracks, in one transaction under a database lock (two instances add it once), and it never touches
    accounts. Names, titles, pairings and genres are real and source-checked; ISRCs (`XX-SMP-…`), emails
    (`@example.com`), dates and statuses are invented.
15. **What did the security review find?** Mainly: change routes were not proven to require the admin,
    no general rate limit, no API security headers, nginx dropping headers on assets, a Google-hosted
    font, and vulnerable OS packages in both images. All fixed with evidence; two image fixes were
    documented exceptions to the version-age rule. See [security-review.md](security-review.md).
16. **Why are 12 `npm audit` findings accepted?** They are one advisory in picomatch, used only by build
    and test tools to match the project's own file patterns, and no fixed release exists. None of it
    ships: the production audit is 0.
17. **How are the two repos kept in sync?** The API exports its OpenAPI document from the running app; a
    test fails when the committed contract drifts. The web app generates its types from that file.
18. **How is it tested?** API: 523 tests (unit, in-memory HTTP, and PostgreSQL via Testcontainers for
    indexes, constraints, ordering, concurrency, seeding). Web app: Vitest + Testing Library with MSW
    fakes. Plus scripted real-browser runs. Key regression tests were checked to fail on the old code.
19. **What would you change for production?** HTTPS with HSTS at a load balancer, token in an httpOnly
    cookie or refresh-token rotation, server-side token revocation, Trivy in CI, branch protection on
    `main`, structured log shipping, and the SDK/package bump to 10.0.15.
20. **How much of this did the AI write?** Nearly all code, tests and documents. The developer set the
    requirements, reviewed and changed every plan, tested in the browser, found bugs the AI missed
    ("Clear filters", unreadable dark dropdowns, "distributed" without a platform), and merged every PR.
    Details below and in DECISIONS.md.

## AI-generated vs developer-changed (summary of the AI log)

| Phase | AI generated | Developer decided or changed |
|---|---|---|
| 0 Foundation | Solution, pipeline, health, Docker, CI, specs, custom skills | 29 interview answers; MediatR pin, Shouldly, license rule; asked for the query-never-saves test and an early library compatibility test |
| 1 Auth | Identity, JWT, login, lockout, rate limit, session store, login page | Identity service tests in this phase, proxy ranges as configuration, test endpoint out of production, stricter JWT; sign-out always to the sign-in page (after testing) |
| 2 Artists & platforms | Artists and DSP endpoints, UTF-8 check, audit timestamps, list and form pages | Email required; PostgreSQL tests in the same project behind a category; log two earlier bugs as mistakes |
| 3 Tracks | Track entity and rules, filters, status changes, catalogue and track pages | Raised the "distributed without a platform" risk; found the "Clear filters" bug in the browser |
| 4 Distribution | Deliveries, distribute, platform answers, dialog and table | PATCH → distributed refused; no data migration; no hard-coded platform count; README rule; found the unreadable dropdowns and chose the fix |
| 5 Hardening and docs | Sample data, security review fixes and evidence, these documents | International legends and labelled sample data; verify every pairing; approved the rate limits, CLI removal and self-hosted font; approved the two image exceptions with conditions |
