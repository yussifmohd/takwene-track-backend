# Feature Specification: Admin Sign-in & Access Control (API service)

**Feature Branch**: `002-jwt-authentication-and-authorization`

**Created**: 2026-11-17

**Status**: Draft

**Input**: User description: "Phase 1 – JWT authentication and authorization. Reads are public; writes are Admin-only; no self-registration. One admin account is created from configuration/environment; its password is never committed (only in the local environment file, with a placeholder in the example file). Admins sign in with email and password and receive a 60-minute access token validated for issuer, audience and signing key. Failed sign-ins return one generic error that does not reveal whether the email exists. Repeated failures lock the account temporarily and sign-in attempts are rate limited."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin signs in (Priority: P1)

The catalog administrator signs in with their email and password and receives an access token
that proves who they are for the next 60 minutes, together with when it expires and who they are
signed in as.

**Why this priority**: Every write in later phases (artists, tracks, distribution) requires a
signed-in admin. Without sign-in nothing beyond reading is possible.

**Independent Test**: With the stack running and the admin configured, sign in with the configured
credentials and receive a token; use it to ask "who am I" and get the admin's email and role back.

**Acceptance Scenarios**:

1. **Given** the configured admin account, **When** the admin signs in with the correct email and
   password, **Then** the response contains an access token, its expiry time (60 minutes from now)
   and the admin's email and role.
2. **Given** a valid token, **When** the admin asks for their own account details, **Then** the
   email and role are returned.
3. **Given** the email is entered in a different letter case or with surrounding spaces, **When**
   the admin signs in, **Then** sign-in succeeds.

---

### User Story 2 - Failed sign-ins reveal nothing and are throttled (Priority: P1)

Anyone trying wrong credentials gets the same response whether or not the email belongs to an
account. Repeated wrong passwords lock that account for a while, and too many attempts from one
client in a short time are refused, so passwords cannot be guessed at speed.

**Why this priority**: The admin account controls the whole catalog; guessing it must be slow and
must not even confirm which emails exist.

**Independent Test**: Compare the responses for an unknown email and for a known email with a
wrong password (same status, same body apart from trace identifiers). Fail repeatedly and observe
lockout; send attempts quickly and observe throttling.

**Acceptance Scenarios**:

1. **Given** an email with no account, **When** someone signs in, **Then** the response is the
   generic "invalid email or password" error.
2. **Given** the admin email with a wrong password, **When** someone signs in, **Then** the
   response is identical to scenario 1.
3. **Given** 5 consecutive wrong passwords for the admin account, **When** the correct password is
   then used within the lockout period, **Then** sign-in is refused with the same generic error,
   and after the lockout period the correct password works again.
4. **Given** one client has made the maximum number of sign-in attempts in the current window,
   **When** it tries again, **Then** it is told to slow down, including when it may retry, and no
   credential check is performed.
5. **Given** a sign-in request with a missing or malformed email or a missing password, **When**
   it is sent, **Then** a validation error lists the offending fields.

---

### User Story 3 - Only admins can change data; anyone can read (Priority: P1)

Reading the catalog never requires signing in. Every operation that changes data requires a valid,
unexpired admin token; otherwise it is refused with a clear "not signed in" or "not allowed" error.

**Why this priority**: This is the access model every later phase builds on.

**Independent Test**: Call a protected operation without a token, with an expired token, with a
token signed by a different key, with a token for the wrong audience or issuer, and with a valid
admin token; only the last succeeds. Call a read operation without a token; it succeeds.

**Acceptance Scenarios**:

1. **Given** no token, **When** a write operation is called, **Then** it is refused as "not signed in".
2. **Given** a token that is expired, tampered with, signed with another key, or issued for another
   issuer or audience, **When** any protected operation is called, **Then** it is refused as
   "not signed in".
3. **Given** a valid token for a signed-in user without the admin role, **When** a write
   operation is called, **Then** it is refused as "not allowed".
4. **Given** no token, **When** the health status or any read operation is called, **Then** it succeeds.

---

### User Story 4 - The admin account is provisioned from configuration (Priority: P1)

The person running the system sets the admin email and password in their local environment file
before the first start. The system creates that admin account on startup if it does not exist.
The password never appears in the repository; the example environment file only has a placeholder.

**Why this priority**: There is no self-registration, so this is the only way an admin exists.

**Independent Test**: Start a fresh stack with the admin email/password set: sign-in works. Start
with the password missing: startup fails with a clear message. Restart with a different password
in the environment: the existing account is not changed.

**Acceptance Scenarios**:

1. **Given** a fresh database and admin credentials in the environment, **When** the system starts,
   **Then** exactly one admin account exists and can sign in.
