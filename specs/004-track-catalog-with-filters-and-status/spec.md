# Feature Specification: Track Catalog with Filters and Status (API service)

**Feature Branch**: `004-track-catalog-with-filters-and-status`

**Created**: 2026-11-17

**Status**: Draft

**Input**: User description: "Phase 3 – Tracks. Track entity: title, artist (required, must exist), ISRC, release date, genre, status (draft, submitted, distributed), with a migration. ISRC strict ISO 3901 (CC-XXX-YY-NNNNN), accepted with or without hyphens, stored as 12 uppercase characters without hyphens, unique on the stored value, shown with hyphens. Genre from a fixed list (Arabic Pop, Mahraganat, Khaleeji, Hip-Hop, Electronic, Rap, R&B, Rock). Release date is date-only, future dates allowed. Create a track (Admin-only); list tracks (public, paginated, filters artist, genre, status, with the artist name); read one track (public, with a distributions list that stays empty until Phase 4); change a track's status (Admin-only). Status rules: draft → submitted, submitted → draft (withdraw), submitted → distributed; distributed is final; a same-status or invalid change is a conflict, an unknown value is a validation error; rules enforced by the track itself, not the endpoint. Duplicate ISRC is a conflict on the isrc field, also when two requests race. Unit tests for handlers, validators, specifications and transitions; real-database tests in the same test project, marked Integration."

## Clarifications

### Session 2026-11-17 (recommended defaults; confirmed by the user at the analyze checkpoint)

