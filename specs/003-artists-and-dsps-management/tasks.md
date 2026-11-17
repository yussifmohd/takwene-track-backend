---
description: "Task list for Phase 2 – Artists & Distribution Platforms (API service)"
---

# Tasks: Artists & Distribution Platforms (API service)

**Input**: Design documents from `specs/003-artists-and-dsps-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/artists-api.md, quickstart.md
**Tests**: REQUIRED by the constitution (Principle III).
**Skills**: dotnet-clean-architecture (all code), git-workflow, decisions-log

## Format: `[ID] [P?] [Story] Description` — paths relative to the backend repo root.

---

## Phase 1: Setup

- [ ] T001 License re-check and add `Testcontainers.PostgreSql` 4.16.0 to `Directory.Packages.props` + the test project (transitives MIT, research R8); record in Memory.md
- [ ] T002 [P] `Api:MaxRequestBodyBytes` option (default 32768, validated > 0) in `appsettings.json` + options class (R12)

---

## Phase 2: Foundational (blocks all stories)

- [ ] T003 [P] Tests `Domain/CountryCodesTests.cs`: count 249, SHA-256 of the comma-joined sorted list (constant shared with the frontend), EG/SA/AE/PS/SS accepted, `eg`/` eg ` normalised, UK/EU/XK/ZZ/empty refused
- [ ] T004 [P] `Domain/Countries/CountryCodes.cs` (`FrozenSet`, `IsAssigned`, `Normalize`); re-verify the 249 codes against the ISO 3166-1 list (R5)
- [ ] T005 [P] Tests `Domain/ArtistTests.cs` (trim, upper country, normalised email, guard on missing/invalid input) and `Domain/DspTests.cs` (three seeds, fixed ids, unique names)
- [ ] T006 `Domain/Artists/Artist.cs`, `Domain/Dsps/Dsp.cs` (`Dsp.Seed` fixed GUIDs, R9); `Artist : IAuditableEntity`
- [ ] T007 [P] Tests `Infrastructure/AuditableEntityInterceptorTests.cs` (Added → CreatedAtUtc from fake `TimeProvider`; Modified → UpdatedAtUtc)
- [ ] T008 `Infrastructure/Persistence/AuditableEntityInterceptor.cs` + DI registration (R10)
- [ ] T009 `ArtistConfiguration`, `DspConfiguration` (lengths, `char(2)`, unique `ix_artists_normalized_email`, `ix_dsps_name`, `HasData` seeds); `DbSet`s; migration `AddArtistsAndDsps` (review the generated SQL)
- [ ] T010 [P] Tests `Application/Common/TextRulesTests.cs` (code-point length: 200 Arabic/emoji accepted, 201 refused; email format shared with login)
- [ ] T011 `Application/Common/Validation/{TextRules, ValidationErrorCodes}.cs`, `Common/Models/PageRequest` rules; `LoginCommandValidator` uses the shared email rule (no behaviour change, Phase 1 tests stay green)
- [ ] T012 [P] Tests `Api/ResultExtensionsTests.cs` additions: Invalid with only conflict-coded errors → 409 ValidationProblem with `errors`; mixed → 400 with all errors
- [ ] T013 `ResultExtensions` conflict mapping + `ToCreatedHttpResult` overload returning the DTO (R2, R13)
- [ ] T014 [P] Tests `Api/MalformedRequestTests.cs` (invalid JSON, wrong type, non-numeric `page` → 400 ProblemDetails in Development and Testing) and `Api/RequestBodyLimitTests.cs` (> limit → 413 ProblemDetails)
- [ ] T015 `ExceptionHandlers/BadRequestExceptionHandler.cs` (before Global), `Security/RequestBodyLimit.cs` (Kestrel + Content-Length guard) (R11, R12)
- [ ] T016 [P] Integration fixture `Integration/PostgresFixture.cs` (Testcontainers, `postgres:17.11-alpine3.24`, initdb UTF8/C.UTF-8, migrate once), trait `Category=Integration`
- [ ] T017 [P] Tests `Integration/DatabaseEncodingCheckTests.cs` (UTF8 passes; a `SQL_ASCII` database in the same container → throws with the encoding in the message)
- [ ] T018 `Infrastructure/Persistence/DatabaseEncodingCheck.cs`, called by `DatabaseInitializer` after migrations for Npgsql only (R7)

**Checkpoint**: model, migration, error mapping and encoding guard in place; all tests green.

---

## Phase 3: User Story 1 – Admin adds an artist (P1) 🎯 MVP

### Tests
- [ ] T019 [P] [US1] `Application/Artists/CreateArtistCommandValidatorTests.cs` (each rule; duplicate email ignoring case/spaces → conflict code; 200/201 code points)
- [ ] T020 [P] [US1] `Application/Artists/CreateArtistCommandHandlerTests.cs` (adds normalised artist, returns Created DTO, never saves)
- [ ] T021 [P] [US1] `Api/Artists/CreateArtistEndpointTests.cs` (201 + Location + body; 401 no token; 403 non-admin token; 400 per field; 409 `errors.email`; emoji/Arabic accepted)

### Implementation
- [ ] T022 [US1] `Features/Artists/{ArtistDto, Specifications/ArtistByNormalizedEmailSpec}.cs`, `Commands/CreateArtist/*`
- [ ] T023 [US1] `Endpoints/ArtistEndpoints.cs` POST (`.RequireAdmin()`, Produces 201/400/401/403/409/413); map in `Program.cs`
- [ ] T024 [US1] `ExceptionHandlers/UniqueConstraintExceptionHandler.cs` (23505 on `ix_artists_normalized_email` → 409 `errors.email`; other constraints → 409 without errors) + unit test with a fake `PostgresException`

---

## Phase 4: User Story 2 – Public paginated list (P1)

### Tests
- [ ] T025 [P] [US2] `Application/Artists/ListArtistsQueryValidatorTests.cs` (page ≥ 1, pageSize 1–50) and spec tests with `Evaluate` (order lower(name) → created → id; skip/take; email null when not admin)
- [ ] T026 [P] [US2] `Api/Artists/ListArtistsEndpointTests.cs` (anonymous: no `email` key; admin token: emails; defaults 20; page past end → empty items with totals; 400 on 0/51)

### Implementation
- [ ] T027 [US2] `Features/Artists/Specifications/{ArtistsPageSpec, ArtistsCountSpec}.cs`, `Queries/ListArtists/*`
- [ ] T028 [US2] `ArtistEndpoints` GET (`IncludeEmail` from `ICurrentUser` Admin role; `[AsParameters]` paging)

---

## Phase 5: User Story 3 – Arabic and other scripts kept exactly (P1)

- [ ] T029 [US3] `Integration/UnicodeRoundTripTests.cs`: Arabic, mixed Arabic/Latin, emoji and combining marks stored and read back ordinal-equal through repository + UoW + page spec; 200-code-point Arabic name fits the column
- [ ] T030 [P] [US3] `Integration/ArtistPersistenceTests.cs`: unique index rejects `A@x.com` vs `a@x.com` (23505 → handler shape), `lower(name)` ordering on Postgres, audit timestamps stored

---

## Phase 6: User Story 4 – List DSPs (P2)

- [ ] T031 [P] [US4] Tests `Api/Dsps/ListDspsEndpointTests.cs` (exactly 3, sorted by name, fixed ids, anonymous) + `Application/Dsps/ListDspsQueryHandlerTests.cs`
- [ ] T032 [US4] `Features/Dsps/{DspDto, Specifications/DspsSpec, Queries/ListDsps/*}`, `Endpoints/DspEndpoints.cs`

---

## Phase 7: Polish & cross-cutting

- [ ] T033 Export contract: `scripts/export-contract.ps1` → `specs/003-artists-and-dsps-management/contracts/openapi.json`; `OpenApiContractTests` cover the new paths; check 401/403/409/413 documented
- [ ] T034 CI: the whole suite (unit + `Category=Integration`) runs on `ubuntu-latest` (Docker) and shows in the TRX report; document the trait filter for running unit tests without Docker
- [ ] T035 README: Artists & DSPs endpoints, the public list hides emails (admins only), UTF-8 requirement, running tests with/without Docker (trait filter)
- [ ] T036 Quickstart run on compose (`down -v`, `up --build`): steps 1–9 incl. restart keeps 3 DSPs; insert 1,000 artists and time page 1 and the last page (< 1 s, SC-003); record results in ai-log
- [ ] T037 Add this phase's section to `docs/walkthrough.md` and update Memory.md (master + repo) (REQUIRED by the constitution's Definition of Done)
- [ ] T038 `docs/ai-log.md` Phase 2 (backend): AI usage, security notes (email PII, body cap), mistakes (malformed JSON → 500 in Development; audit timestamps never filled) with exact cause and fix; developer notes (provided by Yousef)

## Dependencies

- Setup → Foundational → US1 → US2 (shares ArtistDto/endpoints) → US3 (needs model + list spec) ; US4 after Foundational (independent of US1–US3).
- Tests before implementation in each story. T033 after all endpoints; frontend T001 needs T033.
