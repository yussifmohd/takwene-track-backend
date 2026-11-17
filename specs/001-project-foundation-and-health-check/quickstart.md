# Quickstart: Foundation & Architecture (API)

Validates the Phase 0 user stories end to end.

## Prerequisites
Docker Desktop, .NET SDK 10.0.403+ (`global.json` rolls forward to newer 10.0.4xx), Git.

## US1 – Start with one command
```powershell
cd backend
Copy-Item .env.example .env          # then set POSTGRES_PASSWORD and JWT__SIGNINGKEY (≥ 32 chars)
docker compose up -d --build
docker compose ps                    # api and db "healthy" within 3 minutes
curl.exe -i http://localhost:8080/health       # 200 {"status":"healthy",...,"checks":[{"name":"database",...}]}
curl.exe -i http://localhost:8080/api/health   # same body (contract endpoint)
docker compose stop db; curl.exe -i http://localhost:8080/health   # 503 "unhealthy"
docker compose start db
```
Missing secret check: blank `JWT__SIGNINGKEY` in `.env`, `docker compose up api` → container exits
with a validation message.

## US1 – Without Docker
```powershell
dotnet tool restore
$env:ConnectionStrings__Default="Host=localhost;Port=5432;Database=trackmgmt;Username=trackmgmt;Password=<pw>"
dotnet ef database update -p src/TrackManagement.Infrastructure -s src/TrackManagement.Api
dotnet run --project src/TrackManagement.Api   # http://localhost:8080
```

## US3 – API docs
Development: open http://localhost:8080/scalar (and `/openapi/v1.json`). Bearer input is shown.
Production check: `docker compose run -e ASPNETCORE_ENVIRONMENT=Production ...` → `/scalar` 404.

## US4 – Errors
`curl.exe -i http://localhost:8080/api/does-not-exist` → 404 `application/problem+json` with `traceId`.

## US2 – CI
Open a PR to main → "backend-tests" runs; artifacts `test-results` (TRX) and `coverage` attached.

## Tests locally
```powershell
dotnet test
```
