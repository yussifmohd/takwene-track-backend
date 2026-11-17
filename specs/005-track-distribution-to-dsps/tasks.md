---
description: "Task list for Phase 4 – Track Distribution to DSPs (API service)"
---

# Tasks: Track Distribution to DSPs (API service)

**Input**: Design documents from `specs/005-track-distribution-to-dsps/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/distribution-api.md, quickstart.md
**Tests**: REQUIRED by the constitution (Principle III).
**Skills**: dotnet-clean-architecture (all code), git-workflow, decisions-log

## Format: `[ID] [P?] [Story] Description` — paths relative to the backend repo root.

---

## Phase 1: Setup

- [ ] T001 Confirm no new packages are needed; wire names (`pending`/`live`/`rejected`, `dspIds`, `answeredAtUtc`, `allowedAnswers`, `canDistribute`) agreed with the frontend data model

---

## Phase 2: Foundational (blocks all stories)

- [ ] T002 [P] Tests `Domain/Tracks/DistributionStatusRulesTests.cs`: full table (pending → live/rejected allowed; same status; live final; rejected "send again") with outcome + message; `AllowedAnswers` per status (R6)
- [ ] T003 [P] Tests `Domain/Tracks/TrackStatusRulesTests.cs` updated: submitted → distributed refused without deliveries (message R4), allowed with; `AllowedNext(submitted, false)` = [draft]; all Phase 3 cases unchanged
- [ ] T004 `Domain/Tracks/{DistributionStatus, DistributionStatusRules, TrackDistribution}.cs`; `TrackStatusRules` with `hasDistributions`; `Track.Distributions` (backing list); `ChangeStatus` passes `Distributions.Count > 0`
- [ ] T005 `TrackDistributionConfiguration` (table, PK `pk_track_distributions`, FKs, status converter + check, answered check, dsp index); `TrackConfiguration` navigation via field; migration `AddTrackDistributions` incl. the Q1 data fix (R9); review the generated SQL
- [ ] T006 [P] Test `UniqueConstraintExceptionHandlerTests`: `pk_track_distributions` → 409 `errors.dspIds` "This platform was just added by someone else. Reload and try again."; implement in `KnownIndexes`

---

## Phase 3: User Story 1 – Distribute (P1)

- [ ] T007 [P] [US1] Tests `Domain/Tracks/TrackDistributeTests.cs`: draft → NotAllowed; submitted → deliveries pending + status distributed + stamped; distributed: pending/live unchanged, new added, rejected re-sent (new submitted time, answer cleared); duplicates in input count once; no-op → not changed and not stamped
- [ ] T008 [US1] `Track.Distribute(IReadOnlyCollection<Guid> dspIds, DateTime now)` → `DistributeResult` (NotAllowed + message / Changed / Unchanged)
- [ ] T009 [P] [US1] Tests `Application/Tracks/DistributeTrackTests.cs`: validator (required, 1–10 entries, 1–3 distinct, messages on `dspIds`); handler: unknown track 404, unknown DSP → Invalid `dspIds` "Unknown platform: {id}.", draft → Conflict, success → detail with DSP names ordered by name; no-op → Success without changes
- [ ] T010 [US1] `Commands/DistributeTrack/{Command, Validator, Handler}`; `TrackForUpdateSpec` includes `Distributions`; `TrackDetailDto.From` maps deliveries with names from the handler's DSP list
- [ ] T011 [P] [US1] Tests `Api/Tracks/DistributeTrackEndpointTests.cs`: 401/403, 404 (unknown / non-GUID id), 400 shapes, 409 draft, 200 body (status, deliveries, `allowedNextStatuses`), second call no-op, rejected re-send
- [ ] T012 [US1] Endpoint `POST /api/tracks/{id:guid}/distribute` (`RequireAdmin`, `DistributeTrackRequest`)

---

## Phase 4: User Story 2 – Status rule (P1)

- [ ] T013 [P] [US2] Tests: `ChangeTrackStatusTests` + `ChangeTrackStatusEndpointTests` – submitted → distributed 409 with the R4 message; other transitions unchanged; detail `allowedNextStatuses` for submitted = `["draft"]`
- [ ] T014 [US2] Wire `hasDistributions` through `ChangeStatus` and the detail DTO (`AllowedNext`); update Phase 3 tests that walked submitted → distributed by PATCH to use Distribute

---

## Phase 5: User Story 3 – Platform answers (P1)

- [ ] T015 [P] [US3] Tests `Domain/Tracks/TrackRecordAnswerTests.cs`: pending → live/rejected (answer time, track stamped, status unchanged); refusals per table; no delivery → NotFound
- [ ] T016 [US3] `Track.RecordAnswer(dspId, DistributionStatus answer, DateTime now)` → outcome
- [ ] T017 [P] [US3] Tests `Application/Tracks/RecordDistributionAnswerTests.cs` (validator: required, only live/rejected → `errors.status` "Status must be live or rejected."; handler: 404 track, 404 delivery "This track has not been sent to this platform.", 409 messages, 200 detail) and `Api/Tracks/RecordDistributionAnswerEndpointTests.cs` (auth, 400, 404, 409, 200)
- [ ] T018 [US3] `Commands/RecordDistributionAnswer/{Command, Validator, Handler}`; endpoint `PATCH /api/tracks/{id:guid}/distributions/{dspId:guid}`; OpenAPI enum `live|rejected` on the request only

---

## Phase 6: User Story 4 – Detail (P1)

- [ ] T019 [P] [US4] Tests `ListAndGetTrackEndpointTests` updated: anonymous detail lists deliveries (name, status, times, `allowedAnswers`) ordered by name, `canDistribute` per status; empty list for new tracks
- [ ] T020 [US4] `TrackDistributionDto` reshaped, `TrackDetailDto.CanDistribute`, `TrackDetailSpec` projection of deliveries (join `dsps`, order by name)

---

## Phase 7: Integration (real PostgreSQL, `Category=Integration`)

- [ ] T021 [P] `Integration/DistributionPersistenceTests.cs`: round-trip with names and times; check constraints (status, answered vs pending) refuse bad rows; PK refuses a duplicate; detail projection order on real SQL
- [ ] T022 [P] `Integration/DistributionConcurrencyTests.cs`: parallel distribute of the same DSP → one row, losers 409; distribute vs withdraw on a submitted track → exactly one wins, never distributed without deliveries
- [ ] T023 [P] `Integration/DistributionMigrationTests.cs`: migrate to `AddTracks`, insert a distributed track, migrate to latest → submitted with `updated_at_utc` set; other statuses untouched

---

## Phase 8: Polish

- [ ] T024 Export the contract (`scripts/export-contract.ps1 -Feature 005-track-distribution-to-dsps`); contract test green
- [ ] T025 `dotnet build` (warnings as errors) and all tests (incl. Integration) green 2×
- [ ] T026 Quickstart on compose (`down -v`, Phase 3 data first for step 1); record results
- [ ] T027 README (distribution section, curl examples, status rule change), `docs/walkthrough.md` Phase 4, `Memory.md` (repo + master)
- [ ] T028 `docs/ai-log.md` Phase 4 (backend): generated vs written, security, mistakes, quickstart results, developer-notes placeholder

## Dependencies

Setup → Foundational (T002–T006) → US1 (T007–T012) → US2 (T013–T014) → US3 (T015–T018) → US4 (T019–T020)
→ Integration (T021–T023) → Polish. Within a story, tests first ([P] tests can be written together).
