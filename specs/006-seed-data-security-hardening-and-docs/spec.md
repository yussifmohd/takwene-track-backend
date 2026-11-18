# Feature Specification: Sample Data, Security Hardening and Documentation (API service)

**Feature Branch**: `006-seed-data-security-hardening-and-docs`

**Created**: 2026-11-17

**Status**: Draft

**Input**: User description: "Phase 5 – Seed data, security hardening and documentation. Idempotent seed on startup under the existing flag: ≥ 8 MENA artists (Egypt, UAE, Saudi Arabia, Lebanon and others; Arabic and Latin-script names), ≥ 6 international legends (The Beatles GB, Led Zeppelin GB, Michael Jackson US …, ISO codes), ≥ 30 tracks across all genres and all statuses with invented valid-format ISRCs, real song titles for the international artists but every ISRC, email (example.com only) and delivery clearly sample data; the 3 DSPs; deliveries in every state; every invariant respected; never overwrite real data; no admin password in the repo; README states it is sample data. Security review of both repos with a findings table (severity, fix, evidence) and fixes in scope: authorization on every write, over-posting, JWT, CORS allow-list, security headers (API and nginx), rate limiting beyond login, request size and input limits, error and log leaks, raw SQL, user enumeration, dependency vulnerabilities, Docker hardening, secrets in git history; no claim without evidence. Documentation: finished README, DECISIONS.md (backend, covering both repos) answering the three questions from the ai-log and the developer's notes, docs/study-guide.md, no broken links. End-to-end browser check on the full compose stack with the seed. Clean-clone build and test, CI green, complete .env.example, no dead code, submission checklist."

## Clarifications

### Session 2026-11-17 (Q1–Q5 and A1–A6 confirmed by the user, with the changes listed after Q5)

- Q1: When is sample data added, and how is real data protected? → A: Under the existing
  migrate-on-startup switch, **only into an empty catalogue** (no artists and no tracks). If any
  artist or track exists, the whole sample set is skipped and the log says why. Re-running on a
  seeded database changes nothing. No separate switch.
- Q2: Rate limiting beyond sign-in? → A: Two more limits, both configurable: every API request per
  client address (generous, against floods: 300 per minute), and every change (POST/PATCH) per
  signed-in admin (60 per minute). Health checks are not limited. Over the limit → 429 with
  `Retry-After`, like sign-in.
