# Quickstart: Artists & DSPs (manual validation)

Prerequisites: `.env` with secrets and the admin (Phase 1); Docker running.

1. `docker compose down -v && docker compose up --build` → log shows migrations applied, no encoding error.
2. `curl -s localhost:8080/api/dsps` → exactly Apple Music, Spotify, YouTube Music. Restart the API → same ids.
3. Sign in (Phase 1 quickstart) → `$TOKEN`.
4. `curl -s -X POST localhost:8080/api/artists -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"عمرو دياب","countryCode":"eg","email":"booking@example.com"}'`
   → 201, `countryCode` "EG", name byte-identical (check with `| od -c` or a JSON viewer).
5. Repeat with `"email":" Booking@Example.com "` → 409 with `errors.email`.
6. POST without token → 401; with invalid body (`{"name":42}`) → 400 ProblemDetails (not 500), also with
   `ASPNETCORE_ENVIRONMENT=Development`.
7. `curl -s "localhost:8080/api/artists?page=1&pageSize=2"` → no `email` fields; with `$TOKEN` → emails shown.
   `pageSize=51` → 400 `errors.pageSize`.
8. `docker compose exec db psql -U trackmgmt -d trackmgmt -c "select name, country_code, created_at_utc from artists"`
   → Arabic shown correctly, timestamps filled.
9. `dotnet test --solution TrackManagement.slnx` (Docker running) → unit + integration tests pass.
