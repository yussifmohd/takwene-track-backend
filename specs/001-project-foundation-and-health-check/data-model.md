# Data Model: Foundation & Architecture

No business entities in Phase 0 (spec: Key Entities = none). The database contains only the EF
migrations history table after the empty `InitialCreate` baseline migration.

## Shared base types (Domain/Common)

| Type | Members | Purpose |
|---|---|---|
| `BaseEntity<TKey>` | `TKey Id { get; init; }` | Ids generated in code (Guid), `ValueGeneratedNever` in configs |
| `IAuditableEntity` | `CreatedAtUtc`, `UpdatedAtUtc` | Filled by a SaveChanges interceptor once entities exist (Phase 2) |

## Read models (Api/Health) — not persisted

### HealthReportDto
| Field | Type | Notes |
|---|---|---|
| `status` | enum string `healthy` \| `degraded` \| `unhealthy` | overall |
| `checkedAt` | string (date-time, UTC) | time the report was produced |
| `checks` | `HealthCheckEntryDto[]` | one per registered check |

### HealthCheckEntryDto
| Field | Type | Notes |
|---|---|---|
| `name` | string | `database` in Phase 0 |
| `status` | enum string (as above) | |
| `durationMs` | number | rounded to 1 decimal |

HTTP: 200 when `status = healthy`, otherwise 503 with the same body. No exception text is exposed.

### PagedResult<T> (Application/Common/Models) — used from Phase 2
| Field | Type |
|---|---|
| `items` | `T[]` |
| `page` | int (≥ 1) |
| `pageSize` | int (1–50, default 20) |
| `totalCount` | int |
| `totalPages` | int (derived) |