- Q3: (web app – see the frontend spec) development-tool vulnerabilities.
- Q4: (web app – see the frontend spec) the font served from Google.
- Q5: The "one thing the AI got wrong" in DECISIONS.md → A: The status rule that let a track be
  set to "distributed" without being sent to any platform (Phase 3 design, flagged in the
  developer's Phase 3 notes, changed by the developer in Phase 4). Alternative: the unreadable
  dropdowns found by the developer in manual testing.
- **User changes at the OK checkpoint**:
  - Every real artist–song pairing, country, genre and title must be true: verified by web search,
    sources cited in `data-model.md`; unverifiable pairings replaced. Everything else (ISRCs, emails,
    dates, statuses, deliveries) is clearly marked sample data.
  - No "Pop" genre: every sample song fits the existing fixed list truthfully (reasoning in `data-model.md`).
  - Q3: all 21 development-tool `npm audit` findings are listed in the security review and DECISIONS.md
    as known – fixed by removal or accepted – with the reason and the evidence that none ship to browsers.
  - Q4: the Readex Pro license file ships with the font; a real browser shows no request to Google.
  - Q5 wording: the assignment's PATCH wording invited it; the AI modelled "distributed" without recording
    which platforms; the developer caught it and had it changed in Phase 4.
  - A6: the measured sign-in timing numbers are recorded in the security review.
  - DECISIONS.md also records the technical and business decisions and the recommendations given
    since the start of the project (who proposed, who decided, why).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Someone who clones the repo sees a realistic catalogue (Priority: P1)

A reviewer starts the stack for the first time and immediately finds artists, tracks in every
status, and deliveries in every state, without typing anything in.

**Why this priority**: The product can only be judged with data in it; an empty catalogue hides the
filters, the statuses and the distribution views.

**Independent Test**: Start the stack on an empty database; list artists and tracks; open a
distributed track; restart; counts are unchanged.

**Acceptance Scenarios**:

1. **Given** an empty database and the startup switch on, **When** the API starts, **Then** the
   catalogue holds at least 14 artists (at least 8 from MENA countries including Egypt, the UAE,
   Saudi Arabia and Lebanon, some named in Arabic script; at least 6 international artists from the
   United Kingdom and the United States) and at least 30 tracks.
2. **Given** the sample catalogue, **When** tracks are filtered by each genre and each status,
   **Then** every genre and every status returns at least one track.
3. **Given** the sample catalogue, **When** distributed tracks are opened, **Then** each has at
   least one delivery, and across the catalogue there are pending, live and rejected deliveries on
   all three platforms.
4. **Given** a database that already holds the sample data, **When** the API restarts (any number
   of times), **Then** nothing is added, changed or duplicated.
5. **Given** a database that holds any artist or track not from the sample, **When** the API
   starts, **Then** no sample data is added and nothing existing is changed; the log says the
   sample was skipped because the catalogue is not empty.
6. **Given** the startup switch is off, **When** the API starts, **Then** no sample data is added.

---

### User Story 2 - Sample data is plainly sample data (Priority: P1)

**Why this priority**: Real artist names and song titles make the demo believable, but nothing may
look like a real catalogue record, contact or chart fact.

**Independent Test**: Read every seeded artist and track.

**Acceptance Scenarios**:

1. **Given** the sample catalogue, **Then** every artist email ends in `@example.com`.
2. **Given** the sample catalogue, **Then** every ISRC has the valid format but is invented
   (an invented `XX-SMP` prefix that marks it as sample), and no two collide.
3. **Given** the README, **Then** it states that all seed data is sample data, that ISRCs, emails,
   statuses and deliveries are invented, and how to start without it.
4. **Given** the repository, **Then** no admin password exists in any committed file.

---

### User Story 3 - The service resists common attacks (Priority: P1)

**Why this priority**: The assignment asks for a security review; the API is reachable from the
internet once deployed.

**Independent Test**: Run the security checks listed in the findings table; each row names the
evidence (test, command output or file).

**Acceptance Scenarios**:

1. **Given** any change endpoint, **When** called without a token or with a non-admin token,
   **Then** 401 / 403; a test proves it for every change route, and a new change route without the
   admin rule fails the test suite.
2. **Given** a request body with extra or server-owned fields (id, status, timestamps), **When** it
   is sent to a create or change endpoint, **Then** those fields are ignored or refused; they never
   reach the stored data.
3. **Given** a client that sends more than the allowed requests or changes per minute, **When** the
   limit is exceeded, **Then** 429 with `Retry-After`; health checks keep answering.
4. **Given** any API response, **Then** it carries security headers that stop content sniffing,
   framing and referrer leaks, and responses with personal data or tokens are not cached.
5. **Given** a cross-origin request from a site not on the configured list, **Then** the browser is
   not allowed to read the response.
6. **Given** an unexpected error, **Then** the client sees a generic ProblemDetails with a trace id;
   no stack trace, SQL, connection string or internal type name; the log holds the details but never
   passwords, tokens or the signing key.
7. **Given** a wrong email or a wrong password at sign-in, **Then** the answer and its timing do not
   reveal whether the account exists.
8. **Given** the dependency and image scans, **Then** no known vulnerability in shipped code is
   left unexplained; each finding is fixed or listed with the reason it is accepted.
9. **Given** the full git history of both repositories, **Then** a secret scan finds no committed
   secret (or each finding is shown to be a placeholder or test value).

---

### User Story 4 - An evaluator understands the project and how it was built (Priority: P1)

**Why this priority**: The README, DECISIONS.md and the AI-usage story are graded deliverables.

**Acceptance Scenarios**:

1. **Given** the backend README, **Then** it follows the README rule (what, stack, quick start with
   and without Docker, configuration, migrations, obtaining a JWT, tests, CI and branch protection,
   API overview, structure, link to the web app repo, walkthrough and DECISIONS.md links) and every
   link resolves.
2. **Given** DECISIONS.md, **Then** it answers exactly the three questions: (1) what the AI generated
   and what the developer wrote or changed, (2) security issues found or introduced and how they were
   handled, (3) one real, specific thing the AI got wrong that the developer fixed and why it was
   wrong; every statement traces to `docs/ai-log.md` or the developer's notes. It also records the
   technical and business decisions and recommendations since the start of the project.
3. **Given** `docs/study-guide.md`, **Then** it explains the architecture in plain language, has a
   glossary (CQRS, Result pattern, Unit of Work, Specification, ProblemDetails, rate limiting, JWT),
   20 likely evaluator questions with short honest answers, and what was AI-generated versus changed
   by the developer.

---

### User Story 5 - The submission works from a clean clone (Priority: P2)

**Acceptance Scenarios**:

1. **Given** a fresh clone in an empty folder, **When** the documented build and test commands run,
   **Then** they succeed without files that exist only on the developer's machine.
2. **Given** `.env.example`, **Then** it lists every setting the compose stack needs, with safe
   placeholders and no real secret.
3. **Given** the full stack with the sample data, **When** the end-to-end browser check runs (sign
   in, filter, detail, add artist, add track, distribute, record answers, sign out), **Then** every
   step works or the failure is reported.

### Edge Cases

- The database already has the admin account but no catalogue → sample data is added (the admin
  account is not catalogue data).
- The sample run fails halfway (e.g. database restarts) → nothing partial stays; the next start
  tries again.
- Two API instances start together against an empty database → the sample is added once; the
  other instance does not fail startup.
- Someone deletes some sample tracks but keeps others → the catalogue is not empty, nothing is
  re-added.
- An ISRC typed by a user equals a sample ISRC → refused as a duplicate (the normal rule); sample
  ISRCs use the invented `XX-SMP` prefix, so this only happens on purpose.
- Rate limits behind the web app's proxy → limits apply per real client address (trusted proxy
  rules from sign-in), not per proxy.

