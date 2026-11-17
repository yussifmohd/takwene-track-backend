# Implementation Plan: Track Catalog with Filters and Status (API service)

**Branch**: `004-track-catalog-with-filters-and-status` | **Date**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-track-catalog-with-filters-and-status/spec.md`

**Skills applied**: dotnet-clean-architecture, git-workflow, decisions-log

## Summary

Add the `Track` aggregate (title, artist, ISRC, genre, release date, status) with migration
`AddTracks`, `POST /api/tracks` (Admin), `GET /api/tracks` (public, paged, filters artistId/genre/
status, projected with the artist name), `GET /api/tracks/{id}` (public, allowed next statuses and an
empty distributions list) and `PATCH /api/tracks/{id}/status` (Admin). Status rules live in the
domain (`TrackStatusRules`, `Track.ChangeStatus`); concurrent changes are caught by PostgreSQL's
`xmin` as a concurrency token. ISRC uniqueness: validator pre-check + unique index (races → the
existing unique-constraint handler, now also for `isrc`). For the web app's artist picker (Q3):
`GET /api/artists?search=` and `GET /api/artists/{id}` (which also makes Phase 2's `Location` header real).

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.403, runtime 10.0.14)

**Primary Dependencies**: as Phase 2. **No new packages.**

**Storage**: PostgreSQL 17; new table `tracks` (FK `artist_id` → `artists`, restrict delete).

**Testing**: xUnit v3 + NSubstitute + Shouldly; API tests on EF InMemory (`TestApiFactory`);
`Integration/` on Testcontainers PostgreSQL (unique ISRC race, `xmin` concurrency, filters/ordering
SQL, Arabic title round-trip, check constraints).

**Target Platform**: Linux containers (compose); AWS later.

**Project Type**: web-service (API)

**Performance Goals**: any filtered page < 1 s with 1,000 tracks (SC-004): one COUNT + one paged
projection with a join to `artists`, `AsNoTracking`; indexes on `artist_id`, `status`, and
`(release_date DESC, id)`.

**Constraints**: Admin-only writes; no 500 for client mistakes; status rules only in the domain.

**Scale/Scope**: 1 entity, 4 track endpoints + 2 artist additions, 1 migration.

## Constitution Check

| Principle | Gate | Status |
|---|---|---|
| I. Clean Architecture & CQRS | `CreateTrackCommand`, `ChangeTrackStatusCommand` (ICommand); `ListTracksQuery`, `GetTrackQuery`, `GetArtistQuery` (IQuery); rules in Domain (no packages); specs + repositories; UoW commits | ✅ |
| II. Thin HTTP edge | `TrackEndpoints` only binds and maps Results; no status logic in endpoints | ✅ |
| III. Tests ship with features | domain (ISRC, transitions table), validators per rule, handlers, specs, API (auth, filters, 400/404/409), integration on real Postgres | ✅ |
| IV. Security by default | `.RequireAdmin()` on POST/PATCH; artist email still admin-only on the new GET by id; body cap unchanged; malformed → 400 | ✅ |
| V. Spec-driven | spec → clarify → plan → tasks → analyze; contract exported to `contracts/openapi.json`; walkthrough + Memory in the PR | ✅ |
| VI. Simplicity & licenses | no new packages; genre and status as constrained text (no lookup tables) | ✅ |

Post-design re-check: ✅ (Complexity Tracking below).

## Project Structure

### Documentation (this feature)

```text
specs/004-track-catalog-with-filters-and-status/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/tracks-api.md          # reviewed intent; openapi.json exported at implementation
├── checklists/requirements.md
└── tasks.md
```

### Source Code (additions/changes)

```text
src/TrackManagement.Domain/
  Tracks/Track.cs                    # Create(...) → draft; ChangeStatus(target) → StatusChange outcome
  Tracks/TrackStatus.cs              # enum Draft, Submitted, Distributed + wire names "draft"/…
  Tracks/TrackStatusRules.cs         # the transition table, AllowedNext(from), Check(from, to)
  Tracks/Isrc.cs                     # TryNormalize(input, out 12 chars), Format(12 → CC-XXX-YY-NNNNN)
  Tracks/Genres.cs                   # the 8 names, IsKnown (exact, case-sensitive)
src/TrackManagement.Application/
  Features/Tracks/{TrackListItemDto, TrackDetailDto, TrackDistributionDto}.cs
  Features/Tracks/Specifications/{TrackFilter, TracksPageSpec, TracksCountSpec, TrackDetailSpec, TrackByIdSpec, TrackByIsrcSpec}.cs
  Features/Tracks/Commands/CreateTrack/{Command, Validator, Handler}.cs
  Features/Tracks/Commands/ChangeTrackStatus/{Command, Validator, Handler}.cs
  Features/Tracks/Queries/ListTracks/{Query, Validator, Handler}.cs
  Features/Tracks/Queries/GetTrack/{Query, Handler}.cs
  Features/Artists/Queries/GetArtist/{Query, Handler}.cs; ListArtists gains Search; Specifications/{ArtistByIdSpec, ArtistDetailSpec}
