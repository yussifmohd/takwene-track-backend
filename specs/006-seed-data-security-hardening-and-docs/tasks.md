---
description: "Task list for Phase 5 – Sample Data, Security Hardening and Documentation (API service)"
---

# Tasks: Sample Data, Security Hardening and Documentation (API service)

**Input**: Design documents from `specs/006-seed-data-security-hardening-and-docs/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md
**Tests**: REQUIRED by the constitution (Principle III).
**Skills**: dotnet-clean-architecture (all code), git-workflow, decisions-log

## Format: `[ID] [P?] [Story] Description` — paths relative to the backend repo root.

---

## Phase 1: Setup

- [ ] T001 Confirm no new NuGet packages; pick Trivy and gitleaks image versions (newest ≥ 2 weeks old), check licenses, record in Memory.md

## Phase 2: User Story 1 + 2 – Sample catalogue (P1)

- [ ] T002 [P] [US1] Tests `Infrastructure/Seeding/SampleCatalogTests.cs`: built set has 17 artists (≥ 8 MENA incl. EG/AE/SA/LB, ≥ 6 GB/US, some Arabic-script names), 32 tracks, every genre and status, pending/live/rejected on each DSP, every distributed track ≥ 1 delivery, drafts none, answers after sends, emails `@example.com`, ISRCs valid + unique + `XXSMP` prefix
- [ ] T003 [US1] `Persistence/Seeding/SampleCatalog.cs` (data-model.md) built only through domain factories/methods
- [ ] T004 [US1] `Persistence/Seeding/SampleCatalogSeeder.cs`: transaction, advisory lock (relational only), empty check, skip log, save once; register in DI (Order 20)
- [ ] T005 [P] [US1] Integration `Integration/SampleCatalogSeederTests.cs`: empty → seeded; second run no-op; existing artist → skipped, nothing changed; admin-only DB → seeded and the admin account unchanged (A4); two seeders in parallel → one set, no failure
- [ ] T006 [US2] README "Sample data" section (all sample; how to start without it: switch off / existing data); check no admin password in any committed file (grep + gitleaks in T016)

## Phase 3: User Story 3 – Security (P1)

- [ ] T007 [P] [US3] Test `Api/Security/ChangeRoutesRequireAdminTests.cs`: enumerate `EndpointDataSource`; every POST/PUT/PATCH/DELETE under `/api` except login has AdminOnly; anonymous call to each → 401 (prove it fails if one route loses `.RequireAdmin()`)
- [ ] T008 [P] [US3] Test `Api/Security/OverPostingTests.cs`: extra `id/status/createdAtUtc/version/distributions` on create artist/track and the change bodies are ignored
- [ ] T009 [US3] `Security/ApiRateLimiting.cs`: global per-IP limiter (health exempt) + `writes` per-user policy on change routes; settings `RateLimiting:Api`, `RateLimiting:Writes` validated on start; compose + `.env.example`; `TestApiFactory` high limits
- [ ] T010 [P] [US3] Test `Api/Security/RateLimitTests.cs`: over global → 429 + Retry-After + ProblemDetails, health not limited; over writes → 429 per user; another user unaffected
- [ ] T011 [US3] `Security/SecurityHeaders.cs` middleware + `Program.cs`; test `SecurityHeadersTests.cs` (every API response; no-store on /api; Development docs still load)
- [ ] T012 [US3] CORS: startup validation of origins (absolute http/https, no `*`), methods GET/POST/PATCH; test `CorsTests.cs` (allowed vs other origin, preflight)
- [ ] T013 [P] [US3] Test `Api/Security/ErrorLeakTests.cs`: malformed JSON, 404, 405, 413, forced exception → generic body + traceId, no stack/type/SQL
- [ ] T014 [P] [US3] Test `Api/Security/LogLeakTests.cs`: sign-in ok/failed + create → captured log has no password, token, signing key, connection string
- [ ] T015 [US3] Artist `search` ≤ 100 characters (validator + test) if not already limited
- [ ] T016 [US3] Scans with evidence: `dotnet list package --vulnerable`; Trivy on `trackmgmt-api:local` (+ web image for the frontend rows); gitleaks over both histories; `docker history` for secrets; sign-in timing sample (quickstart 8) with the numbers recorded (A6)
- [ ] T017 [US3] `docs/security-review.md`: findings table for both repos (area, finding, severity, status, fix/reason, evidence) incl. the frontend rows from the frontend plan; all 21 `npm audit` development findings listed (removed with the shadcn CLI / pinned / accepted) with `npm audit --omit=dev` = 0 as evidence that none ship (Q3)

## Phase 4: User Story 4 – Documentation (P1)

- [ ] T018 [US4] README: finished per README rule (sample data, CI + branch protection note, rate-limit settings, security review link, DECISIONS.md link)
- [ ] T019 [US4] `DECISIONS.md`: the three questions, only from `docs/ai-log.md` + developer notes; Q3 example worded per the user (assignment wording invited it, AI modelled status without platforms, developer caught it and had it changed in Phase 4); the known/accepted dev-tool findings; plus a decision record (technical and business decisions and recommendations since the start: who proposed, who decided, why) from Memory.md, the specs and the ai-log
- [ ] T020 [US4] `docs/study-guide.md`: architecture, glossary, 20 questions with honest answers, AI vs developer
- [ ] T021 [US4] Link check (scratch script) on all docs; fix any broken link

## Phase 5: User Story 5 – Delivery (P2)

- [ ] T022 [US5] Quickstart 1–9 on the compose stack; record results in the ai-log
- [ ] T023 [US5] Clean clone in a new scratch folder → build + all tests; `.env.example` complete; dead code/files check (unused files, debug code)
- [ ] T024 Polish: walkthrough Phase 5 section, Memory.md, ai-log Phase 5 (a)/(b)/(c) with the developer's Phase 5 notes, tick tasks; PR; CI green

## Dependencies

- T002 → T003 → T004 → T005. T009 before T010. T016 before T017. T017–T020 before T021. T022–T023 after all code.
- Frontend rows of the security review (T017) come from the frontend plan's checks; the frontend PR follows the backend PR.
