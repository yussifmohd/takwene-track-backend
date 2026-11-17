# Research: Track Distribution to DSPs (API service)

## R1 – Where deliveries live
- Decision: `TrackDistribution` is a child entity of the `Track` aggregate (backing list, read-only view).
- Rationale: the rules ("distributed needs a delivery", "rejected → pending on re-send", stamping for
  concurrency) span the track and its deliveries; one aggregate keeps them in one place (constitution I).
- Alternatives: a separate aggregate with its own repository – the status rule would then need a
  cross-aggregate check in a handler.

## R2 – Uniqueness
- Decision: composite primary key `(track_id, dsp_id)` named `pk_track_distributions`.
- Rationale: the natural key is the business rule; no surrogate id is needed (deliveries are addressed
  by track + DSP in the API).
- Alternatives: `id` + unique index – one more column and index for nothing.

## R3 – Concurrency
- Decision: every delivery change also sets `Track.UpdatedAtUtc`, so EF updates the `tracks` row with
  the existing `xmin` check (Phase 3). The PK is a second safety net for inserts.
- Rationale: one token per aggregate covers distribute vs distribute, distribute vs status change,
  answer vs re-send.
- Alternatives: `xmin` on `track_distributions` – does not see a concurrent status change.

## R4 – Status rule
- Decision: `TrackStatusRules.Check(from, to, hasDistributions)`; submitted → distributed only with
  deliveries; `AllowedNext` follows. Message: "A track becomes distributed when it is sent to at least
  one platform. Use Distribute to send it."
- Rationale: keeps PATCH as the assignment requires while closing the Phase 3 gap the user found.

## R5 – Distribute input
- Decision: `{ "dspIds": [...] }`, not empty, at most 20 distinct ids (repeats count once); empty or
  unknown → 400 `errors.dspIds`; existence checked in the handler against all stored DSPs (one small query).
- Rationale: the number of platforms is data, not a rule (user); 20 is a request limit, not a business
  rule; the 32 KB body cap bounds the raw list; checking in the handler
  avoids loading DSPs twice (validator + handler).

## R6 – Answers
- Decision: `PATCH /api/tracks/{id}/distributions/{dspId}` with `status` live|rejected; pending → live |
  rejected only (Q2). Messages: live→live "The delivery is already live."; live→rejected "A live
  delivery is final."; rejected→rejected "The delivery is already rejected."; rejected→live "A rejected
  delivery must be sent again with Distribute before the platform can answer." No delivery for the DSP → 404 "This track has not been sent to this platform."
- Rationale: route shape from interview answer 8; "pending" is reached only by distributing.

## R7 – Times
- Decision: `submitted_at_utc timestamptz not null`, `answered_at_utc timestamptz null`; check
  `(status = 'pending') = (answered_at_utc IS NULL)`; status check `IN ('pending','live','rejected')`.
- Rationale: Q4; the check keeps the two fields consistent even for manual SQL.

## R8 – Detail shape
- Decision: `TrackDistributionDto(dspId, dspName, status, submittedAtUtc, answeredAtUtc?,
  allowedAnswers)`, ordered by DSP name; `TrackDetailDto.canDistribute`.
- Rationale: the web app shows only what the API allows (as with `allowedNextStatuses`).

## R9 – Existing data (Q1, changed by the user)
- Decision: no data migration. Local databases created before this phase may contain distributed
  tracks without deliveries; developers reset them with `docker compose down -v` (README, Memory.md);
  Phase 5's seed creates consistent data.
- Rationale: no real data exists; a data-changing migration would add risk and code for nothing.
