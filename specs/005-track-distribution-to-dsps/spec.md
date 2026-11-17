# Feature Specification: Track Distribution to DSPs (API service)

**Feature Branch**: `005-track-distribution-to-dsps`

**Created**: 2026-11-17

**Status**: Draft

**Input**: User description: "Phase 4 – Distribution. TrackDistribution entity (many-to-many between Track and Dsp): track, DSP, submittedAt, status (pending, live, rejected), unique on (track, DSP), migration, domain rules. POST /api/tracks/{id}/distribute (Admin-only) with one or more DSP ids; allowed only when the track is submitted or already distributed; a DSP that is pending or live is not duplicated, a rejected one goes back to pending with a new submittedAt; the track becomes distributed. PATCH /api/tracks/{id}/status to distributed must return 409 with a clear message unless the track has at least one distribution; the other status rules and the assignment's PATCH requirement stay. An Admin-only endpoint sets a distribution to live or rejected (simulated DSP response). GET /api/tracks/{id} returns the distributions with DSP name, status and submittedAt. Unit tests and PostgreSQL tests marked Integration."

## Clarifications

### Session 2026-11-17 (confirmed by the user at the analyze checkpoint; Q1 and the Q3 limit changed by the user)

One batched round for both repos (web app questions in the web app's spec).

- Q1: What happens to tracks that are already distributed without any delivery (possible since Phase 3,
  when the status change alone could mark a track distributed)? → A (**changed by the user**): **No data
  migration.** No real data exists; local databases created before this phase are reset with
  `docker compose down -v`, and Phase 5's seed creates consistent data. Documented in the README and
  Memory.md. Neither the API nor the web app has a special case for "distributed without deliveries".
- Q2: Which platform answers can be simulated? → A (recommended, confirmed): **pending → live and pending →
  rejected only.** Live is final for the simulation; a rejected delivery returns to pending only by
  distributing it again (as asked). Setting the current status again is a conflict.
  *(Alternative: also allow live → rejected, e.g. a takedown.)*
- Q3: What does distribute answer, and when is it a no-op? → A (recommended, confirmed; limit **changed
  by the user**): **Always the updated track (200)**, also when every requested platform was already
  pending or live (nothing changes, no error). A request names one or more distinct existing platforms,
  up to a request limit of 20 distinct ids (the number of platforms is not hard-coded); repeated ids
  count once; an empty list or an unknown id is a validation error on the platform list.
- Q4: Which times does a delivery show? → A (recommended, confirmed): **When it was sent (submittedAt) and when the
  platform last answered** (time of the change to live or rejected; empty while pending). Both are
  needed to follow a delivery; the second costs one column.

## Status and distributions (replaces the Phase 3 reconciliation)

Phase 3 let an admin mark a submitted track distributed with the status change alone; such a track had
no delivery records. From this phase on:

- **"Distributed" means "sent to at least one platform".** The status change to distributed is refused
  as a conflict with a clear message unless the track has at least one distribution. Since deliveries
  are only created by distributing (which itself makes the track distributed) and a distributed track
  cannot go back, the status change to distributed is in practice always refused with that message –
  the endpoint and its other rules are unchanged (assignment requirement kept).
- **Distribute** is the way to make a track distributed: on a submitted track it creates the deliveries
  and moves the status to distributed through the same domain rule; on a distributed track it only adds
  or re-sends deliveries.
- The allowed next statuses a track reports follow the same rule, so a submitted track lists only
  "draft"; distributing is a separate action.
- Delivery answers (live, rejected) never change the track's status; distributed stays final.
- Existing distributed tracks without deliveries: none are kept – local databases from before this
  phase are reset (Q1).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin sends a track to platforms (Priority: P1)

The signed-in admin sends a submitted track to one or more platforms. Each platform gets a
pending delivery with the time it was sent, and the track becomes distributed.

**Why this priority**: Distribution is the purpose of the product; without it no track reaches a
platform.

**Independent Test**: Create and submit a track; distribute it to Spotify and Apple Music; read it back
with two pending deliveries and status distributed.

**Acceptance Scenarios**:

1. **Given** a submitted track, **When** the admin distributes it to Spotify and Apple Music, **Then**
   the track is distributed and has two pending deliveries, each with the platform's name and the time
   it was sent.
2. **Given** a distributed track with a pending Spotify delivery, **When** the admin distributes it to
   Spotify and YouTube Music, **Then** Spotify is unchanged (same sent time) and YouTube Music is added
   as pending.
3. **Given** a distributed track whose Apple Music delivery is live, **When** distributed to Apple
   Music again, **Then** nothing changes and the answer is the unchanged track (no error).
4. **Given** a distributed track whose YouTube Music delivery was rejected, **When** distributed to
   YouTube Music again, **Then** that delivery is pending again with a new sent time and no answer time.
5. **Given** a draft track, **When** the admin distributes it, **Then** it is refused as a conflict
   explaining that the track must be submitted first, and nothing is stored.
6. **Given** an empty platform list, more than 20 different platforms, or an unknown platform, **When**
   distributing, **Then** it is refused as a validation error naming the platforms field; repeated ids
   count once.
7. **Given** two admins distribute the same track at the same moment, **When** both are saved, **Then**
   one succeeds and the other gets a conflict asking to reload; no platform appears twice.
8. **Given** no token or a non-admin token, or an unknown track, **When** distributing, **Then** it is
   refused (not signed in / not allowed / not found).

---

### User Story 2 - Status change to distributed requires a delivery (Priority: P1)

The status change keeps working as in Phase 3, except that "distributed" is refused unless the track has
at least one delivery, with a message that says how to distribute it.

**Why this priority**: Without it, "distributed" could again mean "sent nowhere" (the gap found in
Phase 3).

**Independent Test**: Try to set a submitted track to distributed by status change (refused with the
message); distribute it (works); the other status changes behave as before.

**Acceptance Scenarios**:

1. **Given** a submitted track without deliveries, **When** the admin changes its status to
   distributed, **Then** it is refused as a conflict saying the track becomes distributed when it is
   sent to at least one platform.
2. **Given** a draft track, **When** changed to distributed, **Then** it is refused as before (submit
   first).
3. **Given** draft → submitted and submitted → draft, **When** requested, **Then** they work as in
   Phase 3; a distributed track still refuses every change.
4. **Given** a submitted track, **When** read, **Then** its allowed next statuses are only "draft".

---

### User Story 3 - Admin records a platform's answer (Priority: P1)

The admin simulates a platform's answer for one delivery: live or rejected.

**Why this priority**: Real platforms are not connected; the simulation is how the team (and the
reviewer) sees deliveries move.

**Independent Test**: Distribute a track; set Spotify live and Apple Music rejected; read the track.

**Acceptance Scenarios**:

1. **Given** a pending Spotify delivery, **When** the admin sets it live, **Then** it is live and shows
   when it was answered; the track stays distributed.
2. **Given** a pending delivery, **When** set rejected, **Then** it is rejected with an answer time; it
   can be sent again by distributing (User Story 1, scenario 4).
3. **Given** a live delivery, **When** set live, **Then** it is refused as a conflict ("already live");
   **When** set rejected, **Then** refused ("a live delivery is final"). **Given** a rejected delivery,
   **When** set rejected, **Then** refused ("already rejected"); **When** set live, **Then** refused
   explaining it must be sent again with Distribute first.
4. **Given** "pending" or an unknown value, **When** requested, **Then** it is refused as a validation
   error naming the status field.
5. **Given** a platform the track was never sent to, an unknown platform, or an unknown track, **When**
   requested, **Then** "not found" is returned.
6. **Given** no token or a non-admin token, **When** requested, **Then** it is refused.

---

### User Story 4 - Anyone follows a track's deliveries (Priority: P1)

Any visitor reading a track sees each delivery: platform name, status, when it was sent and when the
platform answered.

**Why this priority**: The detail view is where distribution is followed.

**Acceptance Scenarios**:

1. **Given** a track with deliveries, **When** read, **Then** each delivery lists platform id and name,
   status, sent time, answer time (empty while pending) and the answers it can still receive; deliveries
   are ordered by platform name.
2. **Given** a track without deliveries, **When** read, **Then** the list is empty and the track says
   whether it can be distributed (submitted or distributed).

---

### Edge Cases

- Distribute with all requested platforms already pending or live → unchanged track, 200, and the
  track's last-change time is not touched.
- Distribute on a distributed track whose platforms are all live → nothing to send; same as above.
- A rejected delivery sent again keeps its row (unique per track and platform); only status, sent time
  and answer time change.
- The track's last-change time is updated whenever its deliveries change (they are part of the track).
- Platform answers never change the track's status.
- The status change of a distributed track (to anything) stays refused as final.
- A local database created before this phase may hold distributed tracks without deliveries; it is
  reset with `docker compose down -v` (Q1), not migrated.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A delivery (distribution) MUST link one track and one platform, with a status (pending,
  live, rejected), the time it was sent and the time of the platform's last answer (empty while
  pending); a track MUST have at most one delivery per platform, also under concurrent requests.
