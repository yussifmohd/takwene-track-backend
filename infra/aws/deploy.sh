#!/usr/bin/env bash
# Releases the application to an environment created by setup.sh.
#
#   ./deploy.sh --env dev                     build both images, push to ECR, roll out
#   ./deploy.sh --env prod --image-tag TAG    roll out images already in ECR (no build; rollback)
#   ./deploy.sh --env dev --refresh-instances replace the instances (after a launch-template change)
#
# Options: --frontend-dir DIR (frontend clone), --allow-dirty (build with uncommitted changes),
#          --yes (no prompt for prod).
#
# Rollout: the tag is stored in SSM (<prefix>/image-tag); then, through Systems Manager (no SSH),
# the FIRST instance restarts with database migrations on, and the others restart one by one without.
# Each restart waits for the API's /health before moving on, so one instance keeps serving.
# Note: migrations run while older instances still serve; keep schema changes backward compatible.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"
# shellcheck source=steps/secrets.sh
source "$INFRA_DIR/steps/secrets.sh"

usage() {
  sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

ENVIRONMENT=""
IMAGE_TAG=""
ALLOW_DIRTY=false
REFRESH=false
ASSUME_YES=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENVIRONMENT="${2:-}"; shift 2 ;;
    --image-tag) IMAGE_TAG="${2:-}"; shift 2 ;;
    --frontend-dir) FRONTEND_DIR="${2:-}"; shift 2 ;;
    --allow-dirty) ALLOW_DIRTY=true; shift ;;
    --refresh-instances) REFRESH=true; shift ;;
    --yes) ASSUME_YES=true; shift ;;
    -h|--help) usage 0 ;;
    *) log "Unknown option: $1"; usage 1 ;;
  esac
done

# Keep a FRONTEND_DIR given on the command line over the one in the config file.
cli_frontend_dir="${FRONTEND_DIR:-}"
load_config "$ENVIRONMENT"
[[ -n "$cli_frontend_dir" ]] && FRONTEND_DIR="$cli_frontend_dir"
[[ -z "$IMAGE_TAG" || "$IMAGE_TAG" =~ ^[A-Za-z0-9._-]{1,128}$ ]] || die "Invalid --image-tag."
init_aws

ASG_NAME="${NAME_PREFIX}-asg"
[[ -n "$(find_asg_name)" ]] || die "Auto Scaling group $ASG_NAME not found; run ./setup.sh --env $ENVIRONMENT first."

if [[ "$REFRESH" == true ]]; then
  [[ "$ENVIRONMENT" == prod ]] && confirm_or_die prod "Replace every prod instance (rolling, 50% stay in service)?"
  refresh_id="$(awst autoscaling start-instance-refresh --auto-scaling-group-name "$ASG_NAME" \
    --preferences '{"MinHealthyPercentage":50,"InstanceWarmup":300}' --query InstanceRefreshId)"
  log "Instance refresh $refresh_id started. Follow it with ./status.sh --env $ENVIRONMENT"
  exit 0
fi

[[ "$ENVIRONMENT" == prod ]] && confirm_or_die prod "Release to prod?"

image_exists() {
  local digest
  digest="$(aws_lookup 'ImageNotFoundException|RepositoryNotFoundException' ecr describe-images \
    --repository-name "$1" --image-ids "imageTag=$2" --query 'imageDetails[0].imageDigest')"
  [[ -n "$digest" ]]
}

build_and_push() {
  local frontend backend_sha frontend_sha dirty
  require_cmd docker git
  frontend="$(resolve_frontend_dir)"
  if [[ "$ALLOW_DIRTY" != true ]]; then
    dirty="$(git -C "$REPO_ROOT" status --porcelain)$(git -C "$frontend" status --porcelain)"
    [[ -z "$dirty" ]] || die "Uncommitted changes in the backend or frontend clone. Commit them, or pass --allow-dirty."
  fi
  backend_sha="$(git -C "$REPO_ROOT" rev-parse --short=7 HEAD)"
  frontend_sha="$(git -C "$frontend" rev-parse --short=7 HEAD)"
  IMAGE_TAG="$(date -u +%Y%m%d%H%M%S)-api${backend_sha}-web${frontend_sha}"

  log "Building $IMAGE_TAG (linux/amd64)"
  docker build --platform linux/amd64 --pull -t "$REGISTRY/${NAME_PREFIX}-api:$IMAGE_TAG" "$REPO_ROOT"
  docker build --platform linux/amd64 --pull -t "$REGISTRY/${NAME_PREFIX}-web:$IMAGE_TAG" "$frontend"

  awsr ecr get-login-password | docker login --username AWS --password-stdin "$REGISTRY" >/dev/null
  docker push "$REGISTRY/${NAME_PREFIX}-api:$IMAGE_TAG"
  docker push "$REGISTRY/${NAME_PREFIX}-web:$IMAGE_TAG"
}

