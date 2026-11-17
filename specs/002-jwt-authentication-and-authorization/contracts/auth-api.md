# Auth API contract (Phase 1)

The machine-readable contract (`openapi.json` in this folder) is exported from the running API
during implementation (`scripts/export-contract.ps1 -Feature 002-jwt-authentication-and-authorization`)
and is what the web app generates its types from. This file is the reviewed intent.

## POST /api/auth/login (anonymous, rate limited)

Request `application/json`: `{ "email": "admin@takwene.local", "password": "…" }`

| Status | Body | When |
|---|---|---|
| 200 | `LoginResponse` | valid credentials |
| 400 | ValidationProblemDetails (`errors.email`, `errors.password`) | missing/malformed/too long |
| 401 | ProblemDetails `{ title: "Unauthorized", detail: "Invalid email or password." }` | unknown email, wrong password, locked out (identical) |
| 429 | ProblemDetails `{ title: "Too many requests", detail: "Too many sign-in attempts. Try again in N seconds." }` + `Retry-After: N` | > 10 attempts/min from the client |

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs…",
  "tokenType": "Bearer",
  "expiresAt": "2026-11-17T11:00:00+00:00",
  "user": { "email": "admin@takwene.local", "roles": ["Admin"], "expiresAt": "2026-11-17T11:00:00+00:00" }
}
```

## GET /api/auth/me (Bearer required)

| Status | Body |
|---|---|
| 200 | `CurrentUserDto { email, roles, expiresAt }` |
| 401 | ProblemDetails (missing/invalid/expired token), `WWW-Authenticate: Bearer` |

## Admin-only writes (from Phase 2)

Every write endpoint uses `RequireAdmin()`: 401 without a valid token, 403 ProblemDetails when the
token lacks the `Admin` role. Reads and `/health`, `/api/health` stay anonymous.
