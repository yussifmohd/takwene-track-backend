# Data Model: Artists & Distribution Platforms (API service)

## Artist (`artists`)

| Property | Column | Type | Rules |
|---|---|---|---|
| Id | `id` | uuid PK | `Guid.CreateVersion7()` in code (`ValueGeneratedNever`) |
| Name | `name` | varchar(200) not null | trimmed; 1–200 code points; any script; stored unchanged otherwise |
| CountryCode | `country_code` | char(2) not null | assigned ISO 3166-1 alpha-2, upper case |
| Email | `email` | varchar(254) not null | trimmed; required; valid format |
| NormalizedEmail | `normalized_email` | varchar(254) not null, unique `ix_artists_normalized_email` | `upper(trim(email))`; not exposed |
| CreatedAtUtc | `created_at_utc` | timestamptz not null | set by `AuditableEntityInterceptor` |
| UpdatedAtUtc | `updated_at_utc` | timestamptz null | set on modify (no edit endpoint yet) |

Domain: `Artist.Create(string name, string countryCode, string email)` normalises (trim, upper) and throws `ArgumentException` on values the validator should already have rejected
(defence in depth, not user-facing).

## Dsp (`dsps`)

| Property | Column | Type | Rules |
|---|---|---|---|
| Id | `id` | uuid PK | fixed ids in `Dsp.Seed` |
| Name | `name` | varchar(100) not null, unique `ix_dsps_name` | |

Seed (`HasData`): Spotify, Apple Music, YouTube Music.

## DTOs (Application)

- `ArtistDto(Guid Id, string Name, string CountryCode, string? Email, DateTime CreatedAtUtc)` —
  `Email` omitted from JSON when null (anonymous readers).
- `PagedResult<ArtistDto>(Items, Page, PageSize, TotalCount)` + `TotalPages`.
- `DspDto(Guid Id, string Name)`.

## Requests

- `CreateArtistRequest(string? Name, string? CountryCode, string? Email)` → `CreateArtistCommand`.
- `GET /api/artists?page=&pageSize=` → `ListArtistsQuery(Page, PageSize, IncludeEmail)`.
- `GET /api/dsps` → `ListDspsQuery`.

## Relationships

None yet. Phase 3 adds `Track.ArtistId → artists.id` (Restrict); Phase 4 adds distributions
referencing `dsps.id`.