One batched round for both repos (web app questions in the web app's spec).

- Q1: Can visitors see tracks in every status, including drafts? → A (recommended): **Yes.** Reads are public (interview answer 4) and this is a catalog management tool, not a public storefront; every list and detail shows all statuses.
- Q2: Are editing and deleting tracks part of this phase? → A (recommended): **No.** This phase creates tracks and changes their status (what was asked); editing other fields and deleting are left out and listed as a known limitation.
- Q3: How is an artist chosen when there are many? → A (recommended): **By typing part of the name.** The artist list gains an optional name search, and a single artist can be read by its identifier (the create-artist response already points to that address, which does not exist yet).
- Q4: In which order are tracks listed? → A (recommended): **Newest release date first**, then by title ignoring case, so upcoming and recent releases come first.

## Status and distributions (reconciliation with Phase 4)

Status is the catalog workflow an admin moves by hand; distributions (Phase 4) are delivery records,
one per track and platform. The two meet as follows, and this phase's status change is not altered:

- An admin may mark a submitted track as distributed with the status change alone (assignment
  requirement). Such a track simply has **no distribution rows**; nothing is invented for it.
- The track's detail always shows its distributions. For a distributed track without rows the web app
  says "Marked as distributed without platform deliveries recorded." (draft/submitted: "Not sent to
  any platform yet.").
- Phase 4's distribute action accepts submitted **and** distributed tracks (interview answer 7). On a
  submitted track it creates the rows and moves the status to distributed (through the same domain
  rule, `Track.ChangeStatus`); on a distributed track it only adds or resets rows, so delivery records
  can be added after a manual "distributed".
- Distribution rows never change the status back; distributed stays final.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin adds a track (Priority: P1)

The signed-in admin adds a track with its title, artist, ISRC, genre and release date. It starts
as a draft.

**Why this priority**: Tracks are the product; nothing else in this phase exists without them.

**Independent Test**: With an admin token, create a track and get it back with an identifier, the
artist's name, the ISRC with hyphens and status draft; without a token or with a non-admin token the
request is refused.

**Acceptance Scenarios**:

1. **Given** a signed-in admin and an existing artist, **When** they create "تملي معاك" by that artist
   with ISRC "eg-a1b-26-00001", genre "Arabic Pop" and release date 2026-12-01, **Then** the track is
   created as a draft and returned with ISRC "EG-A1B-26-00001", the artist's name and a reference to
   where it can be read.
2. **Given** an ISRC written without hyphens ("EGA1B2600002"), **When** submitted, **Then** it is
   accepted and shown as "EG-A1B-26-00002".
3. **Given** a track already uses "EG-A1B-26-00001", **When** another track is created with
   "EGA1B2600001" or "eg-a1b-26-00001", **Then** creation is refused as a conflict naming the ISRC
   field, and nothing is stored; the same holds when two such requests race.
4. **Given** a missing title, an artist that does not exist, an ISRC in the wrong shape (e.g.
   "EG-A1B-26-0001", "E1-A1B-26-00001", "EG_A1B_26_00001"), a genre outside the list, or a missing or
   impossible release date, **When** submitted, **Then** creation is refused with a message per field.
5. **Given** a release date in the future, **When** submitted, **Then** it is accepted.
6. **Given** no token, an invalid token or a token without the Admin role, **When** anyone creates a
   track, **Then** the request is refused and nothing is stored.

---

### User Story 2 - Anyone browses and filters the catalog (Priority: P1)

Any visitor lists tracks a page at a time, optionally only those of one artist, one genre and/or one
status, and sees each track's title, artist name, ISRC, genre, status and release date.

**Why this priority**: The track list is the web app's home page; filtering by status is how the
team sees what is waiting to be distributed.

**Independent Test**: Seed tracks across artists, genres and statuses; request pages with and
without each filter and combinations; check items, order and totals.

**Acceptance Scenarios**:

1. **Given** 45 tracks, **When** the first page is requested with defaults, **Then** 20 tracks are
   returned with total 45 and 3 pages, newest release date first.
2. **Given** tracks in all statuses, **When** filtered by status "submitted", **Then** only submitted
   tracks are returned and the totals count only them.
3. **Given** filters for an artist, a genre and a status together, **When** requested, **Then** only
   tracks matching all three are returned.
4. **Given** an artist with no tracks (or an artist identifier that does not exist), **When** used as
   a filter, **Then** an empty page with total 0 is returned.
5. **Given** an unknown genre or status, a malformed artist identifier, or invalid paging values,
   **When** requested, **Then** the request is refused with a validation message naming the value.

---

### User Story 3 - Anyone opens a track (Priority: P1)

Any visitor opens a single track and sees all its information, including the artist's name, when it
was added and changed, the status changes that are possible next, and its distributions (none yet).

**Why this priority**: The detail view is where status is changed and, in Phase 4, where
distribution is followed.

**Independent Test**: Create a track, read it by identifier; read an unknown identifier.

**Acceptance Scenarios**:

1. **Given** a track, **When** anyone reads it, **Then** title, artist (identifier and name), ISRC
   with hyphens, genre, status, release date, creation and last-change times, the allowed next
   statuses and an empty distributions list are returned.
2. **Given** an identifier that matches no track (or is not an identifier), **When** read, **Then**
   "not found" is returned.

---

### User Story 4 - Admin moves a track through its statuses (Priority: P1)

The signed-in admin submits a draft, withdraws a submitted track back to draft, or marks a submitted
track as distributed. Distributed is final.

**Why this priority**: Status is the core workflow of a distribution catalog.

**Independent Test**: Walk one track through every allowed change and try every forbidden one.

**Acceptance Scenarios**:

1. **Given** a draft, **When** the admin changes it to submitted, **Then** it is submitted and its
   last-change time is updated.
2. **Given** a submitted track, **When** changed to draft, **Then** it is a draft again; **When**
   changed to distributed, **Then** it is distributed.
3. **Given** a distributed track, **When** any change is requested, **Then** it is refused as a
   conflict explaining that distributed is final.
4. **Given** a draft, **When** changed directly to distributed, **Then** it is refused as a conflict
   explaining the allowed changes; **When** changed to its current status, **Then** it is refused as a
   conflict ("already draft").
5. **Given** a status value that does not exist (e.g. "published"), **When** requested, **Then** it is
   refused as a validation error naming the status field.
6. **Given** two admins change the same track at the same moment (e.g. one withdraws while the other
   marks it distributed), **When** both are saved, **Then** exactly one succeeds and the other gets a
   conflict asking to reload; a distributed track never goes back to draft.
7. **Given** no token or a non-admin token, or an unknown track, **When** a change is requested,
   **Then** it is refused (not signed in / not allowed / not found).

---

### User Story 5 - Choosing an artist by name (Priority: P2)

When an artist must be picked (creating a track, filtering the list), anyone can find artists by typing
part of their name, and read one artist by its identifier.

**Why this priority**: Supports the create form and the artist filter once there are more than a page
of artists; the catalog works without it for small rosters.

**Independent Test**: Search "amr" and "عمرو"; read one artist by identifier; read an unknown one.

**Acceptance Scenarios**:

1. **Given** artists "Amr Diab" and "عمرو دياب", **When** searching "amr", **Then** "Amr Diab" is
   returned; **When** searching "عمرو", **Then** "عمرو دياب" is returned; letter case is ignored.
2. **Given** an artist identifier, **When** read, **Then** the artist is returned (email only for an
   admin, as in the list); an unknown identifier returns "not found".

---

### Edge Cases

- ISRC with surrounding spaces or lower case → normalised; inner spaces, underscores, dots or a
  hyphen in the wrong place → refused (only the exact hyphen positions of CC-XXX-YY-NNNNN or none).
- ISRC country part: two letters (registrant prefixes such as "QZ" or "ZZ" exist, so it is not checked
  against the country list); registrant part: three letters or digits; year: two digits;
  designation: five digits.
- Title of only spaces → required; outer spaces removed; up to 200 characters in any script.
- Two tracks may share a title (covers, versions); only the ISRC is unique.
- Release dates are dates without a time or time zone; "2026-02-30" is refused; any year from 1900
  to 2100 is accepted.
- Genre names must match the list exactly ("R&B", "Hip-Hop"); other spellings are refused.
- Creating a track with a status other than draft is not possible (new tracks always start as draft).
- Status change on a track that does not exist → not found, even if the status value is valid.
- Filters combine with AND; omitted filters do not restrict.
- A page past the end returns an empty list with the correct totals.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Only a signed-in admin MUST be able to create a track or change its status; reading is
  open to everyone.
- **FR-002**: A track MUST have a title (required, outer spaces removed, 1–200 characters, any
  script), an existing artist, an ISRC, a genre from the fixed list and a release date (date only,
  1900-01-01 to 2100-12-31, future allowed).
- **FR-003**: The ISRC MUST follow ISO 3901 (two letters, three letters or digits, two digits, five
  digits), be accepted with or without hyphens in the standard positions and in any letter case, be
  stored as 12 upper-case characters without hyphens, and be returned as `CC-XXX-YY-NNNNN`.
- **FR-004**: ISRCs MUST be unique on the stored value; a duplicate MUST be refused as a conflict
  naming the ISRC field, including when two requests race, and nothing is stored.
- **FR-005**: The genre MUST be one of: Arabic Pop, Mahraganat, Khaleeji, Hip-Hop, Electronic, Rap,
  R&B, Rock.
- **FR-006**: A new track MUST start as draft.
- **FR-007**: The allowed status changes MUST be exactly draft → submitted, submitted → draft and
  submitted → distributed; distributed is final. A change to the current status or any other change
  MUST be refused as a conflict with an explanation; an unknown status value MUST be refused as a
  validation error. The track itself decides; no endpoint holds its own copy of the rules.
- **FR-008**: Concurrent status changes to the same track MUST NOT both succeed; the later one is
  refused as a conflict.
- **FR-009**: Anyone MUST be able to list tracks page by page (page default 1; page size default 20,
  1–50), filtered by artist, genre and/or status (all optional, combined with AND); each item shows
  identifier, title, artist identifier and name, ISRC (hyphenated), genre, status and release date;
  the response includes page, page size, total count and total pages.
- **FR-010**: Tracks MUST be listed by release date (newest first), then title ignoring case, then a
  stable tie-breaker.
- **FR-011**: Anyone MUST be able to read one track with all list fields plus creation time,
  last-change time, the statuses it can change to next, and its distributions (empty until Phase 4).
- **FR-012**: Unknown track identifiers MUST return "not found".
- **FR-013**: Every invalid field MUST produce its own message in the standard error format; invalid
  filter or paging values MUST be refused with a message naming the value.
- **FR-014**: Anyone MUST be able to search artists by part of the name (ignoring letter case) and read
  one artist by identifier; emails stay admin-only.
- **FR-015**: Created tracks MUST be returned with a reference to where they can be read; the same
  MUST hold for artists (that reference must work).
- **FR-016**: Titles MUST be stored and returned unchanged in full Unicode (as artist names in Phase 2).

### Key Entities

- **Track**: a recording in the catalog. Title (any script), artist (exactly one), ISRC (unique),
  genre (fixed list), release date (date only), status (draft, submitted, distributed), creation and
  last-change times.
- **Track status**: the workflow state with its allowed changes (draft ⇄ submitted → distributed).
- **Distribution** (shown only, Phase 4): a track's delivery to one platform; always empty here.
- **Artist** (Phase 2): now also findable by name and readable by identifier.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can add a track, including fixing one validation error, in under 1 minute.
- **SC-002**: 0 duplicate ISRCs exist after concurrent creation attempts with the same code written
  in different forms.
- **SC-003**: 100% of forbidden status changes (all 6 forbidden pairs plus same-status) are refused,
  and all 3 allowed ones succeed.
- **SC-004**: With 1,000 tracks, any filtered page is returned in under 1 second.
- **SC-005**: A distributed track is never returned to draft or submitted, also under concurrent
  changes.
- **SC-006**: Arabic and mixed-script titles round-trip unchanged through the real database.

## Assumptions

- Phase 4 adds distributions and the platform statuses; here the list is always empty (see "Status and distributions").
- Editing other track fields and deleting tracks are out of scope (Q2).
- Seed data (sample tracks) comes with Phase 5.
- One artist per track (the assignment's model); featured artists are out of scope.
- The genre list is fixed in this release; changing it needs a new release.
- Paging defaults as for artists (interview answer 9).
