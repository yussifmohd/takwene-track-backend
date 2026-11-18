#!/usr/bin/env bash
# Read-only checks before the first setup.sh: local tools, AWS sign-in, and whether the region offers
# what the configuration asks for. Creates nothing and costs nothing.
#
#   ./preflight.sh --env dev
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
source "$INFRA_DIR/steps/network.sh"
source "$INFRA_DIR/steps/rds.sh"

ENVIRONMENT=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENVIRONMENT="${2:-}"; shift 2 ;;
    -h|--help) sed -n '2,6p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "Unknown option: $1" ;;
  esac
done

problems=0
ok()   { printf '  ok    %s\n' "$*"; }
fail() { printf '  FAIL  %s\n' "$*"; problems=$((problems + 1)); }

echo "== Local tools"
for cmd in aws docker git openssl curl base64; do
  if command -v "$cmd" >/dev/null 2>&1; then ok "$cmd"; else fail "$cmd not found"; fi
done
ok "bash $BASH_VERSION"
if docker info >/dev/null 2>&1; then ok "docker daemon running"; else fail "docker daemon not reachable (needed by deploy.sh)"; fi

echo "== Configuration"
load_config "$ENVIRONMENT"
ok "config/$ENVIRONMENT.env is valid"
frontend="$(resolve_frontend_dir 2>/dev/null || true)"
if [[ -n "$frontend" ]]; then ok "frontend clone: $frontend"; else fail "frontend clone not found (deploy.sh needs it)"; fi

echo "== AWS"
init_aws
ok "signed in to account $ACCOUNT_ID"

zones="$(availability_zones)"
read -r -a azs <<<"$zones"
read -r -a public <<<"$PUBLIC_SUBNET_CIDRS"
if (( ${#azs[@]} >= ${#public[@]} )); then ok "availability zones: ${azs[*]:0:${#public[@]}}"; else fail "not enough availability zones"; fi

offered="$(awst ec2 describe-instance-type-offerings --location-type availability-zone \
  --filters "Name=instance-type,Values=$INSTANCE_TYPE" --query 'InstanceTypeOfferings[].Location' | tr '\t' ' ')"
for az in "${azs[@]:0:${#public[@]}}"; do
  if [[ " $offered " == *" $az "* ]]; then ok "$INSTANCE_TYPE offered in $az"; else fail "$INSTANCE_TYPE not offered in $az"; fi
done

version="$(resolve_engine_version 2>/dev/null || true)"
if [[ -n "$version" ]]; then ok "PostgreSQL $version available"; else fail "PostgreSQL $DB_ENGINE_VERSION not offered"; fi
if [[ -n "$version" ]]; then
  orderable="$(awst rds describe-orderable-db-instance-options --engine postgres --engine-version "$version" \
    --db-instance-class "$DB_INSTANCE_CLASS" --query 'OrderableDBInstanceOptions[0].DBInstanceClass' --max-items 1 \
    | head -n 1)"
  if [[ "$orderable" == "$DB_INSTANCE_CLASS" ]]; then ok "$DB_INSTANCE_CLASS orderable"; else fail "$DB_INSTANCE_CLASS not orderable for PostgreSQL $version"; fi
fi

if [[ -n "$CERTIFICATE_ARN" ]]; then
  cert_status="$(aws_lookup 'ResourceNotFoundException' acm describe-certificate \
    --certificate-arn "$CERTIFICATE_ARN" --query Certificate.Status)"
  if [[ "$cert_status" == ISSUED ]]; then ok "certificate issued"; else fail "certificate status: ${cert_status:-not found}"; fi
fi

vpcs="$(awst ec2 describe-vpcs --query 'length(Vpcs)')"
ok "$vpcs VPC(s) in the region (default limit: 5)"

echo
if (( problems == 0 )); then
  echo "Preflight passed. Next: ./setup.sh --env $ENVIRONMENT"
else
  echo "Preflight found $problems problem(s)."
  exit 1
fi
