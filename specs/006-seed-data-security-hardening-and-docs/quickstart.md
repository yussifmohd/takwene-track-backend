# Quickstart: verify Phase 5 (API)

1. Fresh database: `docker compose down -v && docker compose up -d --build`. Log shows the sample was added.
   `GET /api/artists?pageSize=50` → 17 artists, all emails `@example.com` (as admin); `GET /api/tracks?pageSize=50`
   → 32 tracks; each `status` and `genre` filter returns ≥ 1.
2. `docker compose restart api` → log "Sample catalogue skipped: the catalogue already has data"; counts unchanged.
3. Open each distributed track: ≥ 1 delivery; pending, live and rejected appear on all three platforms.
4. Anonymous `POST/PATCH` to every change route → 401; admin token → expected status.
5. Response headers of `GET /api/tracks`: nosniff, DENY, no-referrer, CSP, `Cache-Control: no-store`.
6. 301 GETs in a minute from one client → 429 + `Retry-After`; `/health` still 200. 61 changes in a minute → 429.
7. `OPTIONS` preflight from `http://evil.example` → no `Access-Control-Allow-Origin`; from `http://localhost:8081` → allowed.
8. Sign-in timing: 100 unknown-email vs 100 wrong-password attempts (limit raised for the run) → medians within the same range; recorded.
9. Scans: `dotnet list package --vulnerable --include-transitive`; Trivy on both images; gitleaks on both histories → recorded in `docs/security-review.md`.
10. Clean clone into a new scratch folder → `dotnet test --solution TrackManagement.slnx` passes.
11. Every link in README, DECISIONS.md, docs/study-guide.md, docs/security-review.md resolves.
