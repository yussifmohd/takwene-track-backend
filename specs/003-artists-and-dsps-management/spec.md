# Feature Specification: Artists & Distribution Platforms (API service)

**Feature Branch**: `003-artists-and-dsps-management`

**Created**: 2026-11-17

**Status**: Draft

**Input**: User description: "Phase 2 – Artists and DSPs. Artist and DSP records with a migration; create an artist (Admin-only) and list artists (public, paginated); list DSPs (public); the 3 DSPs seeded (Spotify, Apple Music, YouTube Music). Validation: name and country required, email format, unique email, country as an ISO 3166-1 alpha-2 code, input length limits. Include the Unicode work from the Phase 0 analysis: UTF-8 database setup and a round-trip test that stores and reads back Arabic text unchanged. Admin-only writes use the policy defined in Phase 1."

## Clarifications

### Session 2026-11-17 (recommended defaults, pending the user's confirmation at the analyze checkpoint)

- Q: Is an artist's email required? → A: No. Name and country are required; email is optional, and when given it must be a valid address that no other artist uses (compared ignoring letter case and surrounding spaces).
- Q: Who may see an artist's email? → A: Only a signed-in admin. The public artist list omits email addresses (artists are real people; reads are public).
- Q: What does a duplicate email return? → A: A "conflict" error that names the email field, so the form can show the message next to it. The same applies when two creations race.
- Q: In which order are artists listed, and what happens with bad paging values? → A: By name, ignoring letter case, then oldest first for equal names. A page number below 1 or a page size outside 1–50 is rejected with a validation error; a page past the end returns an empty page with the correct totals.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin adds an artist (Priority: P1)

The signed-in admin adds an artist with a name, a country and optionally a contact email, so the
artist can later be credited on tracks.

**Why this priority**: Tracks (Phase 3) belong to artists; without artists no track can be created.

**Independent Test**: With an admin token, create an artist and see it returned with an
identifier; without a token or with a non-admin token the request is refused.

**Acceptance Scenarios**:

1. **Given** a signed-in admin, **When** they create an artist with name "Amr Diab", country "EG"
   and email "booking@example.com", **Then** the artist is created and returned with its
   identifier, name, country "EG", email and creation time.
2. **Given** a signed-in admin, **When** they create an artist with country "eg", **Then** the
   country is stored and returned as "EG".
3. **Given** an artist already uses "booking@example.com", **When** the admin creates another
   artist with " Booking@Example.com ", **Then** creation is refused with a conflict that names
   the email field, and nothing is stored.
4. **Given** a missing name, a missing country, a country that is not an officially assigned
   two-letter code (e.g. "XX", "UK", "EGY"), a malformed email, or a value longer than its limit,
   **When** the admin submits, **Then** creation is refused with a message per invalid field and
   nothing is stored.
5. **Given** no token, an invalid/expired token, or a token without the Admin role, **When**
   anyone tries to create an artist, **Then** the request is refused (not signed in / not
   allowed) and nothing is stored.

---

### User Story 2 - Anyone browses the artists page by page (Priority: P1)

Any visitor can list artists a page at a time with the total count, so the catalog can be browsed
without signing in.

**Why this priority**: Reads are public by design; the web app's artist list and the track form
(Phase 3) depend on it.

**Independent Test**: Seed more artists than one page holds; request page 1 and page 2 and check
the items, order and totals; request without a token.

**Acceptance Scenarios**:

1. **Given** 25 artists, **When** anyone requests the first page with default settings, **Then**
   20 artists are returned, ordered by name ignoring case, with total 25 and 2 pages.
2. **Given** 25 artists, **When** page 2 is requested, **Then** the remaining 5 are returned.
3. **Given** a page past the end, **When** it is requested, **Then** an empty list is returned with
   the correct totals.
4. **Given** page 0 or a page size of 0 or 51, **When** requested, **Then** the request is refused
   with a validation message naming the value; non-numeric values are refused as a malformed
   request.
5. **Given** no token, **When** the list is requested, **Then** artists are returned without email
   addresses; **Given** an admin token, **Then** email addresses are included.

---

### User Story 3 - Names in Arabic and other scripts are kept exactly (Priority: P1)

Artist names entered in Arabic, Latin or a mix (with diacritics or emoji) come back exactly as
entered, and the system refuses to run against storage that cannot hold such text.

**Why this priority**: Takwene distributes MENA music; Arabic names are the normal case, and a
silently corrupted name would be published to platforms under the wrong name.

**Independent Test**: Create artists named "عمرو دياب", "Nancy عجرم", "فيروز ✨" and
"محمد عبده (مُحمّد)" against the real database engine and read them back byte-for-byte.

**Acceptance Scenarios**:

1. **Given** the real database, **When** an artist named "عمرو دياب" is created and listed,
   **Then** the returned name is identical to the input (same characters, same order, no
   normalisation beyond trimming outer spaces).
2. **Given** a name mixing Arabic, Latin, diacritics and emoji, **When** it is stored and read back,
   **Then** it is identical.
