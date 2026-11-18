# Implementation Plan: AWS Infrastructure Scripts

**Branch**: `007-aws-infrastructure-scripts` | **Date**: 2026-11-18 | **Spec**: [spec.md](spec.md)

> Recorded after implementation (see the note in spec.md).

## Summary
Bash scripts over AWS CLI v2 in `infra/aws`: `setup.sh` runs idempotent steps, `deploy.sh` releases images
through Systems Manager, `status.sh`/`preflight.sh` read, `destroy.sh` removes, `validate.sh` checks locally.

## Technical context
- **Shell**: bash 4.4+ (`inherit_errexit`), Git Bash on Windows; AWS CLI v2; Docker for builds.
- **Region**: eu-central-1 (interview answer 20). No new NuGet or npm packages.
- **Tools (not shipped)**: ShellCheck `koalaman/shellcheck:v0.11.0` (GPL-3.0, image from 2025-08-04), gitleaks `v8.30.1` (MIT).

## Design

| Area | Choice | Reason |
|---|---|---|
| Format | Bash (the Phase 6 prompt asked for "AWS CLI v2 + Bash scripts") | The master prompt preferred PowerShell; the newer, explicit prompt won. No PowerShell version exists. |
| Structure | One step file per resource in `steps/`, run in order by `setup.sh` (`--only` for single steps) | One entry point with a confirmation; each resource still has its own file |
| Compute | Launch template + Auto Scaling group (min 2), instead of a fixed `09-ec2-instances` script | Replaces failed instances; the master prompt had the ASG as an optional replacement for 09 |
| Images | ECR per environment, immutable tags `<time>-api<sha>-web<sha>`, scan on push, keep 10 | Traceable releases and rollback |
| Secrets | SSM SecureString (db, JWT key, admin password), via private temp files | Free; no secrets in user data or argv |
| Database | RDS PostgreSQL 17 `db.t4g.micro`, private subnets, gp3 encrypted, `SSL Mode=VerifyFull` with the regional CA bundle | Interview answers 21–22; RDS forces TLS |
| Network | Public subnets for ALB and instances (ingress only from the ALB SG), private for RDS, no NAT | Interview answer 22 (cost) |
| Release | SSM Run Command: first instance with migrations, others one by one, each waits for `/health`; then ELB health checks | Interview answer 14 (migrations on one instance); no SSH |
| Trusted proxy | `ReverseProxy__KnownNetworks__0 = VPC CIDR` | Standing rule for Phase 6 |
| S3 | Optional, off by default (no upload feature) | Interview answer 25 |
| Docs | `infra/aws/README.md` (architecture diagram, commands, cost, limits) | The Phase 6 prompt kept all infrastructure files in `infra/aws` |

## Validation
`validate.sh --shellcheck-docker` (LF endings, `bash -n`, ShellCheck, three configs, rendered user data < 16 KB,
seven guard tests with a fake `aws`), a deliberately broken config, and gitleaks. Nothing runs against AWS.
