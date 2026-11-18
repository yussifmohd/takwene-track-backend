# Implementation Plan: Sample Data, Security Hardening and Documentation (API service)

**Branch**: `006-seed-data-security-hardening-and-docs` | **Date**: 2026-11-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/006-seed-data-security-hardening-and-docs/spec.md`

**Skills**: dotnet-clean-architecture, git-workflow, decisions-log

## Summary

Add a sample catalogue that is created through the domain on the first start against an empty
catalogue (17 artists, 32 tracks, deliveries in every state). Run a security review of both repos
with evidence and fix what is in scope: a test that every change route is admin-only, over-posting
tests, two more rate limits, API security headers, a stricter CORS setup, log and error leak tests,
an artist search length limit, dependency/image/secret scans. Finish the README; write
`DECISIONS.md`, `docs/security-review.md` and `docs/study-guide.md`. Verify from a clean clone.

## Technical Context

**Language/Version**: C# / .NET 10 (SDK 10.0.403)

**Primary Dependencies**: unchanged – **no new NuGet packages**. Tools run from pinned container images,
not added to the project: Trivy (image scan, Apache-2.0) and gitleaks (secret scan, MIT); versions
chosen at implementation (newest ≥ 2 weeks old) and recorded in Memory.md.

**Storage**: PostgreSQL 17 – **no schema change, no migration**.

**Testing**: xUnit v3 + NSubstitute + Shouldly; PostgreSQL tests (`Category=Integration`) for the seeder
(empty → seeded, re-run no-op, non-empty skipped, two instances at once).

**Performance Goals**: seeding adds < 2 s to a first start; later starts add one `EXISTS` query.

**Constraints**: no admin password anywhere in the repo; sample emails only `@example.com`; nothing
claims to be a real catalogue, contact or chart fact.

## Constitution Check

| Principle | Status |
|---|---|
| I. Clean Architecture & CQRS | Seeder in Infrastructure, uses Domain factories only; rate limits/headers in Api. ✅ |
| II. Result-driven edge | 429s are ProblemDetails; no new endpoints. ✅ |
| III. Tests ship with features | Seeder unit + PostgreSQL tests; route-authorization, over-posting, header, CORS, log-leak, rate-limit tests. ✅ |
| IV. Security by default | This phase. Every claim in the review has evidence. ✅ |
| V. Spec-driven | spec → plan → tasks → analyze before code. ✅ |
| VI. Simplicity & license hygiene | No packages; scans via pinned images. ✅ |
| README rule (v1.2.0) | README describes the product; sample-data note added. ✅ |

## Project Structure

### Documentation (this feature)

```text
specs/006-seed-data-security-hardening-and-docs/
├── spec.md, plan.md, research.md, data-model.md, quickstart.md, tasks.md
└── checklists/requirements.md
```

### Source Code (additions/changes)

```text
src/TrackManagement.Infrastructure/Persistence/Seeding/
  SampleCatalog.cs              # the data (records), see data-model.md
  SampleCatalogSeeder.cs        # ISeeder, Order 20: lock, empty check, build via domain, save, commit
src/TrackManagement.Infrastructure/DependencyInjection.cs   # register the seeder
src/TrackManagement.Api/Security/
  ApiRateLimiting.cs            # global per-IP limiter + "writes" per-user policy (replaces/extends LoginRateLimiting setup)
  SecurityHeaders.cs            # middleware: nosniff, DENY, no-referrer, CSP for JSON, no-store on /api
  CorsSetup.cs                  # origins validated at startup; methods GET/POST/PATCH
src/TrackManagement.Api/Endpoints/*.cs   # change routes .RequireRateLimiting("writes")
src/TrackManagement.Api/Program.cs       # UseSecurityHeaders()
src/TrackManagement.Application/Features/Artists/Queries/ListArtists/*Validator.cs  # search ≤ 100 (if missing)
tests/TrackManagement.UnitTests/
  Infrastructure/Seeding/SampleCatalogTests.cs        # data rules: counts, genres, statuses, states per DSP, emails, ISRCs unique/valid
  Integration/SampleCatalogSeederTests.cs             # empty → seeded; re-run; non-empty skip; parallel start
  Api/Security/ChangeRoutesRequireAdminTests.cs       # EndpointDataSource enumeration + anonymous 401
  Api/Security/OverPostingTests.cs
  Api/Security/SecurityHeadersTests.cs, CorsTests.cs, RateLimitTests.cs, LogLeakTests.cs, ErrorLeakTests.cs
docker-compose.yml, .env.example         # new rate-limit settings
docs/security-review.md                  # findings table (both repos)
docs/study-guide.md
DECISIONS.md
README.md                                # sample data, CI + branch protection, security link
```

## Key design decisions (details in research.md)

1. **Sample only into an empty catalogue**, inside a transaction with an advisory lock (R1). Built with
   the domain so invariants hold by construction; a unit test also checks them on the built set.
2. **Data**: `data-model.md` (ISRC `XX-SMP-26-9NNNN`, `@example.com`, fixed base time) (R2).
3. **Rate limits**: global 300/min per IP, writes 60/min per admin, sign-in unchanged (R3).
4. **Headers** middleware for API responses; HSTS documented as not applicable without HTTPS (R4).
5. **Route-authorization test** over the real endpoint table (R5).
6. **Over-posting** tests on every body (R6). **Leak** tests for errors and logs (R7).
7. **CORS**: validated origins, only used methods (R8). **Search** length limit (R9).
8. **Scans** (dependencies, images, history) recorded with commands and output in
   `docs/security-review.md` (R10).
9. **Docs**: DECISIONS.md answers exactly the three questions from the ai-log and the developer's notes;
   study guide as agreed; README links checked (R11). Clean-clone run in the scratchpad (R12).
10. **TestApiFactory** sets high limits so existing tests are not throttled; `RateLimitTests` uses its own
    low limits.

## Complexity Tracking

None.
