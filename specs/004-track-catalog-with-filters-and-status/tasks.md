---
description: "Task list for Phase 3 – Track Catalog with Filters and Status (API service)"
---

# Tasks: Track Catalog with Filters and Status (API service)

**Input**: Design documents from `specs/004-track-catalog-with-filters-and-status/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/tracks-api.md, quickstart.md
**Tests**: REQUIRED by the constitution (Principle III).
**Skills**: dotnet-clean-architecture (all code), git-workflow, decisions-log

## Format: `[ID] [P?] [Story] Description` — paths relative to the backend repo root.

---

## Phase 1: Setup

- [ ] T001 Confirm no new packages are needed (plan); `Genres`/`TrackStatus` wire names agreed with the frontend data model

---

## Phase 2: Foundational (blocks all stories)

- [ ] T002 [P] Tests `Domain/Tracks/IsrcTests.cs`: valid with/without hyphens and any case → 12 upper chars; `Format` hyphenates; refused: 11/13 chars, partial hyphens, wrong positions, `_`/`.`/inner spaces, letters in year/designation, digit in country part; outer spaces trimmed (R4)
- [ ] T003 [P] Tests `Domain/Tracks/TrackStatusRulesTests.cs`: full 3×3 table (3 allowed, 3 same-status, 3 forbidden incl. everything from distributed) with outcome + message; `AllowedNext` per status (R1)
- [ ] T004 [P] Tests `Domain/Tracks/GenresTests.cs` (exactly the 8 names, exact match only: "r&b", "HipHop", "Pop" refused) and `Domain/Tracks/TrackTests.cs` (Create → draft, trimmed title, normalised ISRC, guards; `ChangeStatus` changes only on allowed moves)
- [ ] T005 `Domain/Tracks/{Isrc, TrackStatus, TrackStatusRules, Genres, Track}.cs` (`Track : IAuditableEntity`, `Artist` navigation, `uint Version`)
- [ ] T006 `Infrastructure/Persistence/Configurations/TrackConfiguration.cs` (lengths, `char(12)`, status converter, check constraints, indexes, FK restrict, `Version` `IsRowVersion()`); `DbSet<Track>`; migration `AddTracks` (review SQL: checks, indexes, no `xmin` column added)
- [ ] T007 [P] Tests `Api/ConcurrencyExceptionHandlerTests.cs` (`DbUpdateConcurrencyException` → 409 ProblemDetails with the generic "This record was changed by someone else…" message) and `UniqueConstraintExceptionHandler` case for `ix_tracks_isrc` → 409 `errors.isrc`
- [ ] T008 `ExceptionHandlers/ConcurrencyExceptionHandler.cs` (registered before Global); `ix_tracks_isrc` in `KnownIndexes` (R3, R4)
- [ ] T009 [P] Integration `Integration/TrackPersistenceTests.cs`: Arabic/mixed title round-trip; check constraints reject a bad genre/status/isrc inserted by SQL; FK restrict; unique ISRC under 10 parallel inserts → 1 row
- [ ] T010 [P] Integration `Integration/TrackConcurrencyTests.cs`: two contexts change one submitted track (withdraw vs distribute) → one `DbUpdateConcurrencyException`; final state is one of the two, never "draft after distributed"

**Checkpoint**: model, migration and error mapping in place; all tests green.

---

## Phase 3: User Story 1 – Admin adds a track (P1) 🎯 MVP

### Tests
- [ ] T011 [P] [US1] `Application/Tracks/CreateTrackCommandValidatorTests.cs` (each field rule and message; unknown artist; duplicate ISRC in another form → conflict code; 200/201 code points; dates 2026-02-30, 1899-12-31, 2101-01-01, "01/12/2026" refused; future allowed)
- [ ] T012 [P] [US1] `Application/Tracks/CreateTrackCommandHandlerTests.cs` (adds draft with normalised values, returns Created detail with artist name and `allowedNextStatuses ["submitted"]`, never saves)
- [ ] T013 [P] [US1] `Api/Tracks/CreateTrackEndpointTests.cs` (201 + Location + body; 401; 403 non-admin; 400 per field; 409 `errors.isrc`; Arabic title)

### Implementation
- [ ] T014 [US1] `Features/Tracks/{TrackListItemDto, TrackDetailDto, TrackDistributionDto}.cs`, `Specifications/{TrackByIsrcSpec}`, `Features/Artists/Specifications/ArtistByIdSpec`, `Commands/CreateTrack/*`
- [ ] T015 [US1] `Endpoints/TrackEndpoints.cs` POST (`.RequireAdmin()`, Produces 201/400/401/403/409/413); map in `MapApiEndpoints`

---

## Phase 4: User Story 2 – Browse and filter (P1)

### Tests
- [ ] T016 [P] [US2] `Application/Tracks/ListTracksQueryValidatorTests.cs` (unknown genre/status, paging) and `TracksPageSpecTests.cs` (each filter, combination, order release date desc → title ignoring case → id, totals match items' filter) on InMemory
- [ ] T017 [P] [US2] `Api/Tracks/ListTracksEndpointTests.cs` (45 tracks → 20/45/3; filters; unknown artist → empty; `status=published` 400 `errors.status`; `artistId=nope` 400; page past the end → empty items with correct totals; anonymous allowed)

### Implementation
- [ ] T018 [US2] `Specifications/{TrackFilter, TracksPageSpec, TracksCountSpec}.cs` (shared `ApplyFilter`), `Queries/ListTracks/*`, GET `/api/tracks` with `[AsParameters]` request
- [ ] T019 [P] [US2] Integration: filters + ordering + `Isrc.Format` projection on PostgreSQL (`TrackPersistenceTests`)

---

## Phase 5: User Story 3 – Open a track (P1)

- [ ] T020 [P] [US3] Tests `Api/Tracks/GetTrackEndpointTests.cs` (200 full detail with `distributions: []` and allowed next statuses; unknown id 404; non-GUID 404; anonymous allowed)
- [ ] T021 [US3] `Specifications/TrackDetailSpec.cs`, `Queries/GetTrack/*`, GET `/api/tracks/{id:guid}`

---

## Phase 6: User Story 4 – Status changes (P1)

### Tests
- [ ] T022 [P] [US4] `Application/Tracks/ChangeTrackStatusCommandValidatorTests.cs` (missing/unknown → message listing values) and `…HandlerTests.cs` (not found → NotFound; same/forbidden → Conflict with the domain message; allowed → updated detail; never saves)
- [ ] T023 [P] [US4] `Api/Tracks/ChangeTrackStatusEndpointTests.cs` (full walk draft→submitted→draft→submitted→distributed; distributed→draft 409; draft→distributed 409; same 409; "published" 400; 404; 401; 403; `updatedAtUtc` set)

### Implementation
- [ ] T024 [US4] `Specifications/TrackByIdSpec` (tracked, includes Artist), `Commands/ChangeTrackStatus/*`, PATCH `/api/tracks/{id:guid}/status` (`.RequireAdmin()`, Produces 200/400/401/403/404/409)

---

## Phase 7: User Story 5 – Find artists by name (P2)

- [ ] T025 [P] [US5] Tests: `ListArtistsQueryValidatorTests` (search > 100 → 400), `ArtistsPageSpecTests` (search "amr" / "عمرو" / "AMR", `%` and `_` matched literally, count matches), `Api/Artists/GetArtistEndpointTests.cs` (200; email only with admin token; 404)
- [ ] T026 [US5] `ListArtistsQuery.Search` + filter on page/count specs; `Specifications/ArtistDetailSpec`, `Queries/GetArtist/*`, GET `/api/artists/{id:guid}`; Phase 2 `Location` now resolves (test)

---

## Phase 8: Polish & cross-cutting

- [ ] T027 OpenAPI: enum lists for `genre`/`status` (schemas + query parameters) via a schema transformer; export contract → `specs/004-…/contracts/openapi.json`; `OpenApiContractTests` cover the new paths and enums
- [ ] T028 README: tracks endpoints, ISRC rules, status rules table, filters, artist search; trait filter unchanged
- [ ] T029 Quickstart on compose (`down -v`, `up --build`): steps 1–10 incl. 20 parallel duplicate creates, parallel PATCHes, 1,000 tracks timing (SC-004); results in ai-log
- [ ] T030 `docs/walkthrough.md` Phase 3 section; Memory.md (master + repo)
- [ ] T031 `docs/ai-log.md` Phase 3 (backend): AI usage, security notes, mistakes with cause and fix; developer notes only as provided by Yousef

## Dependencies

- Setup → Foundational → US1 → US2/US3 (share DTOs) → US4 (needs detail DTO) ; US5 after Foundational (independent).
- Tests before implementation in each story. T027 after all endpoints; frontend T001 needs T027.