wait_for_ssm() {
  local id="$1" ping=""
  for _ in $(seq 1 60); do
    ping="$(awst ssm describe-instance-information --filters "Key=InstanceIds,Values=$id" \
      --query 'InstanceInformationList[0].PingStatus')"
    [[ "$ping" == Online ]] && return 0
    sleep 5
  done
  die "Instance $id is not reachable through Systems Manager (status: ${ping:-unknown})."
}

run_on_instance() {
  local id="$1" command="$2" command_id status=""
  command_id="$(awst ssm send-command --instance-ids "$id" --document-name AWS-RunShellScript \
    --comment "Track Management ${ENVIRONMENT} release ${IMAGE_TAG}" --timeout-seconds 900 \
    --parameters "{\"commands\":[\"$command\"]}" --query Command.CommandId)"
  for _ in $(seq 1 180); do
    sleep 5
    status="$(aws_lookup 'InvocationDoesNotExist' ssm get-command-invocation \
      --command-id "$command_id" --instance-id "$id" --query Status)"
    case "$status" in
      Success) break ;;
      Failed|Cancelled|TimedOut|Cancelling)
        awst ssm get-command-invocation --command-id "$command_id" --instance-id "$id" \
          --query StandardErrorContent >&2 || true
        die "Release failed on $id ($status). Instance logs: CloudWatch $LOG_GROUP" ;;
    esac
  done
  [[ "$status" == Success ]] || die "Release on $id did not finish in 15 minutes."
  awst ssm get-command-invocation --command-id "$command_id" --instance-id "$id" \
    --query StandardOutputContent | tail -n 3 >&2
}

if [[ -n "$IMAGE_TAG" ]]; then
  if ! image_exists "${NAME_PREFIX}-api" "$IMAGE_TAG" || ! image_exists "${NAME_PREFIX}-web" "$IMAGE_TAG"; then
    die "Images with tag $IMAGE_TAG are not in both ECR repositories."
  fi
  log "Rolling out existing images $IMAGE_TAG"
else
  build_and_push
fi

put_string_param "$SSM_PREFIX/image-tag" "$IMAGE_TAG"

instances="$(awst autoscaling describe-auto-scaling-groups --auto-scaling-group-names "$ASG_NAME" \
  --query "AutoScalingGroups[0].Instances[?LifecycleState=='InService'].InstanceId")"
read -r -a instance_ids <<<"$(tr '\t' ' ' <<<"$instances")"
(( ${#instance_ids[@]} > 0 )) || die "No running instances in $ASG_NAME yet; wait a few minutes and re-run."

first=true
for id in "${instance_ids[@]}"; do
  wait_for_ssm "$id"
  if [[ "$first" == true ]]; then
    log "Starting $IMAGE_TAG on $id with database migrations"
    run_on_instance "$id" "/opt/trackmgmt/run-app.sh --migrate"
    first=false
  else
    log "Starting $IMAGE_TAG on $id"
    run_on_instance "$id" "/opt/trackmgmt/run-app.sh"
  fi
done

# From now on the Auto Scaling group replaces instances that fail the load balancer's checks.
awsr autoscaling update-auto-scaling-group --auto-scaling-group-name "$ASG_NAME" \
  --health-check-type ELB --health-check-grace-period 300

lb_dns="$(awst elbv2 describe-load-balancers --names "${NAME_PREFIX}-alb" --query 'LoadBalancers[0].DNSName')"
scheme=http
[[ -n "$CERTIFICATE_ARN" ]] && scheme=https
if command -v curl >/dev/null 2>&1; then
  for _ in $(seq 1 24); do
    if curl -fsSk -o /dev/null "$scheme://$lb_dns/api/health"; then
      log "Health check through the load balancer: OK"
      break
    fi
    sleep 5
  done
fi
log "Released $IMAGE_TAG to $NAME_PREFIX: $scheme://$lb_dns"
log "Admin sign-in: $ADMIN_EMAIL; password: aws ssm get-parameter --region $AWS_REGION --name $SSM_PREFIX/admin-password --with-decryption --query Parameter.Value --output text"
