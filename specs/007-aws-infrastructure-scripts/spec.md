# Feature Specification: AWS Infrastructure Scripts

**Feature Branch**: `007-aws-infrastructure-scripts`
**Created**: 2026-11-18
**Status**: Implemented (scripts only; not run against AWS)
**Input**: Master prompt Phase 6 (AWS CLI scripts, load balancer, at least two EC2 instances, managed
database, teardown, no Lambda) and the Phase 6 session prompt ("AWS CLI v2 + Bash scripts", dev/staging/prod,
setup/deploy/status/cleanup, validate locally, do not execute AWS commands).

> Written after the implementation: the Phase 6 prompt asked to start implementing immediately, so this
> spec, the plan and the task list record what was built instead of preceding it (see docs/ai-log.md, Phase 6).

## User Scenarios

### User Story 1 – Create an environment (P1)
An operator with their own AWS sign-in creates a complete environment (network, load balancer, two or more
instances, database, registries, secrets) with one command, and can re-run it safely.

**Acceptance**
1. `setup.sh --env dev` prints what it will create and the cost warning, and changes nothing until the
   operator types the environment name (`--yes` for CI).
2. Re-running it reports existing resources as "exists" and creates only what is missing.

### User Story 2 – Release the application (P1)
The operator builds the API and web images and releases them without downtime and without SSH.

**Acceptance**
1. `deploy.sh --env dev` builds both images from committed code, pushes them, and restarts the instances
   one by one; exactly one instance runs database migrations.
2. `deploy.sh --image-tag <tag>` releases (or rolls back to) images already in the registry.

### User Story 3 – Inspect and remove (P2)
The operator sees the state of an environment without changing it, and removes it without losing data by accident.

**Acceptance**
1. `status.sh` and `preflight.sh` only read.
2. `destroy.sh` requires the typed prefix, refuses prod without `--allow-prod`, never switches off deletion
   protection, and keeps a final database snapshot, images, secrets and logs unless told otherwise.

## Requirements

- **FR-001** Bash + AWS CLI v2; everything inside `backend/infra/aws`; no application code changes.
- **FR-002** dev, staging and prod from configuration files; every value validated before any AWS call.
- **FR-003** Idempotent: resources found by name and tags (`Project=TrackManagement`, `Environment`) before creating.
- **FR-004** No credentials in files; secrets generated into SSM Parameter Store, never printed or passed on a command line.
- **FR-005** Load balancer routes `/api/*` to the API (health `/health`) and the rest to the web app, across at least two instances in two availability zones.
- **FR-006** Database in private subnets, reachable only from the instances, TLS verified.
- **FR-007** Least privilege: security groups reference each other; the instance role reads only its environment's parameters, repositories and log group.
- **FR-008** Local validation without AWS: syntax, ShellCheck, configuration, rendered user data, safety guards.
- **FR-009** No paid resource is created or deleted during this phase.

## Out of scope
Running the scripts against AWS, a domain and certificate, Lambda, ElastiCache, ECS, a NAT gateway,
upload features using S3.

## Success criteria
- **SC-001** `validate.sh --shellcheck-docker` passes.
- **SC-002** No secret in the folder (gitleaks).
- **SC-003** The backend README links to the scripts and states they are validated locally only.
