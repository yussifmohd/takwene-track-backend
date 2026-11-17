# Data Model: Track Distribution to DSPs

## TrackDistribution (table `track_distributions`)

| Field | Type | Rules |
|---|---|---|
| track_id | uuid | PK part; FK → `tracks.id` (cascade; tracks are not deleted) |
| dsp_id | uuid | PK part; FK → `dsps.id` (restrict); index `ix_track_distributions_dsp_id` |
| status | text (max 10) | `pending` / `live` / `rejected` (check `ck_track_distributions_status`) |
| submitted_at_utc | timestamptz | set when created and when sent again |
| answered_at_utc | timestamptz null | set on live/rejected; null while pending (check `ck_track_distributions_answered`) |

Key: `pk_track_distributions (track_id, dsp_id)`.

## Delivery status transitions

| From \ To | pending | live | rejected |
|---|---|---|---|
| (none) | Distribute | – | – |
| pending | Distribute: no-op | answer ✅ | answer ✅ |
| live | Distribute: no-op | 409 "already live" | 409 "a live delivery is final" |
| rejected | Distribute: re-send (new submitted time, answer cleared) | 409 "must be sent again with Distribute" | 409 "already rejected" |

## Track (changes)

- `Distributions` (at most one per DSP).
- Status rule: submitted → distributed only with ≥ 1 delivery; `Distribute` on submitted does it.
- Any delivery change sets `UpdatedAtUtc` (and thus the `xmin` check).

## Wire shapes

- `TrackDistributionDto`: `dspId`, `dspName`, `status` (`pending`|`live`|`rejected`), `submittedAtUtc`,
  `answeredAtUtc` (null while pending), `allowedAnswers` (`live`|`rejected` list).
- No data migration for pre-existing rows (Q1): reset local databases with `docker compose down -v`.
- `TrackDetailDto`: Phase 3 fields + `canDistribute` (bool); `distributions` ordered by `dspName`.
- `DistributeTrackRequest`: `dspIds` (array of uuid). `RecordDistributionAnswerRequest`: `status` (`live`|`rejected`).
