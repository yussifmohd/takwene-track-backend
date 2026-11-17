# Data Model: Admin Sign-in & Access Control

## ApplicationUser (table `users`, Identity)

| Field | Type | Rules |
|---|---|---|
| id | uuid | PK, generated in code |
| email / normalized_email | varchar(256) | required, unique (normalised upper-case) |
| user_name / normalized_user_name | varchar(256) | = email |
| password_hash | text | Identity PBKDF2 hash; never logged or returned |
| security_stamp, concurrency_stamp | text | Identity |
| lockout_enabled | bool | true |
| lockout_end | timestamptz? | set for 15 min after 5 failures |
| access_failed_count | int | reset on success |
| email_confirmed | bool | true for the seeded admin (no confirmation flow) |

## Role (table `roles`) / UserRole (table `user_roles`)

- One role: `Admin` (constant `Roles.Admin`). Seeded admin is its only member.
- Other Identity tables (`user_claims`, `user_logins`, `user_tokens`, `role_claims`) exist but are unused.

## Access token (not stored)

| Claim | Value |
|---|---|
| `sub` | user id |
| `email` | user email |
| `role` | `Admin` (one claim per role) |
| `jti` | random id |
| `iat`, `nbf`, `exp` | issue time, not-before, issue + 60 min |
| `iss`, `aud` | `Jwt:Issuer`, `Jwt:Audience` |

## DTOs

- `LoginRequest { email: string (≤256, email), password: string (≤128) }`
- `LoginResponse { accessToken: string, tokenType: "Bearer", expiresAt: date-time, user: CurrentUserDto }`
- `CurrentUserDto { email: string, roles: string[], expiresAt: date-time }` (`/me`; in the login response `expiresAt` equals the token's)

## State transitions (lockout)

```
active --(wrong password, count < 5)--> active (count+1)
active --(5th consecutive failure)--> locked (lockout_end = now + 15 min, count = 0)
locked --(any attempt before lockout_end)--> locked (generic 401)
locked --(lockout_end passed)--> active
active --(success)--> active (count = 0)
```
