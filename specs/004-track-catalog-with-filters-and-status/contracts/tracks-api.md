# Contract intent: Tracks (+ artist lookup)

Exported OpenAPI (`openapi.json`) is produced at implementation from the running app.

## POST /api/tracks (Admin)
Body `{ "title": "تملي معاك", "artistId": "…", "isrc": "eg-a1b-26-00001", "genre": "Arabic Pop", "releaseDate": "2026-12-01" }`
- 201 + `Location: /api/tracks/{id}` + `TrackDetailDto` (status `draft`, isrc `EG-A1B-26-00001`)
- 400 ValidationProblem `errors.{title|artistId|isrc|genre|releaseDate}`
- 409 ValidationProblem `errors.isrc` ("Another track already uses this ISRC.") – also on races
- 401 / 403 / 413

## GET /api/tracks?artistId=&genre=&status=&page=1&pageSize=20 (public)
- 200 `PagedResult<TrackListItemDto>` ordered release date desc, title, id
- 400 for unknown genre/status, malformed artistId, invalid paging

## GET /api/tracks/{id} (public)
- 200 `TrackDetailDto` (`allowedNextStatuses`, `distributions: []`); 404 ProblemDetails

## PATCH /api/tracks/{id}/status (Admin)
Body `{ "status": "submitted" }`
- 200 `TrackDetailDto`
- 400 `errors.status` (missing/unknown value)
- 404 unknown track
- 409 ProblemDetails (same status, forbidden change, or changed concurrently – `detail` explains)
- 401 / 403

## GET /api/artists?search=amr (public, Phase 2 list + search)
## GET /api/artists/{id} (public; email only for admins) → 200 `ArtistDto` / 404
