# Data Model: Track Catalog with Filters and Status (API service)

## Track (`tracks`)
| Field | Column | Type | Rules |
|---|---|---|---|
| Id | id | uuid PK | `Guid.CreateVersion7()` in code |
| Title | title | varchar(200) | required, trimmed, 1–200 code points, any script |
| ArtistId | artist_id | uuid FK → artists(id) ON DELETE RESTRICT | must exist; index `ix_tracks_artist_id` |
| Isrc | isrc | char(12) | normalised (R4); unique `ix_tracks_isrc`; check `ck_tracks_isrc_format` |
| Genre | genre | varchar(20) | one of `Genres.All`; check `ck_tracks_genre` |
| ReleaseDate | release_date | date | 1900-01-01..2100-12-31 |
| Status | status | varchar(12) | draft/submitted/distributed; check `ck_tracks_status`; index `ix_tracks_status` |
| CreatedAtUtc / UpdatedAtUtc | created_at_utc / updated_at_utc | timestamptz | interceptor (Phase 2) + factory |
| Version | xmin (system) | xid | concurrency token, not a real column |

Index `ix_tracks_release_date_id` on `(release_date DESC, id)`.

### State transitions
```
draft ──submit──▶ submitted ──distribute──▶ distributed (final)
  ▲                   │
  └────withdraw───────┘
```
Same status → 409; any other pair → 409; unknown value → 400.

## Genres (fixed)
Arabic Pop, Mahraganat, Khaleeji, Hip-Hop, Electronic, Rap, R&B, Rock.

## DTOs
- `TrackListItemDto { id, title, artistId, artistName, isrc ("EG-A1B-26-00001"), genre, status, releaseDate ("2026-12-01") }`
- `TrackDetailDto` = list item + `createdAtUtc`, `updatedAtUtc?`, `allowedNextStatuses: status[]`, `distributions: TrackDistributionDto[]` (empty)
- `TrackDistributionDto { dspId, dspName, status, updatedAtUtc }` (Phase 4)
- `PagedResult<TrackListItemDto>` (Phase 2 shape)

## Requests
- `CreateTrackRequest { title, artistId, isrc, genre, releaseDate }` (all strings/GUID, all required)
- `ChangeTrackStatusRequest { status }`
- List query: `artistId?`, `genre?`, `status?`, `page?`, `pageSize?`
- Artists: `GET /api/artists?search=&page=&pageSize=`, `GET /api/artists/{id}`

## Relationships
Artist 1 ─── * Track (restrict delete). Track 1 ─── * Distribution arrives in Phase 4.