- **FR-002**: Only a signed-in admin MUST be able to distribute a track or record a platform's answer;
  reading stays open to everyone.
- **FR-003**: Distributing MUST accept one or more distinct existing platform ids, at most 20 distinct
  ids per request (repeats count once; an empty list or an unknown id refused as a validation error) and be allowed only for submitted or distributed tracks; otherwise it is a conflict
  with an explanation.
- **FR-004**: For each requested platform: no delivery → create pending (sent now); pending or live →
  unchanged; rejected → pending again with a new sent time and no answer time.
- **FR-005**: Distributing a submitted track MUST make it distributed, through the same domain rule as
  the status change; distributing a distributed track keeps it distributed.
- **FR-006**: The status change to distributed MUST be refused as a conflict with a clear message unless
  the track has at least one delivery; all other status rules from Phase 3 stay unchanged, and the
  allowed next statuses MUST follow this rule.
- **FR-007**: Recording an answer MUST accept only "live" or "rejected" (anything else is a validation
  error) and allow only pending → live and pending → rejected; other changes are conflicts with an
  explanation; an unknown track, platform or delivery is "not found".
- **FR-008**: Reading a track MUST return its deliveries with platform id and name, status, sent time,
  answer time and the answers each can still receive, ordered by platform name, and whether the track
  can be distributed.
