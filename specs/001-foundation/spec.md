# Feature Specification: Foundation & Architecture (API service)

**Feature Branch**: `001-foundation`

**Created**: 2026-11-17

**Status**: Draft

**Input**: User description: "Phase 0 – Foundation & Architecture. Scaffold the API service with no business features: layered architecture, health status, API documentation, consistent error responses, automated tests, automated checks on pull requests, one-command local startup with a database, and the project documentation skeleton. Proof of life: the stack starts with one command and the web app can show the API health status."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Start the API locally with one command (Priority: P1)

A reviewer clones the API repository, copies the example environment file and starts the API
together with its database using a single command. Within a few minutes the API reports itself
healthy, and the companion web app (started separately from its own repository) can reach it.

**Why this priority**: Every later phase and the final evaluation depend on reviewers being able to
run the system without manual setup. Nothing else is demonstrable without it.

**Independent Test**: On a clean machine with only the documented prerequisites, follow the README,
run the single start command and request the health status; it reports healthy.

**Acceptance Scenarios**:

1. **Given** a fresh clone and the example environment file copied, **When** the reviewer runs the
   documented start command, **Then** the database and API start, the database schema is prepared
   automatically, and the health status reports healthy within 3 minutes.
2. **Given** the stack is running, **When** the database becomes unavailable, **Then** the health
   status reports unhealthy instead of healthy.
3. **Given** the stack is running, **When** the companion web app's stack is started on the shared
   local network, **Then** the web app can reach the API by its service name.

---

### User Story 2 - Pull requests are checked automatically (Priority: P1)

When the developer opens a pull request against the main branch, the repository automatically
builds the service and runs the full automated test suite, and publishes the test results so the
reviewer can see them on the pull request.

**Why this priority**: The assignment is graded partly on a clean, trustworthy history; automated
checks are the evidence that each phase works.

**Independent Test**: Open a pull request containing a deliberately failing test; the check fails.
Remove it; the check passes and test results are downloadable from the run.

**Acceptance Scenarios**:

1. **Given** a pull request targeting main, **When** it is opened or updated, **Then** a check runs
   build and tests and reports pass/fail on the pull request.
2. **Given** the check has run, **When** the reviewer opens the run, **Then** test result and
   coverage files are available to download.
3. **Given** the README, **When** the repository owner follows the documented steps, **Then** the
   check can be made mandatory before merging.

---

### User Story 3 - Explore the API through interactive documentation (Priority: P2)

A developer running the API in development mode opens an interactive API reference, sees every
available operation, and can enter an access token once to try protected operations. In
non-development environments the reference is not publicly exposed.

**Why this priority**: Speeds up every following phase and lets reviewers explore without extra
tools, but the system still works without it.

**Independent Test**: Start in development mode, open the documented reference URL, confirm the
health operation is listed and a token input is offered; start in production mode and confirm the
reference URL is not available.

**Acceptance Scenarios**:

1. **Given** development mode, **When** the developer opens the reference URL, **Then** all
   operations are listed with their request/response shapes and a bearer-token input is available.
2. **Given** a non-development environment, **When** anyone requests the reference URL or the raw
   API description, **Then** it is not served.
3. **Given** any phase, **When** the plan is produced, **Then** the machine-readable API description
   is exported into that phase's spec folder for the web app to consume.

---

### User Story 4 - Errors are consistent and safe (Priority: P2)

Any client calling the API receives errors in one standard, machine-readable format, whether the
error is a validation problem, a missing resource, a conflict or an unexpected failure. Unexpected
failures never reveal internal details.

**Why this priority**: The web app maps these errors to forms and notifications in every later
phase; defining the format now prevents rework.

**Independent Test**: Call an unknown route and a deliberately failing diagnostic path in tests;
both return the standard error format with a correlation identifier and no internal details.

**Acceptance Scenarios**:

1. **Given** any request that fails unexpectedly, **When** the API responds, **Then** the response
   uses the standard problem format with status 500, a generic message and a trace identifier, and
   the full error is written to the server log only.
2. **Given** a request that fails validation, **When** the API responds, **Then** the response
   lists each invalid field with a human-readable message.

---

### Edge Cases

- The database starts slower than the API: the API waits/retries rather than crashing permanently,
  and the health status reports unhealthy until the database is reachable.
- Schema preparation fails at startup: the API fails loudly (non-zero exit, clear log) instead of
  running against a broken schema.
- Required secrets (e.g. token signing key) are missing or too short: the API refuses to start
  with a clear message.
- The shared local network does not exist yet: the documented command creates or reuses it.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The service MUST expose an anonymous health status that reports healthy only when the
  service and its database are reachable.
- **FR-002**: The repository MUST start the service and its database with one documented command,
  preparing the database schema automatically when enabled by configuration (enabled for local use).
- **FR-003**: The service MUST read every secret (database credentials, token signing key, initial
  admin credentials) from configuration or environment; no real secret may be committed, and an
  example environment file with placeholders MUST be provided.
- **FR-004**: The service MUST refuse to start when a required secret is missing or invalid.
- **FR-005**: All error responses MUST use one standard problem format with status, title, detail
  and a trace identifier; unexpected errors MUST NOT expose internal details.
- **FR-006**: In development mode the service MUST serve interactive API documentation with
  bearer-token support; outside development it MUST NOT be publicly served.
- **FR-007**: The repository MUST run build and tests automatically on every pull request to main
  and publish test results and coverage as downloadable artifacts.
- **FR-008**: The repository MUST include an automated test suite skeleton covering the shared
  building blocks introduced in this phase (request pipeline, result-to-response mapping).
- **FR-009**: The repository MUST pin its toolchain versions so every machine and the automated
  checks build with the same versions.
- **FR-010**: The repository MUST contain a README skeleton (prerequisites, run with/without
  containers, database migrations, how to obtain an access token – placeholder until Phase 1,
  tests, CI), an AI-usage log, and a link to the companion web repository.
- **FR-011**: The service container MUST run as a non-root user and declare a health check.
- **FR-012**: Cross-origin access MUST be limited to an explicit allow-list of web origins from configuration.

### Key Entities

- None. This phase introduces no business data. (Business entities arrive in Phases 1–4.)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A reviewer with only the documented prerequisites gets a healthy service in under
  5 minutes and at most 3 commands from a fresh clone.
- **SC-002**: 100% of pull requests to main show an automated build-and-test result before merge.
- **SC-003**: 100% of error responses in the test suite use the standard problem format.
- **SC-004**: Zero secrets are present in the repository history (verified by review before the PR).
- **SC-005**: The automated check completes in under 10 minutes.

## Assumptions

- Reviewers have the documented container runtime and SDK installed; no cloud account is needed for Phase 0.
- The companion web app lives in a separate repository and joins the same local network.
- Business endpoints, authentication flows and seed data are out of scope for this phase
  (Phases 1–5); the access-token input in the documentation is wired now but used from Phase 1.
- "Development mode" is the local/container default; production-like settings are used on AWS (Phase 6).