2. **Given** the admin account already exists, **When** the system restarts, **Then** no duplicate
   is created and the existing password is left unchanged.
3. **Given** account provisioning is enabled but the admin email or password is missing or the
   password is too weak, **When** the system starts, **Then** it refuses to start and says which
   setting is wrong (without printing the password).

---

### Edge Cases

- A token presented after its 60 minutes is refused, with only a small tolerance for clock
  differences between machines.
- Signing out is done by the client discarding its token; a copied token stays valid until it
  expires (no server-side revocation in this phase, documented as a known trade-off).
- The account is locked: sign-in with the correct password is refused generically; the lock clears
  by itself after the lockout period.
- Sign-in requests arriving through the web app's proxy are throttled per original client, not as a
  single client.
- Very long email or password inputs are rejected by validation, not processed.
- Passwords and tokens never appear in logs or error responses.
- There is no way to create additional accounts through the API.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a sign-in operation accepting an email and password and, on
  success, returning an access token, its absolute expiry time, and the signed-in user's email and roles.
- **FR-002**: Access tokens MUST expire 60 minutes after issue and MUST identify their issuer,
  intended audience and the user's role(s).
- **FR-003**: Every protected operation MUST reject tokens that are missing, expired, not yet
  valid, signed with a different key or algorithm, or issued for another issuer or audience.
- **FR-004**: Failed sign-ins (unknown email, wrong password, locked account) MUST return the same
  status and the same message, so callers cannot tell which emails have accounts.
- **FR-005**: After 5 consecutive failed sign-ins, the account MUST be locked for 15 minutes; a
  successful sign-in resets the failure count.
- **FR-006**: Sign-in attempts MUST be limited per client (10 per minute); excess attempts MUST be
  refused with a "too many requests" error stating when to retry, before credentials are checked.
- **FR-007**: Sign-in input MUST be validated (email required, valid format, ≤ 256 characters;
  password required, ≤ 128 characters) and invalid input reported per field.
- **FR-008**: The system MUST offer a "current user" operation that, for a valid token, returns the
  user's email and roles.
- **FR-009**: All read operations and the health status MUST remain available without signing in.
- **FR-010**: All operations that change data MUST require the admin role; the rule MUST be
  defined once and applied to every write operation added in later phases.
- **FR-011**: Unauthenticated requests to protected operations MUST receive a "not signed in"
  error and authenticated users lacking the role a "not allowed" error, both in the standard
  error format used by the rest of the API.
- **FR-012**: There MUST be no operation for creating accounts or registering.
- **FR-013**: On startup, when account provisioning is enabled, the system MUST create the admin
  account from configuration if it does not exist, MUST NOT change an existing account, and MUST
  refuse to start if the admin email or password is missing or does not meet the password rules.
- **FR-014**: Admin passwords MUST be at least 12 characters with upper-case, lower-case, digit and
  symbol, and MUST be stored only as a salted hash.
- **FR-015**: The admin password and token signing key MUST come only from the environment or
  local secret storage; committed files contain placeholders only.
- **FR-016**: Passwords, tokens and signing keys MUST never be written to logs or error responses;
  sign-in successes and failures MUST be logged without the password.
- **FR-017**: The interactive API documentation MUST let a reader authorise with a token and try
  protected operations.

### Key Entities

- **User account**: email (unique, case-insensitive), password hash, roles, failed-attempt count,
  lockout end. Exactly one exists in this phase: the admin.
- **Role**: named permission set; only "Admin" exists.
- **Access token**: short-lived signed statement of who the user is and their roles; not stored by
  the server.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The admin can sign in and receive a token in under 1 second on a local machine.
- **SC-002**: 100% of tested invalid-token variants (missing, expired, wrong key, wrong issuer,
  wrong audience, tampered) are refused; 100% of read operations work without a token.
- **SC-003**: Responses for "unknown email" and "wrong password" are indistinguishable in status
  and body (apart from per-request trace identifiers).
- **SC-004**: After 5 wrong passwords, 0 sign-ins succeed for 15 minutes; after 10 attempts in a
  minute from one client, further attempts are refused until the window resets.
- **SC-005**: A fresh clone started with the documented environment values has a working admin
  sign-in with no manual database steps; the repository contains no real password or signing key.

## Assumptions

- No refresh tokens; the admin signs in again after 60 minutes (interview decision).
- No email confirmation, password reset or account management UI in this phase.
- One admin is enough for the take-home; more admins could be added later through configuration
  or a future management feature.
- Throttling is per client address as seen by the API after trusting the web app's proxy.
- Clock tolerance for token expiry is 30 seconds.
