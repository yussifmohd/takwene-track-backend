# Quickstart: Distribution (manual validation)

1. `docker compose down -v && docker compose up --build -d` (fresh database, Q1);
   `GET /api/dsps` → the platform ids. Sign in → `$T`; create and submit a few tracks.
2. PATCH a submitted track's status to `distributed` → 409 with the "Use Distribute" message;
   `allowedNextStatuses` is `["draft"]`.
3. `POST /api/tracks/{id}/distribute` `{ dspIds: [spotify, apple] }` → 200, status distributed, two
   pending deliveries ordered by name; again with `[spotify, youtube]` → Spotify unchanged, YouTube added.
4. Draft track → 409; `dspIds: []`, an unknown id, 21 distinct ids → 400 `errors.dspIds`; no token → 401.
5. `PATCH …/distributions/{spotify}` `live` → answered time set; again → 409; `rejected` on Apple → 200;
   `pending` → 400; YouTube on another track never sent there → 404.
6. Distribute Apple again → pending, new submitted time, answer cleared.
7. 20 parallel distribute requests for the same new platform on one distributed track → every response
   is 200 or 409 (at least one 200), and the track has exactly one delivery for that platform. A
   distribute and a withdraw racing on a submitted track → exactly one 200, one 409 (A3).
8. `GET /api/tracks/{id}` anonymously → deliveries with names and times, no admin-only data.
9. Timing (A4): with 1,000 tracks (scratch seed), `GET /api/tracks/{id}` of a track with three deliveries
   < 1 s (SC-005).
