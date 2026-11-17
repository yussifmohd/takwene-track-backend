# Research: Track Catalog with Filters and Status (API service)

## R1 – Where the status rules live
- Decision: `Domain/Tracks/TrackStatusRules` (static table) + `Track.ChangeStatus(TrackStatus target)`
  returning `StatusChangeResult { Outcome: Changed | AlreadyInStatus | NotAllowed, Message }`.
- Rationale: requirement "enforce in the domain"; returning an outcome keeps exceptions for bugs and
  the Domain free of Ardalis.Result. `AllowedNext(from)` feeds the detail DTO, so the web app never
  keeps its own copy.
- Messages: same → "The track is already {status}."; draft→distributed → "A draft track must be
  submitted before it can be distributed."; from distributed → "A distributed track is final; its
  status cannot change."
- Alternatives: state pattern classes (overkill for 3 states); rules in the validator (not domain).

## R2 – Unknown values as 400 naming the field
- Decision: request records carry `string? Status`, `string? Genre`, `string? ReleaseDate`; FluentValidation
  checks membership/format with messages "Status must be one of: draft, submitted, distributed." etc.
- Alternatives: `JsonStringEnumConverter` binding → `BadHttpRequestException` with a JSON path, no
  `errors.status` for the form.

## R3 – Concurrent status changes
- Decision: Npgsql maps `uint Version` + `IsRowVersion()` to the `xmin` system column (no migration
  column). EF adds `WHERE xmin = @original` to the UPDATE; 0 rows → `DbUpdateConcurrencyException` →
  `ConcurrencyExceptionHandler` → 409 ProblemDetails. Integration test: two contexts load the same
  submitted track, one withdraws, the other distributes → exactly one save succeeds.
- InMemory provider also honours concurrency tokens, but the real proof is the Postgres test.
- Alternatives: `SELECT … FOR UPDATE` (needs explicit transactions in the handler), app-level version
  column (works, but `xmin` is free).

## R4 – ISRC rules (ISO 3901)
- Shape: CC (2 letters) – XXX (registrant, 3 alphanumerics) – YY (2 digits) – NNNNN (5 digits).
- Input: trim, upper-case (invariant); accept exactly `CC-XXX-YY-NNNNN` (hyphens at 2, 6, 9) or 12
  characters without hyphens. Anything else (other separators, partial hyphens, inner spaces) is
  invalid: "Enter the ISRC as CC-XXX-YY-NNNNN, e.g. EG-A1B-26-00001."
- CC is not checked against ISO 3166 (ISRC prefixes include non-country codes such as QZ, ZZ).
- Storage `char(12)`, unique `ix_tracks_isrc`, check constraint as defence in depth.
- The validator calls `Isrc.TryNormalize` (single source of the rule) and `TrackByIsrcSpec` for the
  duplicate pre-check (`conflict` code → 409, existing mapping from Phase 2).

## R5 – Genre and status storage
- Decision: `varchar(20)` text columns with check constraints; `Genres.All` is the single list
  (exact names, case-sensitive); status via `HasConversion` to "draft"/"submitted"/"distributed".
- OpenAPI: a schema transformer adds `enum` for `genre` and `status` properties (and the `genre`/
  `status` query parameters) so `openapi-typescript` emits literal unions.
- Alternatives: lookup tables (needless joins for a fixed list), PG enum types (migration friction).

## R6 – Release date
- `DateOnly` ↔ `date`; parse with `DateOnly.TryParseExact(value, "yyyy-MM-dd", InvariantCulture)`;
  range 1900-01-01..2100-12-31; future allowed. Messages: "Enter the release date as YYYY-MM-DD." /
  "Enter a release date between 1900 and 2100."

## R7 – Artist must exist
- Validator `MustAsync` with `ArtistByIdSpec` (AnyAsync) → 400 `errors.artistId` "Choose an existing
  artist."; empty GUID → "Choose an artist." FK `ON DELETE RESTRICT` (artists can't be deleted yet).

## R8 – Filtering, ordering, projection
- `TrackFilter` record; extension `ApplyFilter(this ISpecificationBuilder<Track> q, TrackFilter f)`
  used by `TracksCountSpec` and `TracksPageSpec` (identical WHERE for totals and items).
- Order: `ReleaseDate DESC, Title.ToLower(), Id`. Indexes: `ix_tracks_artist_id`, `ix_tracks_status`,
  `ix_tracks_release_date_id (release_date DESC, id)`.
- Projection: `new TrackListItemDto(t.Id, t.Title, t.ArtistId, t.Artist.Name, Isrc.Format(t.Isrc), t.Genre, wire(t.Status), t.ReleaseDate)`
  – EF translates the join and evaluates `Format` client-side in the top-level projection.
- Query string: `artistId` (Guid?, malformed → 400 by binding, handled by `BadRequestExceptionHandler`),
  `genre`, `status` (strings, validated), `page`, `pageSize` (Phase 2 rules). Empty strings = no filter.

## R9 – Detail and distributions placeholder
- `TrackDetailDto(Id, Title, ArtistId, ArtistName, Isrc, Genre, Status, ReleaseDate, CreatedAtUtc,
  UpdatedAtUtc?, AllowedNextStatuses[], Distributions[])`. `TrackDistributionDto(DspId, DspName,
  Status, UpdatedAtUtc)` – Phase 4 may extend (additive).

## R10 – Create and change responses
- POST 201 + Location + detail; PATCH 200 + detail; both read back through `TrackDetailSpec` after the
  UoW commit? No – the handler builds the DTO from the entity + artist name it already loaded (no
  second query), `CreatedAtUtc` set in the factory (Phase 2 lesson).

## R11 – Artist search and GET by id
- `search` trimmed; empty = no filter; > 100 characters → 400. `Name.ToLower().Contains(term.ToLower())`
  (EF/Npgsql translates to `strpos(lower(name), lower(@term)) > 0`, no wildcard injection).
  Same filter on count and page specs. Arabic has no case, so `lower()` is a no-op there.
- `GET /api/artists/{id:guid}` → `ArtistDto` (email only for admins) or 404.
