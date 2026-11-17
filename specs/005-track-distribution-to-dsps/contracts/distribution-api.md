# Contract intent: distribution endpoints

Exported OpenAPI (`contracts/openapi.json`) is produced at implementation; this file is the reviewed intent.

| Method & route | Auth | Body | Success | Errors |
|---|---|---|---|---|
| `POST /api/tracks/{id}/distribute` | Admin | `{ "dspIds": ["<uuid>", …] }` | 200 `TrackDetailDto` (also when nothing changed) | 400 `errors.dspIds` (missing, > 10, > 3 distinct, unknown platform); 401/403; 404 track; 409 draft track / concurrent change |
| `PATCH /api/tracks/{id}/distributions/{dspId}` | Admin | `{ "status": "live" \| "rejected" }` | 200 `TrackDetailDto` | 400 `errors.status`; 401/403; 404 track or delivery; 409 not allowed / concurrent change |
| `PATCH /api/tracks/{id}/status` | Admin | unchanged | unchanged | + 409 "A track becomes distributed when it is sent to at least one platform. Use Distribute to send it." |
| `GET /api/tracks/{id}` | public | – | `TrackDetailDto` with `canDistribute` and `distributions[]` (`dspId`, `dspName`, `status`, `submittedAtUtc`, `answeredAtUtc`, `allowedAnswers`) | 404 |

All errors are ProblemDetails. Breaking change for the web app only: `TrackDistributionDto` loses
`updatedAtUtc` and its free-text `status`.
