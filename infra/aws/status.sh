#!/usr/bin/env bash
# Shows the state of one environment. Read-only: it never creates, changes or deletes anything,
# and never prints secret values.
#
#   ./status.sh --env dev
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

ENVIRONMENT=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENVIRONMENT="${2:-}"; shift 2 ;;
    -h|--help) sed -n '2,5p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) die "Unknown option: $1" ;;
  esac
done
load_config "$ENVIRONMENT"
init_aws

show() { printf '  %-22s %s\n' "$1" "${2:--}"; }

echo "== $NAME_PREFIX ($AWS_REGION)"

vpc_id="$(find_vpc_id)"
show "VPC" "$vpc_id"
if [[ -n "$vpc_id" ]]; then
  show "Public subnets" "$(subnet_ids "$vpc_id" public)"
  show "Private subnets" "$(subnet_ids "$vpc_id" private)"
fi

lb_arn="$(find_lb_arn)"
if [[ -n "$lb_arn" ]]; then
  show "Load balancer" "$(awst elbv2 describe-load-balancers --load-balancer-arns "$lb_arn" \
    --query 'LoadBalancers[0].[State.Code,DNSName]' | tr '\t' ' ')"
  for name in api web; do
    tg="$(find_tg_arn "${NAME_PREFIX}-$name")"
    if [[ -n "$tg" ]]; then
      show "Targets ($name)" "$(awst elbv2 describe-target-health --target-group-arn "$tg" \
        --query 'TargetHealthDescriptions[].[Target.Id,TargetHealth.State]' | tr '\t\n' ': ')"
    fi
  done
else
  show "Load balancer" "not created"
fi

if [[ -n "$(find_asg_name)" ]]; then
  show "Auto Scaling group" "$(awst autoscaling describe-auto-scaling-groups \
    --auto-scaling-group-names "${NAME_PREFIX}-asg" \
    --query 'AutoScalingGroups[0].[MinSize,DesiredCapacity,MaxSize,HealthCheckType]' \
    | awk '{print "min " $1 ", desired " $2 ", max " $3 ", health checks " $4}')"
  show "Instances" "$(awst autoscaling describe-auto-scaling-groups \
    --auto-scaling-group-names "${NAME_PREFIX}-asg" \
    --query 'AutoScalingGroups[0].Instances[].[InstanceId,AvailabilityZone,LifecycleState,HealthStatus]' \
    | tr '\t\n' ' ;')"
  show "Last instance refresh" "$(awst autoscaling describe-instance-refreshes \
    --auto-scaling-group-name "${NAME_PREFIX}-asg" --max-records 1 \
    --query 'InstanceRefreshes[0].[Status,PercentageComplete]' | tr '\t' ' ')"
else
  show "Auto Scaling group" "not created"
fi

show "Database" "$(aws_lookup 'DBInstanceNotFound' rds describe-db-instances \
  --db-instance-identifier "${NAME_PREFIX}-db" \
  --query 'DBInstances[0].[DBInstanceStatus,DBInstanceClass,EngineVersion,MultiAZ,DeletionProtection]' \
  | awk '{print $1 ", " $2 ", PostgreSQL " $3 ", Multi-AZ " $4 ", deletion protection " $5}')"

for repo in api web; do
  show "Images ($repo)" "$(aws_lookup 'RepositoryNotFoundException' ecr describe-images \
    --repository-name "${NAME_PREFIX}-$repo" \
    --query 'length(imageDetails)')"
done

show "Released tag" "$(aws_lookup 'ParameterNotFound' ssm get-parameter --name "$SSM_PREFIX/image-tag" \
  --query Parameter.Value)"
show "Parameters" "$(awst ssm describe-parameters \
  --parameter-filters "Key=Name,Option=BeginsWith,Values=$SSM_PREFIX/" \
  --query 'Parameters[].Name' | tr '\t' ' ')"

if [[ -n "$lb_arn" ]] && command -v curl >/dev/null 2>&1; then
  dns="$(awst elbv2 describe-load-balancers --load-balancer-arns "$lb_arn" --query 'LoadBalancers[0].DNSName')"
  code="$(curl -sk -o /dev/null -w '%{http_code}' --max-time 10 "http://$dns/api/health" || true)"
  show "GET /api/health" "HTTP $code (http://$dns)"
fi