src/TrackManagement.Infrastructure/
  Persistence/Configurations/TrackConfiguration.cs   # char(12) isrc unique, checks on genre/status/isrc, xmin token
  Persistence/Migrations/<ts>_AddTracks.cs
src/TrackManagement.Api/
  Endpoints/TrackEndpoints.cs; ArtistEndpoints (search, GET by id)
  ExceptionHandlers/ConcurrencyExceptionHandler.cs   # DbUpdateConcurrencyException → 409
  ExceptionHandlers/UniqueConstraintExceptionHandler.cs  # + ix_tracks_isrc → errors.isrc
  OpenApi/ (transformer) enum lists for genre and status in the contract
tests/TrackManagement.UnitTests/
  Domain/Tracks/{IsrcTests, TrackStatusRulesTests, TrackTests, GenresTests}.cs
  Application/Tracks/…, Application/Artists/(search, get)
  Api/Tracks/{CreateTrackEndpointTests, ListTracksEndpointTests, GetTrackEndpointTests, ChangeTrackStatusEndpointTests}.cs
  Api/Artists/{GetArtistEndpointTests, ListArtistsSearchTests}.cs
  Integration/{TrackPersistenceTests, TrackConcurrencyTests}.cs
```

**Structure Decision**: unchanged layout; new `Domain/Tracks` and `Features/Tracks` slices.

## Key design decisions (details in research.md)

1. **Status rules in the domain** (R1): `TrackStatusRules` holds the table
   `{draft→submitted, submitted→draft, submitted→distributed}`. `Track.ChangeStatus(target)` returns a
   `StatusChangeResult` (Changed / AlreadyInStatus / NotAllowed + message) instead of throwing; the
   handler maps non-Changed to `Result.Conflict` (409). Domain stays package-free.
2. **Unknown status → 400** (R2): status (and genre, release date) are bound as strings, so the
   validator names the field (`errors.status`); typed binding would fail as a generic malformed body.
3. **Concurrency** (R3): `xmin` system column as concurrency token (Npgsql `IsRowVersion()` on a
   `uint Version`). A lost race → `DbUpdateConcurrencyException` → `ConcurrencyExceptionHandler` → 409
   "This track was changed by someone else. Reload it and try again." Combined with the domain check
   on the freshly loaded row, a distributed track can never go back.
4. **ISRC** (R4): `Isrc.TryNormalize` accepts `^[A-Z]{2}[A-Z0-9]{3}\d{7}$` after trim + upper case,
   either with all three hyphens at positions 2/6/9 or none; stored `char(12)`; unique index
   `ix_tracks_isrc`; DB check `^[A-Z]{2}[A-Z0-9]{3}[0-9]{7}$`. Output always `Isrc.Format`.
   Duplicate → validator conflict (`errors.isrc`, 409); race → unique handler (same body).
5. **Genre / status storage** (R5): text columns with check constraints (genre = the 8 names; status
   ∈ draft/submitted/distributed), `TrackStatus` via a value converter to the lowercase wire name.
   The OpenAPI document lists both as `enum`, so the web app gets literal union types.
6. **Release date** (R6): `DateOnly` → PostgreSQL `date`; the request carries `"YYYY-MM-DD"`, parsed
   exactly (invalid/impossible → `errors.releaseDate`), range 1900-01-01..2100-12-31.
7. **Artist must exist** (R7): validator checks `ArtistByIdSpec` → 400 `errors.artistId` "Choose an
   existing artist."; the FK (restrict) is the backstop.
8. **List** (R8): `TrackFilter(ArtistId?, Genre?, Status?)` applied by one shared spec-builder
   extension to count and page specs; order `release_date DESC, lower(title), id`; projection includes
   `Artist.Name` (join) and `Isrc.Format` (client-side in the final projection). Unknown genre/status
   filter → 400; malformed `artistId` → 400 (binding); unknown artist → empty page.
9. **Detail** (R9): `TrackDetailDto` = list fields + `createdAtUtc`, `updatedAtUtc`,
   `allowedNextStatuses` (from `TrackStatusRules`), `distributions: []` (`TrackDistributionDto` shape
   reserved for Phase 4: dspId, dspName, status, updatedAtUtc). `{id:guid}` route constraint → a
   non-GUID id is 404.
10. **Responses** (R10): POST → 201 + `Location: /api/tracks/{id}` + `TrackDetailDto`; PATCH → 200 +
    the updated `TrackDetailDto` (the web app updates its cache without a refetch).
11. **Artist search / by id** (R11): `GET /api/artists?search=` (trim, ≤ 100 chars, case-insensitive
    contains on the name, translated with escaping by EF), `GET /api/artists/{id:guid}` (email only
    for admins, as in the list).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Genre, status and release date bound as strings | FR-013/US4-5: unknown values must be 400 with the field named | Enum/DateOnly binding fails before validation with a generic body error |
| `xmin` concurrency token | FR-008/SC-005: two admins must not both win | A domain check alone races between read and write |
| Distribution DTO shape defined before Phase 4 | the detail contract must contain the (empty) list now | An untyped array gives the web app no type |
