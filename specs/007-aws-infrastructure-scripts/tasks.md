---
description: "Task list for Phase 6 – AWS Infrastructure Scripts (recorded after implementation)"
---

# Tasks: AWS Infrastructure Scripts

**Input**: `specs/007-aws-infrastructure-scripts/` (spec.md, plan.md)
**Tests**: local validation only (`infra/aws/validate.sh`); nothing is run against AWS.
**Skills**: aws-infra-scripts (adapted to Bash), git-workflow, decisions-log

## Phase 1: Foundation
- [x] T001 `infra/aws/lib/common.sh`: strict mode with `inherit_errexit`, AWS wrappers (`awst`, `aws_lookup`), tags, confirmation, config validation, lookups
- [x] T002 [P] `infra/aws/config/{dev,staging,prod}.env` (non-secret) and `.gitignore` for `*.local.env`

## Phase 2: User Story 1 – Create an environment (P1)
- [x] T003 [P] `steps/network.sh`: VPC, 2 public + 2 private subnets, IGW, public route table
- [x] T004 [P] `steps/security-groups.sh`: alb / app / db groups referencing each other
- [x] T005 [P] `steps/ecr.sh`: two repositories, immutable tags, scan on push, lifecycle
- [x] T006 [P] `steps/secrets.sh`: generated SecureString parameters, never overwritten
- [x] T007 [P] `steps/storage.sh`: log group with retention; optional private S3 bucket
- [x] T008 `steps/rds.sh`: subnet group, PostgreSQL 17, wait, `db-host` parameter
- [x] T009 [P] `steps/iam.sh`: instance role (SSM core, own parameters, two repositories, log group) and profile
- [x] T010 `steps/alb.sh`: load balancer, api/web target groups, listeners, `/api/*` rule, optional HTTPS
- [x] T011 `steps/compute.sh` + `instance/bootstrap.sh` + `instance/run-app.sh`: launch template (IMDSv2, encrypted disk, user data), Auto Scaling group, CPU target tracking
- [x] T012 `setup.sh`: plan + cost warning, typed confirmation, `--only`

## Phase 3: User Story 2 – Release (P1)
- [x] T013 `deploy.sh`: build/push with clean-tree check, `--image-tag`, rolling SSM release with one migrating instance, ELB health checks, `--refresh-instances`

## Phase 4: User Story 3 – Inspect and remove (P2)
- [x] T014 [P] `status.sh` and `preflight.sh` (read-only)
- [x] T015 `destroy.sh`: typed prefix, prod guard, deletion-protection stop, reverse order with retries, data kept unless `--purge-data`

## Phase 5: Validation and docs
- [x] T016 `validate.sh` + `tests/fake-aws/aws`: LF, `bash -n`, ShellCheck, configs, user data, seven guards
- [x] T017 ShellCheck 0.11.0 clean (fixed the JMESPath `+` bug in destroy.sh); broken-config check; gitleaks clean
- [x] T018 `infra/aws/README.md` (architecture, commands, Running on Windows, environments, cost, limits)
- [x] T019 Backend README "Deploying to AWS", DECISIONS.md note, ai-log Phase 6, walkthrough, Memory (master + repo)
- [x] T020 Commit with executable bits (`git ls-files --stage`: every `.sh` 100755), push, PR, CI green