## Requirements *(mandatory)*

### Functional Requirements

**Sample data**

- **FR-001**: On startup with the migrate-on-startup switch on, the system MUST add the sample
  catalogue if, and only if, the catalogue has no artists and no tracks.
- **FR-002**: The sample MUST contain at least 8 MENA artists (Egypt, UAE, Saudi Arabia, Lebanon and
  at least one more country; Arabic-script and Latin-script names) and at least 6 international
  artists (United Kingdom as `GB`, United States as `US`), each with an `@example.com` email.
- **FR-003**: The sample MUST contain at least 30 tracks covering every genre and every status, with
  invented ISRCs in the valid format. Artist names, countries, song titles, artist–song pairings and
  genres MUST be true (verified, sources in `data-model.md`); ISRCs, emails, dates, statuses and
  deliveries are invented.
- **FR-004**: The sample MUST contain deliveries on all three platforms in every state (pending, live,
  rejected), and MUST respect every rule of the product: each distributed track has at least one
  delivery, draft tracks have none, statuses are reachable by allowed moves, answers come after sends.
- **FR-005**: Adding the sample MUST be all-or-nothing and safe when several instances start together.
- **FR-006**: The sample MUST NOT create, change or reveal the admin account or its password (tested).

**Security**

- **FR-007**: Every change endpoint MUST require the admin role, enforced by a test that covers every
  registered change route.
- **FR-008**: Request bodies MUST bind only to explicit request types; server-owned fields cannot be set
  by clients.
- **FR-009**: The service MUST limit requests per client address and changes per admin (configurable),
  answering 429 with `Retry-After`; health checks are exempt.
- **FR-010**: Every API response MUST carry security headers (no sniffing, no framing, no referrer, a
  restrictive content policy for API responses; API docs in development keep working); token and
  personal-data responses MUST NOT be cached.
- **FR-011**: Cross-origin access MUST be limited to the configured list of web app origins.
- **FR-012**: Error responses MUST NOT leak internals; logs MUST NOT contain passwords, tokens, the
  signing key or connection strings.
- **FR-013**: Sign-in MUST NOT reveal whether an email has an account (same answer; comparable timing).
- **FR-014**: The security review MUST be published as a findings table (area, finding, severity, fix
  or reason accepted, evidence) in the repository, covering both repos.

**Documentation and delivery**

- **FR-015**: README, DECISIONS.md and docs/study-guide.md MUST exist as described in User Story 4,
  with no broken links.
- **FR-016**: `.env.example` MUST list every compose setting; the README MUST say all seed data is
  sample data.
- **FR-017**: A clean clone MUST build and pass tests with the documented commands; CI MUST be green.

### Key Entities

- **Sample catalogue**: a fixed set of artists, tracks and deliveries, recognisable by its reserved
  invented ISRC registrant and `@example.com` emails.
- **Security finding**: area, description, severity, status (fixed / accepted), evidence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A first start on an empty database shows ≥ 14 artists, ≥ 30 tracks, every genre and
  status, and deliveries in all three states on all three platforms; a second start changes nothing.
- **SC-002**: 100% of change routes are proven admin-only by an automated test.
- **SC-003**: Every row of the findings table has evidence that someone else can re-run.
- **SC-004**: 0 broken links in README, DECISIONS.md and the study guide.
- **SC-005**: A fresh clone builds and passes all tests on the first try with the documented commands.

## Assumptions

- "Real data" means any artist or track in the database that the sample did not add; the check is
  "catalogue empty", not row-by-row matching.
- HTTPS is not available without a domain (AWS decision), so HSTS is documented but not enabled.
- The security review covers this code and its dependencies, not GitHub account settings.
- Real song titles are factual references for a demo; release dates, ISRCs and statuses are invented
  and labelled as sample data.
