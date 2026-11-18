# Decisions and AI usage

Covers **both repositories**: the API ([takwene-track-backend](https://github.com/yussifmohd/takwene-track-backend))
and the web app ([takwene-track-frontend](https://github.com/yussifmohd/takwene-track-frontend)).

I directed this project. I chose the technology stack, the architecture and the way of working, and I
reviewed and approved every plan before any code was written. Claude Code did the implementation under
that direction.

*Voice: in this opening section "I" is me, Yousef, the developer. The sections and the decision
record below are written in the third person: "the developer" is me, "the AI" is Claude Code.*

### Choices I made
- **Spec Kit with Memory.md**: every phase starts from a reviewed spec, plan and task list, and Memory.md carries decisions between sessions.
- **Two repositories, one branch per phase, backend and frontend built together in each phase**: each phase ships a complete feature end to end and is reviewed as one unit.
- **.NET 10 Minimal APIs with MapGroup**: one endpoint group per feature, without controller ceremony.
- **Clean Architecture**: business rules stay in the domain, independent of the database and HTTP.
- **CQRS**: changes and reads take separate, simpler paths.
- **Ardalis Result, Repository and Specification**: expected outcomes without exceptions, and reusable, named queries.
- **MediatR with a Unit of Work**: one save per command, and queries can never write.
- **React with zustand, React Query, shadcn/ui and Tailwind**: a small session store, cached server data, and accessible components owned in the repo.
- **Skeleton loaders**: every loading state shows the shape of the content instead of a spinner.
- **Unit tests with CI on pull requests**: nothing is merged without a green run.
- **Docker**: one command starts each repo's stack the same way on any machine.
- **AWS design with a load balancer and at least two EC2 instances**: no single server is a point of failure. The deployment scripts are a separate, optional phase that is not built yet.

### Phase planning
- My master prompt gave Claude a reference list of seven phases: foundation and architecture, authentication and authorization, artists and platforms, tracks, distribution, hardening and docs, and AWS. Claude could adjust it with a reason.
- Claude proposed the final breakdown: the same seven phases, each with a branch per repository. I approved it.
- Before any implementation, I reviewed each phase's spec, plan and task list.

### Proposals I approved or changed
- Claude proposed the database (PostgreSQL with snake_case, the reference project's provider, as my prompt required), authentication and roles, and the ISRC and status rules. I approved them with these changes:
  - **No register endpoint, no viewer role**: one admin from configuration is all a distributor's back office needs.
  - **Artist email required**: the assignment's Artist entity has it.
  - **No data migration for old local databases**: only test data existed, so resetting was simpler and safer.
  - **No fixed limit of three platforms**: platforms are data, so up to 20 distinct ids per request.
  - **PATCH to "distributed" refused**: a distributed track must record where it was delivered.
  - **Scalar instead of Swagger**: in my prompt from the start, replacing the reference project's Swashbuckle.
  - **README describes the finished product**: someone who clones the repo needs the product, not the phase history.
  - **Every sign-out lands on the sign-in page**: after testing, landing on a public page looked as if nothing had happened.
  - **Dropdown colours**: I chose the customizable select with a plain-CSS fallback after the dark-mode lists were unreadable.

### Problems I caught
- **Tracks marked distributed without any platform**: raised in the Phase 3 review and seen in the browser; fixed in Phase 4.
- **"Clear filters" left the artist in the field**: found while testing; the tests had missed it.
- **Unreadable dropdowns in dark mode**: found while testing; the AI's browser runs never opened a native list.
- **The sign-out expectation**: I reported that sign-out looked wrong. It worked, but landed on a page that looked unchanged, so I decided it should go to the sign-in page.

Everything below comes from the running log [docs/ai-log.md](docs/ai-log.md) (one entry per phase, never
rewritten) and the developer's own notes in it; the security evidence is in
[docs/security-review.md](docs/security-review.md).

## What the AI generated, and what the developer wrote or changed

**Honest summary:** the AI wrote nearly all of the code, tests, specifications and documentation. The
developer's part was deciding what to build and how strictly, reviewing, testing in a real browser,
catching problems the AI missed, and changing the plan; the developer notes in the AI log are their own words.

| Area | AI generated | Developer decided, found or changed |
|---|---|---|
| Requirements and stack | Interview questions with recommended answers; reference-project analysis (what to copy, what not) | 29 interview answers: Admin-only writes with public reads, no registration, no refresh tokens, PostgreSQL, 3 platforms, the fixed genre list, ISRC format, status transitions, routes, MediatR pinned to 12.5.0, Shouldly instead of FluentAssertions, a license check before every package, four deviations from the reference project |
| Specifications and plans | Constitution, spec, clarifications, plan, research, data model, contract, quickstart and tasks for every phase in both repos | Approved or changed each plan (see the decision record); one batched clarification round per phase |
| API code | Clean Architecture solution, CQRS handlers and behaviors, Unit of Work, entities and domain rules, EF Core configurations and migrations, Identity + JWT, exception handlers, OpenAPI, health checks, rate limiting, security headers, sample data, Docker, CI | Phase 1: tests for the identity service in that phase, trusted proxy ranges as configuration, test-only endpoint kept out of production, stricter JWT rules. Phase 2: artist email required, PostgreSQL tests in the same project behind a category. Phase 4: PATCH → distributed refused, no data migration, no hard-coded platform count |
| Web app code | React app, routing, session store, API client from the contract, every page and component, forms, tests, nginx, Docker, CI | Sign-out should always land on the sign-in page (after testing); dropdown approach for dark mode (customizable select with fallback); approved self-hosting the font |
| Tests | All tests: 523 in the API (incl. PostgreSQL via Testcontainers), 223 in the web app | Asked for specific tests: Unit of Work never runs for queries, early Ardalis + EF Core 10 compatibility test, identity service tests, logs without passwords |
| Manual testing | Scripted real-browser runs (Edge over DevTools) | Found bugs the AI's tests and scripts missed: "Clear filters" kept the artist, "Mark as distributed" recorded no platform, unreadable dropdowns in dark mode; reported the sign-out behaviour that led to the redirect change |
| Documentation | READMEs, walkthroughs, AI log entries, security review, this file, study guide | Developer notes in the AI log (verbatim); the README rule (describe the finished product, not the phases); corrections where an AI statement could not be verified (one sentence removed from a developer note) |
| Sample data | Seeder and catalogue; web searches to verify each real pairing | Asked for international legends next to MENA artists, everything clearly labelled as sample data, and every artist–song pairing verified instead of trusting the draft |
| AWS scripts (optional extra) | Bash + AWS CLI v2 scripts in `infra/aws` (setup, deploy, status, preflight, destroy) and their local validation; it departed from the design without asking first (the Auto Scaling group replaces a fixed set of instances instead of being optional; one step file per resource run by `setup.sh` instead of numbered scripts), see the AI log, Phase 6 | Decided the AWS design (load balancer across at least two EC2 instances, optional Auto Scaling group, one script per resource, no Lambda) and chose to have the scripts written without running them |

**AWS scripts:** the scripts were written and statically checked (syntax, ShellCheck, configuration, safety checks against a fake AWS CLI), and were not deployed by Claude.

Per-phase detail, including which commits fixed what: [docs/ai-log.md](docs/ai-log.md).

## Security issues the AI's code introduced or the review found, and how they were handled

| Issue | Introduced or found by | Handling |
|---|---|---|
| Reference project: CORS `AllowAnyOrigin`, lockout disabled, login message revealing unconfirmed accounts, migrate-on-startup swallowing errors | Found by the AI reading the reference | Not copied: explicit origin allow-list (now validated at startup), lockout 5 failures / 15 min, one generic 401, migration failure stops the process |
| A route that changes data could ship without the Admin check | Gap found by the AI in the security review (the policy was proven only on a test endpoint) | Every change route uses one helper; a test reads the real route table and fails if one is missing (proven by removing it) |
| User enumeration on sign-in (message and timing) | AI design | Same 401 for every failure; unknown emails still verify a dummy hash. Measured honestly: medians 63.6 ms vs 70.1 ms over 100 attempts each – close, not identical |
| Brute force and floods | AI design, extended in the review | Sign-in 10/min per IP, all requests 300/min per IP, changes 60/min per admin; forwarded IPs trusted only from configured proxy ranges |
| Forged tokens (`alg: none`, algorithm swap, tampering) | AI design, rules set by the developer | HS256 only, issuer/audience/lifetime/signature, 30 s skew; 10 rejection cases tested |
| Over-posting | AI design | Request records only; tests post ids, status, version and deliveries and see them ignored |
| Artist emails (personal data) on public reads | AI design | Selected only for an admin; omitted otherwise; removed from the web app's cache on sign-out |
| Malformed JSON answered with 500 in Development (since Phase 0) | Introduced by AI code, found by the AI in Phase 2 | Dedicated handler → 400/413 without parser details; tests in all environments |
| Lost updates and duplicate rows under concurrency | AI design | Unique indexes and a PostgreSQL row version → 409; PostgreSQL tests and parallel runs |
| Missing security headers (API) and assets served without them (nginx) | Found by the AI in the review | API middleware with a deny-all content policy; one nginx snippet in every location serving files |
| Font loaded from Google (visitor addresses sent to a third party) | AI design in Phase 0, changed in the review | Font files and license served by the app; a real browser made 48 requests, all to the app's origin |
| Token in `localStorage` (readable by any script) | Planned trade-off, approved by the developer | Accepted: strict content policy, no raw HTML (lint rule), 60-minute tokens, removed on sign-out and on any 401 |
| Open redirect via `?redirect=` | AI design | Same-origin paths only, 10 cases tested |
| Container images: API runtime with 3 HIGH + 3 MEDIUM CVEs; web image with 61 (1 CRITICAL, 12 HIGH) | Found by Trivy in the review | Two **documented exceptions approved by the developer**: API runtime image → `aspnet:10.0.15-alpine3.24` (younger than the two-week version rule; SDK and packages to follow after 2026-11-24); web image removes unused curl and upgrades Alpine packages at build time (not pinned, so re-scan on every rebuild). Both rescanned to 0 |
| Development-tool vulnerabilities: `npm audit` 21 findings (13 high, 8 moderate) | Found by `npm audit` | **Known and accepted where no fix exists.** 9 cleared by removing the unused shadcn CLI and moving eslint's minimatch to 11.0.4. The remaining 12 all come from picomatch ReDoS GHSA-fc5q-39xc-vjpf, which has no fixed release; it reaches vite, vitest, @vitejs/plugin-react, tinyglobby, fdir and typescript-eslint (6 packages), only matches the project's own file patterns at build time, and none of it ships: `npm audit --omit=dev` = 0 and `npm ls --omit=dev picomatch` is empty |
| Secrets in git history | Checked in the review | gitleaks over both histories: one hit, a fake test key; the real local secrets appear in no revision |
| Unpinned packages injected by tooling (`shadcn add` installed `cn` and `next-themes`) | Introduced by an AI tool run, found by the AI | Removed; components written by hand; later the CLI itself was removed |
| No HTTPS/HSTS, client-side sign-out (token valid until expiry) | Known limitations | Accepted and documented: no domain or certificate; no refresh tokens by decision |

Full table with severity and evidence for each row: [docs/security-review.md](docs/security-review.md).

## What the AI got wrong: a track could be "distributed" without saying where

**What happened.** The assignment describes changing a track's status with a PATCH, and its status
list includes "distributed". The AI followed that wording literally in Phase 3: an admin could PATCH a
submitted track to "distributed", and the web app had a "Mark as distributed" button with a confirmation.
The AI modelled "distributed" as a status only, **without recording which platforms the track was sent
to**. For a music distributor that is the one thing "distributed" has to mean.

**Why it was wrong.** "Distributed" is a claim that the track reached streaming platforms. Marked by
a PATCH, it was a claim with no record behind it: nobody could see which platform had the track, whether
it went live or was rejected, or send it again. And with two ways to reach the same status, one recording
deliveries and one not, the catalogue could contradict itself.

**Who caught it.** The developer. While reviewing the Phase 3 plan the developer raised the risk that a PATCH could
set "distributed" without any platform delivery and clash with the upcoming distribute endpoint, and asked
for the reconciliation to be written into the spec. The AI's reconciliation kept the PATCH and labelled such
tracks "Marked as distributed without platform deliveries recorded" – it documented the gap instead of
closing it. While testing the Phase 3 web app in the browser, the developer saw that "Mark as distributed"
records no platform and asked for it to be addressed in Phase 4.

**Why it happened.** The AI treated the assignment's endpoint list as the model, instead of asking what
the status means in the business. It also optimised for "don't break the earlier phase" and so preserved
a path it should have questioned.

**How it was fixed (Phase 4).** At the developer's decision:
- PATCH to "distributed" is refused with `409`: "A track becomes distributed when it is sent to at least
  one platform. Use Distribute to send it." The rule lives in the domain (`TrackStatusRules`), so no
  endpoint can bypass it; `allowedNextStatuses` never offers it.
- The only way to reach "distributed" is `POST /api/tracks/{id}/distribute`, which records at least one
  delivery (platform, status, sent time) in the same change.
- The web app's "Mark as distributed" button, its confirmation and the "without platform deliveries" text
  were removed. No data migration: local databases from before were reset (developer's choice over the
  AI's proposed one-time data fix).
- The README explains why the PATCH is refused.

**Lesson.** When the specification's wording and the domain's meaning disagree, raise it as a question
before modelling it; a status that claims something happened must carry the record of what happened.

### Other mistakes (briefly; details and commits in the AI log)
- Diagnosed a reported sign-out bug as fact before reproducing it; the fixes were real edge cases but not
  shown to be the cause. Later reproduction showed a UX expectation, which the developer turned into a
  product change.
- Malformed JSON → 500 in Development for two phases; audit timestamps never filled until Phase 2; test
  hosts logging into each other.
- "Clear filters" left the artist in the field (found by the developer; tests used non-GUID ids).
- Dark-mode dropdowns unreadable (found by the developer; the AI's browser runs never opened a native list).
- Staged renames/deletions rode along in commits (caught before pushing, twice).
- Phase 5: the first sample-data draft paired Hussain Al Jassmi with "Ahebak Moot" (no such song could be
  found; the real one is "Ahebak") and gave several songs genres that sources do not support (e.g. an
  Egyptian song as Khaleeji). The developer asked for every pairing to be verified; verification replaced
  them (table with sources in `specs/006-seed-data-security-hardening-and-docs/data-model.md`). The README
  said sign-in failures had "matching response times"; the Phase 5 measurement showed a gap of about
  6.5 ms in the medians, and the wording was corrected.

## Decision record (from the start of the project)

Technical and business decisions and the recommendations behind them. "AI" = proposed by the assistant
(usually as a recommended answer in a clarification); "Developer" = the developer's own requirement or
change. Every decision was made or approved by the developer.

### Product and business

| Decision | Proposed by | Why |
|---|---|---|
| Reads public, every change Admin-only; one admin from configuration; no registration, no viewer role | Developer (interview) | A catalogue to browse; only the distributor changes it |
| Exactly three platforms (Spotify, Apple Music, YouTube Music) with fixed ids | Developer (interview) | Same ids in every environment; the code does not hard-code the count (up to 20 per request) |
| Fixed genre list: Arabic Pop, Mahraganat, Khaleeji, Hip-Hop, Electronic, Rap, R&B, Rock; no "Pop" added in Phase 5 | Developer (interview); AI recommended not adding Pop | The list reflects the MENA catalogue; every sample song fits it truthfully, and adding a genre would change validation, the contract, generated types, dropdowns, filters and tests |
| ISRC strict ISO 3901, accepted with or without hyphens, stored as 12 characters, unique | AI (interview recommendation), approved | One canonical form; duplicates impossible |
| Status workflow draft → submitted → distributed, submitted → draft, distributed final; invalid → 409 | AI (interview recommendation), approved | Matches a release process; distributed cannot be undone |
| "Distributed" only through Distribute, with at least one delivery | Developer (Phase 4, after the Phase 3 review) | See "What the AI got wrong" |
| Platform answers simulated by the admin (`PATCH …/distributions/{dspId}`), re-send after rejection | AI (interview recommendation), approved | No real platform integration; still shows every delivery state |
| No data migration for old local databases; reset instead | Developer (Phase 4; AI had proposed a one-time fix) | Only local test data existed |
| Artist email required and unique; shown only to admins | Developer (email required, Phase 2); AI (admin-only display) | Assignment's entity; emails are personal data |
| Release date as a calendar date, future allowed | Developer (interview) | Releases are scheduled |
| Sample data only into an empty catalogue; real names, titles, pairings and genres, everything else invented and labelled | AI (empty-catalogue rule); Developer (legends, labelling, verification) | Never mixes with real data; evaluators see a realistic catalogue without false records |
| README describes the finished product, never the phases | Developer (Phase 4) | A reader who clones wants the product, not the history |

### Architecture and code

| Decision | Proposed by | Why |
|---|---|---|
| .NET 10 Minimal APIs, Clean Architecture, MediatR CQRS, Ardalis Result/Specification, FluentValidation, EF Core + PostgreSQL | Developer (fixed stack) | Mirrors the reference project the team knows |
| Handler interfaces, a real Unit of Work (one save per command), no-tracking projections for queries, ProblemDetails for every error | Developer (master prompt); the AI listed where these differ from the reference project and the developer confirmed them | Fewer saves and clearer transactions; standard error format |
| Plain DTOs on success (no result wrapper in responses) | AI (interview recommendation), approved | Simple responses for the web app |
| Domain rules in entities (`Track.ChangeStatus`, `Distribute`, `RecordAnswer`) | AI | One place for rules; the sample data is built through them too |
| PostgreSQL row version (`xmin`) for concurrent changes → 409 | AI | Two admins cannot overwrite each other silently |
| Contract exported from the running app, drift fails CI; web types generated from it | AI (after the build-time export failed) | The two repos cannot disagree silently |
| Migrate and seed on startup behind a flag; fail loudly | Developer (interview) | Easy local start; no silent broken schema |
| Testcontainers PostgreSQL tests in the same project behind a category | Developer (Phase 2) | Unit tests run without Docker; CI runs all |
| Frontend: React + Vite, TanStack Query for server state, zustand for the session, react-hook-form + zod, shadcn/ui components written by hand | Developer (stack); AI (hand-written components after the CLI injected packages) | No unvetted packages |
| Session in `localStorage`, sign-out on expiry or 401, every sign-out lands on the sign-in page | Developer (storage trade-off accepted; redirect after testing) | No refresh tokens; predictable sign-out |
| Customizable select (`appearance: base-select`) with a plain-CSS fallback | Developer chose among options the AI presented | Plain CSS could not style the highlighted row |

### Security and supply chain

| Decision | Proposed by | Why |
|---|---|---|
| License check before every package; no commercial or source-available packages; MediatR exactly 12.5.0; Shouldly | Developer | Licensing risk (MediatR 13+, FluentAssertions 8+ are commercial) |
| Versions: newest stable at least two weeks old | Developer | Avoid brand-new releases (supply-chain risk) |
| Exceptions for security patches: API runtime image 10.0.15; web image Alpine upgrade without pins | AI found and proposed options; Developer approved both with conditions (2026-11-18) | Known HIGH/CRITICAL CVEs in what ships outweigh the age rule; documented and re-scanned |
| JWT HS256 only, 60 minutes, no refresh, 30 s skew | Developer | Simple and strict |
| Lockout 5/15 min; rate limits sign-in 10/min, all 300/min per IP, changes 60/min per admin | AI (numbers), Developer approved | Slow guessing, floods and bulk changes with a stolen token |
| Trusted proxy ranges as configuration | Developer (Phase 1) | The cloud load balancer's range differs from Docker's |
| Remove the unused shadcn CLI; self-host the font; accept picomatch (no fix exists) | AI (recommended), Developer approved | Fewer dev-tool findings; no third-party requests; honest about what cannot be fixed |
| Secrets only in `.env` / user-secrets; never the admin password in the repo | Developer | Nothing secret in git |

### Process

| Decision | Proposed by | Why |
|---|---|---|
| Spec Kit for every phase in both repos; no production code before spec, plan and tasks | Developer | Reviewable plans before code |
| One branch per phase per repo, Conventional Commits, PR per repo, merge commits only, developer merges, backend first | Developer | Clean, honest history |
| One batched clarification round per phase (≤ 5 questions, recommended answers) | Developer | Fewer interruptions |
| Tests required with every feature; prove a regression test fails on the old code | Developer (constitution), AI (prove-it practice) | Tests that cannot fail prove nothing |
| AI log per phase with mistakes, never rewritten; developer notes only in the developer's words | Developer | Honest evidence for this document |
| Real-browser checks each phase, probes and screenshots only in a scratch folder, no wildcard deletes | Developer | jsdom missed real bugs; nothing stray in the repos |
| AWS deployment scripts as a separate, optional phase; nothing paid created without approval | Developer | Graded scope first |
