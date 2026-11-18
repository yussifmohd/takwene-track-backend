#!/usr/bin/env bash
# Creates or updates the AWS infrastructure for one environment. Safe to re-run: every step looks its
# resources up first and only creates what is missing.
#
#   ./setup.sh --env dev                 all steps, asks for confirmation (creates billable resources)
#   ./setup.sh --env dev --only alb      one or more steps (comma-separated)
#   ./setup.sh --env prod --yes          no prompt (CI)
#
# Steps, in order: network, security-groups, ecr, secrets, logs, s3, rds, iam, alb, compute.
# After setup, release the application with ./deploy.sh --env <env>.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
for step_file in "$INFRA_DIR"/steps/*.sh; do
  # shellcheck source=/dev/null
  source "$step_file"
done

readonly ALL_STEPS="network security-groups ecr secrets logs s3 rds iam alb compute"

usage() {
  sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

ENVIRONMENT=""
ONLY=""
ASSUME_YES=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENVIRONMENT="${2:-}"; shift 2 ;;
    --only) ONLY="${2:-}"; shift 2 ;;
    --yes) ASSUME_YES=true; shift ;;
    -h|--help) usage 0 ;;
    *) log "Unknown option: $1"; usage 1 ;;
  esac
done

load_config "$ENVIRONMENT"

steps="$ALL_STEPS"
if [[ -n "$ONLY" ]]; then
  steps="${ONLY//,/ }"
  for step in $steps; do
    [[ " $ALL_STEPS " == *" $step "* ]] || die "Unknown step '$step'. Steps: $ALL_STEPS"
  done
fi

init_aws

cat >&2 <<EOF

Environment : $ENVIRONMENT ($NAME_PREFIX) in $AWS_REGION, account $ACCOUNT_ID
Steps       : $steps
Creates     : VPC $VPC_CIDR, load balancer, $ASG_DESIRED x $INSTANCE_TYPE (max $ASG_MAX),
              RDS $DB_INSTANCE_CLASS PostgreSQL $DB_ENGINE_VERSION (Multi-AZ: $DB_MULTI_AZ), 2 ECR repositories,
              SSM parameters, log group$( [[ "$S3_BUCKET_ENABLED" == true ]] && echo ", S3 bucket" )
COST        : the load balancer, instances, database, public IPv4 addresses and logs are billed
              while they exist. Remove everything with ./destroy.sh --env $ENVIRONMENT.

EOF
confirm_or_die "$ENVIRONMENT" "This creates or changes billable AWS resources."

for step in $steps; do
  "step_${step//-/_}"
done

lb_dns="$(awst elbv2 describe-load-balancers --names "${NAME_PREFIX}-alb" \
  --query 'LoadBalancers[0].DNSName' 2>/dev/null || true)"
log "Setup finished for $NAME_PREFIX."
if [[ -n "$lb_dns" ]]; then
  log "Load balancer: http://$lb_dns"
fi
log "Next: ./deploy.sh --env $ENVIRONMENT   (builds the images, pushes them and starts the app)"
