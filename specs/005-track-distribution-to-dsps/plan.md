# Implementation Plan: Track Distribution to DSPs (API service)

**Branch**: `005-track-distribution-to-dsps` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-track-distribution-to-dsps/spec.md`

**Skills applied**: dotnet-clean-architecture, git-workflow, decisions-log

## Summary

Add `TrackDistribution` (a delivery: track, DSP, status pending/live/rejected, submitted time, answer
time) as part of the `Track` aggregate, with migration `AddTrackDistributions` (table, keys, checks; no
data changes – Q1). New endpoints: `POST /api/tracks/{id}/distribute` (Admin, one or more DSP ids, at
most 20 distinct) and
`PATCH /api/tracks/{id}/distributions/{dspId}` (Admin, simulated answer live/rejected). The status
change to distributed now needs at least one delivery (409 otherwise); `allowedNextStatuses` follows.
`GET /api/tracks/{id}` returns the deliveries (DSP name, status, submittedAt, answeredAt, allowed
answers) and `canDistribute`. Every delivery change stamps the track, so the existing `xmin` token
serialises concurrent changes to one track.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.403, runtime 10.0.14)

**Primary Dependencies**: as Phase 3. **No new packages.**

**Storage**: PostgreSQL 17; new table `track_distributions` (PK `(track_id, dsp_id)`, FK `track_id` →
`tracks` cascade, FK `dsp_id` → `dsps` restrict).

**Testing**: xUnit v3 + NSubstitute + Shouldly; API tests on EF InMemory; `Integration/` on
Testcontainers PostgreSQL (parallel distribute, distribute vs status change race, answer vs re-send
race, check constraints, detail projection ordering and timing).

**Target Platform**: Linux containers (compose); AWS later.

**Project Type**: web-service (API)

**Performance Goals**: detail with 3 deliveries < 1 s with 1,000 tracks (SC-005): one projection query
with a join to `dsps` (`AsNoTracking`, split for the collection).

**Constraints**: Admin-only writes; no 500 for client mistakes; rules only in the domain.

**Scale/Scope**: 1 entity, 2 new endpoints, 1 changed rule, 1 schema migration.

## Constitution Check

| Principle | Gate | Status |
|---|---|---|
| I. Clean Architecture & CQRS | `DistributeTrackCommand`, `RecordDistributionAnswerCommand` (ICommand); rules in Domain (`Track.Distribute`, `Track.RecordAnswer`, `DistributionStatusRules`, `TrackStatusRules`); UoW commits | ✅ |
| II. Thin HTTP edge | `TrackEndpoints` binds and maps Results only | ✅ |
| III. Tests ship with features | domain tables, validators, handlers, API (auth, 400/404/409), integration on real Postgres | ✅ |
| IV. Security by default | `.RequireAdmin()` on both writes; DSP ids checked against the stored list; body cap unchanged | ✅ |
| V. Spec-driven | spec → clarify → plan → tasks → analyze; contract exported; walkthrough + Memory + ai-log | ✅ |
| VI. Simplicity & licenses | no packages; composite key instead of id + unique index; status as constrained text | ✅ |

Post-design re-check: ✅ (Complexity Tracking below).

## Project Structure

### Documentation (this feature)

```text
specs/005-track-distribution-to-dsps/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/distribution-api.md    # reviewed intent; openapi.json exported at implementation
├── checklists/requirements.md
└── tasks.md
```

### Source Code (additions/changes)

```text
src/TrackManagement.Domain/Tracks/
  TrackDistribution.cs               # child entity: TrackId, DspId, Dsp nav, Status, SubmittedAtUtc, AnsweredAtUtc
  DistributionStatus.cs              # enum Pending, Live, Rejected + wire names
  DistributionStatusRules.cs         # pending → live | rejected; Check(from, to) → outcome + message
  Track.cs                           # Distributions; Distribute(dspIds, now); RecordAnswer(dspId, answer, now); ChangeStatus uses hasDistributions
  TrackStatusRules.cs                # Check/AllowedNext gain hasDistributions
src/TrackManagement.Application/Features/Tracks/
  TrackDtos.cs                       # TrackDistributionDto reshaped; TrackDetailDto + CanDistribute
  Specifications/TrackSpecs.cs       # TrackForUpdateSpec includes Distributions; TrackDetailSpec projects them
  Commands/DistributeTrack/{Command, Validator, Handler}
  Commands/RecordDistributionAnswer/{Command, Validator, Handler}
  Features/Dsps/Specifications/      # (existing list spec reused by the handler)
