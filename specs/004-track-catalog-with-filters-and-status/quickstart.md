# Quickstart: Tracks (manual validation)

1. `docker compose up --build -d` (fresh: `down -v` first); sign in via `POST /api/auth/login` → `$T`.
2. Pick an artist id from `GET /api/artists?search=amr`.
3. `POST /api/tracks` (Bearer `$T`) with `isrc: "eg-a1b-26-00001"` → 201, isrc `EG-A1B-26-00001`, status `draft`,
   `allowedNextStatuses: ["submitted"]`, `distributions: []`; `GET` the `Location` → same body without a token.
4. Same ISRC as `EGA1B2600001` → 409 `errors.isrc`. 20 parallel creates with one new ISRC → 1×201, 19×409.
5. Bad values (title "", isrc "EG-A1B-26-0001", genre "Pop", releaseDate "2026-02-30", random artistId)
   → 400 with each field named.
6. PATCH status: draft→distributed 409; →submitted 200; →draft 200; →submitted; →distributed 200;
   →draft 409 "final"; "published" 400; no token 401.
7. Two parallel PATCHes on a submitted track (draft vs distributed) → one 200, one 409.
8. Seed 1,000 tracks (scratch script) and time `GET /api/tracks?status=submitted&genre=Rap&page=5` (< 1 s).
9. Filters: `?status=published` → 400; `?artistId=nope` → 400; unknown artist id → empty page.
10. Arabic title round-trips unchanged through `GET /api/tracks/{id}`.