3. **Given** a database whose text encoding is not UTF-8, **When** the service starts, **Then** it
   refuses to start and names the problem.
4. **Given** the length limit for names, **When** an Arabic name is exactly at the limit, **Then** it
   is accepted (limits count characters, not bytes).

---

### User Story 4 - Anyone lists the distribution platforms (Priority: P2)

Any visitor can list the music platforms tracks are distributed to: Spotify, Apple Music and
YouTube Music.

**Why this priority**: Distribution (Phase 4) sends tracks to these platforms; the list must exist
and be stable first, but nothing in this phase changes it.

**Independent Test**: On a fresh stack, list the platforms and get exactly the three, each with a
stable identifier; restart and get the same identifiers.

**Acceptance Scenarios**:

1. **Given** a fresh installation, **When** anyone lists the platforms, **Then** exactly Apple
   Music, Spotify and YouTube Music are returned (ordered by name), each with an identifier.
2. **Given** the service restarts (or is deployed to another environment), **When** the platforms
   are listed, **Then** they are not duplicated and keep the same identifiers.

---

### Edge Cases

- Name of only spaces → "name is required". Surrounding spaces are removed; inner spaces kept.
- Country with spaces or lower case (" eg ") → accepted as "EG". Codes that are reserved, user-assigned
  or not country codes ("UK", "EU", "XK") → refused.
- Two artists may share a name (different people); only email must be unique.
- Email given as an empty string → treated as "no email".
- Concurrent creation of two artists with the same email → exactly one succeeds; the other gets the
  conflict.
- Unknown fields in the request body are ignored; wrong types (e.g. a number for the name) →
  validation error, not a server error.
- Request body far larger than the field limits → refused without being processed.
- Malformed request data (invalid JSON, wrong types) → refused as a malformed request in every
  environment, never as a server error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let only a signed-in admin create an artist (Phase 1 Admin rule);
  anyone else is refused as not signed in or not allowed.
- **FR-002**: An artist MUST have a name: required, outer whitespace removed, 1–200 characters,
  any script.
- **FR-003**: An artist MUST have a country: an officially assigned ISO 3166-1 alpha-2 code,
  accepted in any letter case with outer spaces removed, stored and returned in upper case.
- **FR-004**: An artist MAY have an email: at most 254 characters, a valid address, unique across
  artists ignoring letter case and outer spaces; an empty value means "no email".
- **FR-005**: A duplicate email MUST be refused as a conflict naming the email field, including when
  two requests race; nothing is stored.
- **FR-006**: Each invalid field MUST produce its own message in the standard error format; no
  data is stored when any field is invalid.
- **FR-007**: A created artist MUST be returned with identifier, name, country, email (if any) and
  creation time, with a reference to where it can be read.
- **FR-008**: Anyone MUST be able to list artists page by page: page (default 1, at least 1) and
  page size (default 20, 1–50); the response includes the items, page, page size, total count and
  total pages. Invalid paging values are refused with a validation message.
- **FR-009**: Artists MUST be listed by name ignoring letter case, then by creation time.
- **FR-010**: Artist email addresses MUST only be returned to a signed-in admin.
- **FR-011**: Anyone MUST be able to list the distribution platforms (identifier and name),
  ordered by name.
- **FR-012**: Exactly three platforms MUST exist once the database is set up: Spotify, Apple Music
  and YouTube Music, with identifiers that are the same in every environment; repeated setups and
  restarts MUST never duplicate them.
- **FR-013**: Text MUST be stored and returned unchanged in full Unicode, including Arabic script
  (closes Phase 0 FR-013); length limits count characters.
- **FR-014**: The service MUST refuse to start when the database's text encoding is not UTF-8.
- **FR-015**: Requests whose body exceeds a small fixed size for this kind of data MUST be refused
  without being processed.

### Key Entities

- **Artist**: a performer credited on tracks. Name (any script), country (ISO alpha-2), optional
  unique contact email, creation and last-update times.
- **Distribution platform (DSP)**: a music service tracks are delivered to. Name and a stable
  identifier; fixed set of three in this release.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can add an artist in under 1 minute, including fixing one validation error.
- **SC-002**: 100% of the Arabic and mixed-script sample names round-trip unchanged through the real
  database engine.
- **SC-003**: With 1,000 artists stored, any page of the list is returned in under 1 second.
- **SC-004**: 0 duplicate emails exist after concurrent creation attempts with the same address.
- **SC-005**: Exactly 3 platforms exist after any number of restarts, with unchanged identifiers.
- **SC-006**: 0 email addresses appear in artist lists returned to visitors who are not signed in.

## Assumptions

- Editing and deleting artists, artist detail pages and search are out of scope for this phase
  (not requested); the list and create operations are what the web app and Phase 3 need.
- Platforms are fixed reference data: no create/edit/delete for platforms.
- Artist fields are limited to name, country and email; biography, images and genres are out of scope
  (genres belong to tracks).
- Country names are presented by the web app; the API stores and returns only the code.
- The paging defaults (20, max 50) come from the interview (answer 9).