src/TrackManagement.Infrastructure/Persistence/
  Configurations/TrackDistributionConfiguration.cs; TrackConfiguration (navigation, backing field)
  Migrations/<ts>_AddTrackDistributions.cs   # table + keys + checks (no data changes)
src/TrackManagement.Api/
  Endpoints/TrackEndpoints.cs        # + distribute, + distributions/{dspId}
  ExceptionHandlers/UniqueConstraintExceptionHandler.cs  # + pk_track_distributions → errors.dspIds
  OpenApi/FixedValuesSchemaTransformer.cs                # answer status enum live|rejected on the request
tests/TrackManagement.UnitTests/
  Domain/Tracks/{DistributionStatusRulesTests, TrackDistributeTests, TrackRecordAnswerTests}.cs; TrackStatusRulesTests/TrackTests updated
  Application/Tracks/{DistributeTrackTests, RecordDistributionAnswerTests}.cs; ChangeTrackStatusTests updated
  Api/Tracks/{DistributeTrackEndpointTests, RecordDistributionAnswerEndpointTests}.cs; detail + status tests updated
  Integration/{DistributionPersistenceTests, DistributionConcurrencyTests, DistributionMigrationTests}.cs
```

**Structure Decision**: unchanged layout; distributions live in `Domain/Tracks` (part of the aggregate).

## Key design decisions (details in research.md)

1. **Deliveries belong to the Track aggregate** (R1): `Track.Distributions` (read-only view over a
   backing list); only `Track.Distribute` and `Track.RecordAnswer` change them. No repository for
   `TrackDistribution`.
2. **Composite key `(track_id, dsp_id)`** (R2) enforces "one delivery per platform" in the database;
   a lost insert race → unique-constraint handler (`pk_track_distributions` → 409 `errors.dspIds`).
3. **Every delivery change stamps the track** (R3): `UpdatedAtUtc` on the track is set, so the
   `tracks` row is updated with the `xmin` check; two concurrent changes to one track (distribute,
   answers, status change) → one 409 "This record was changed by someone else…". A no-op distribute
   changes nothing and saves nothing.
4. **Status rule with deliveries** (R4): `TrackStatusRules.Check(from, to, hasDistributions)` refuses
   submitted → distributed without deliveries: "A track becomes distributed when it is sent to at
   least one platform. Use Distribute to send it." `AllowedNext(from, hasDistributions)` drops
   distributed accordingly. `Track.Distribute` adds deliveries first, then changes status through the
   same rule.
5. **Distribute** (R5): body `{ "dspIds": [guid, …] }`; validator: required, not empty, at most 20 distinct ids (repeats count once)
   (`errors.dspIds`); handler loads all DSPs once, unknown ids → `Result.Invalid` on `dspIds`
   ("Unknown platform: {id}."). Draft → 409 "A draft track must be submitted before it can be
   distributed." Response 200 + `TrackDetailDto` (also for a no-op) (Q3).
6. **Answers** (R6): `PATCH /api/tracks/{id}/distributions/{dspId}` body `{ "status": "live" |
   "rejected" }`; bound as a string (validator 400 names `status`, "pending" refused there).
   `DistributionStatusRules`: pending → live | rejected only (Q2); messages "The delivery is already
   live.", "A live delivery is final.", "The delivery is already rejected.", "A rejected delivery must
   be sent again with Distribute before the platform can answer."
   Unknown track / no delivery for that DSP → 404. Response 200 + `TrackDetailDto`.
7. **Times** (R7, Q4): `submitted_at_utc` (set on create and on re-send), `answered_at_utc` (set on
   live/rejected, cleared on re-send); check `(status = 'pending') = (answered_at_utc IS NULL)`.
8. **Detail** (R8): `TrackDistributionDto(DspId, DspName, Status, SubmittedAtUtc, AnsweredAtUtc?,
   AllowedAnswers)` ordered by DSP name; `TrackDetailDto.CanDistribute` (submitted or distributed).
   The Phase 3 placeholder shape (`status` string, `updatedAtUtc`) is replaced – the web app is the
   only client and regenerates its types.
9. **Pre-Phase-4 data** (R9, Q1): no data migration; README and Memory.md tell developers to reset
   local databases created before this phase (`docker compose down -v`); Phase 5 seeds consistent data.
10. **Answers do not touch the track status** (spec): distributed stays final.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Track stamped on every delivery change | FR-009: concurrent changes to one track must not both win | A separate token per delivery misses races between distribute and status change |
| Answer status bound as string | FR-007: "pending"/unknown must be 400 naming the field | Enum binding fails before validation with a generic body error |