- **FR-009**: Concurrent changes to one track (distribute, answers, status changes) MUST NOT both
  succeed; the later one is refused as a conflict asking to reload.
- **FR-010**: The rules MUST live in the domain (track and delivery), not in endpoints.
- **FR-011**: No data migration for pre-existing data (Q1); the reset is documented in the README.

### Key Entities

- **Track** (Phase 3): now owns its deliveries; distributed only with at least one.
- **Distribution (delivery)**: a track's delivery to one platform – platform, status (pending, live,
  rejected), sent time, answer time.
- **Platform (DSP)** (Phase 2): Spotify, Apple Music, YouTube Music (fixed).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 0 tracks become distributed without at least one delivery through any API use.
- **SC-002**: 0 duplicate deliveries per track and platform after concurrent distribute requests.
- **SC-003**: 100% of the delivery answer rules (2 allowed, all others refused) behave as specified.
- **SC-004**: An admin distributes a track to three platforms and records two answers in under 1 minute.
- **SC-005**: Reading a track with three deliveries returns in under 1 second with 1,000 tracks stored.

## Assumptions

- Real platform integrations are out of scope; answers are simulated by the admin endpoint.
- Deliveries are not deleted (no "unpublish").
- Seed data with varied deliveries comes with Phase 5 (interview answer 8).
- The platforms are the stored platform list (three today); nothing in the rules depends on that number.
