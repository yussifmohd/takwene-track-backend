# Contract intent: Artists & DSPs

Reviewed intent; the authoritative contract is `openapi.json` in this folder, exported from the
running app at implementation (`scripts/export-contract.ps1 -Feature 003-artists-and-dsps-management`).

## POST /api/artists (Admin)

Request:
```json
{ "name": "عمرو دياب", "countryCode": "eg", "email": "booking@example.com" }
```
- `201` + `Location: /api/artists/{id}`:
  `{ "id": "…", "name": "عمرو دياب", "countryCode": "EG", "email": "booking@example.com", "createdAtUtc": "2026-11-17T12:00:00Z" }`
- `400` ValidationProblem: `errors.name | errors.countryCode | errors.email` (camelCase keys).
- `409` ValidationProblem (title "Conflict"): `errors.email = ["An artist with this email already exists."]`.
- `401` (no/invalid token), `403` (not Admin), `413` (body > 32 KB) — ProblemDetails.

## GET /api/artists?page=1&pageSize=20 (public)

- `200`: `{ "items": [ { "id", "name", "countryCode", "createdAtUtc", "email"? } ], "page": 1, "pageSize": 20, "totalCount": 25, "totalPages": 2 }`
  — `email` present only for an Admin bearer token.
- `400` ValidationProblem: `errors.page` (< 1), `errors.pageSize` (outside 1–50); malformed values → 400 ProblemDetails.

## GET /api/dsps (public)

- `200`: `[ { "id": "…0002", "name": "Apple Music" }, { "id": "…0001", "name": "Spotify" }, { "id": "…0003", "name": "YouTube Music" } ]`
