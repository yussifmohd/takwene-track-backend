# Quickstart: Distribution (manual validation)

1. Before updating: with the Phase 3 stack, mark one submitted track distributed by PATCH (old rule).
   `docker compose up --build -d` with this branch → that track is now `submitted` (Q1 migration);
   `GET /api/dsps` → the three platform ids. Sign in → `$T`.
2. PATCH a submitted track's status to `distributed` → 409 with the "Use Distribute" message;
   `allowedNextStatuses` is `["draft"]`.
3. `POST /api/tracks/{id}/distribute` `{ dspIds: [spotify, apple] }` → 200, status distributed, two
   pending deliveries ordered by name; again with `[spotify, youtube]` → Spotify unchanged, YouTube added.
4. Draft track → 409; `dspIds: []`, unknown id, 4 distinct ids → 400 `errors.dspIds`; no token → 401.
5. `PATCH …/distributions/{spotify}` `live` → answered time set; again → 409; `rejected` on Apple → 200;
   `pending` → 400; YouTube on another track never sent there → 404.
6. Distribute Apple again → pending, new submitted time, answer cleared.
7. 20 parallel distribute requests for the same platform on one distributed track → 1×200 + 409s
   (or 200 no-ops after the first commit); exactly one row. A distribute and a withdraw racing on a
   submitted track → one wins.
8. `GET /api/tracks/{id}` anonymously → deliveries with names and times, no admin-only data.
