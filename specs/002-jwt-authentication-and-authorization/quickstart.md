# Quickstart: verify Phase 1 (API)

Prereq: `.env` from `.env.example` with `JWT__SIGNINGKEY`, `ADMIN__EMAIL`, `ADMIN__PASSWORD`
(≥ 12 chars, upper, lower, digit, symbol). `docker compose up --build`.

## US1 – sign in
```bash
curl -s -X POST localhost:8080/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"admin@takwene.local","password":"<ADMIN__PASSWORD>"}'        # 200 + accessToken
curl -s localhost:8080/api/auth/me -H "Authorization: Bearer <token>"         # 200 email/roles/expiresAt
```

## US2 – generic failures, lockout, throttling
- Unknown email vs wrong password → both 401 with identical `title`/`detail`.
- 5 wrong passwords then the right one → 401; after 15 min → 200.
- 11 requests within a minute → the 11th is 429 with `Retry-After`.

## US3 – access control
- `GET /api/auth/me` without token → 401 ProblemDetails; tampered token → 401.
- `GET /api/health` without token → 200.

## US4 – provisioning
- Remove `ADMIN__PASSWORD` from `.env` → `docker compose up` fails: api exits with a message naming `Admin:Password`.
- Restart with a different password → old password still works.

## Housekeeping
- `docker compose logs api | grep -i gssapi` → no output.
- PR checks run without the "Node.js 20 is deprecated" annotation.
