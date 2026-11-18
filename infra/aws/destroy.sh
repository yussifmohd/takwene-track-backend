#!/usr/bin/env bash
# Deletes one environment's infrastructure, in reverse dependency order, waiting for each deletion.
# Only resources with this environment's names and tags are touched.
#
#   ./destroy.sh --env dev                         asks you to type the environment prefix
#   ./destroy.sh --env dev --confirm trackmgmt-dev no prompt (the prefix must match)
#   ./destroy.sh --env prod --allow-prod ...       prod additionally needs --allow-prod
#
# Kept by default (data): a final database snapshot, the ECR images, the SSM secrets, the log group
# and the S3 bucket. Options:
#   --skip-final-snapshot  delete the database without a final snapshot
#   --purge-data           also delete ECR repositories, SSM parameters, the log group and an EMPTY bucket
# Deletion protection (database or load balancer) is never switched off here; the script stops and
# prints the command for you to run consciously.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

usage() {
  sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

ENVIRONMENT=""
CONFIRM=""
ALLOW_PROD=false
SKIP_SNAPSHOT=false
PURGE_DATA=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env) ENVIRONMENT="${2:-}"; shift 2 ;;
    --confirm) CONFIRM="${2:-}"; shift 2 ;;
    --allow-prod) ALLOW_PROD=true; shift ;;
    --skip-final-snapshot) SKIP_SNAPSHOT=true; shift ;;
    --purge-data) PURGE_DATA=true; shift ;;
    -h|--help) usage 0 ;;
    *) log "Unknown option: $1"; usage 1 ;;
  esac
done

load_config "$ENVIRONMENT"
if [[ "$ENVIRONMENT" == prod && "$ALLOW_PROD" != true ]]; then
  die "Refusing to destroy prod without --allow-prod."
fi
init_aws

cat >&2 <<EOF

DELETE environment $NAME_PREFIX in $AWS_REGION (account $ACCOUNT_ID):
  instances and Auto Scaling group, launch template, load balancer and target groups,
  database ${NAME_PREFIX}-db $( [[ "$SKIP_SNAPSHOT" == true ]] && echo "WITHOUT a final snapshot" || echo "(a final snapshot is kept)" ),
  security groups, subnets, route table, internet gateway, VPC, instance role.
$( [[ "$PURGE_DATA" == true ]] && echo "  ALSO: ECR repositories and images, SSM secrets, log group, empty S3 bucket." \
  || echo "Kept: ECR images, SSM secrets, log group, S3 bucket (use --purge-data to delete them)." )

EOF
if [[ -n "$CONFIRM" ]]; then
  [[ "$CONFIRM" == "$NAME_PREFIX" ]] || die "--confirm must be exactly '$NAME_PREFIX'."
else
  [[ -t 0 ]] || die "No terminal to confirm on. Pass --confirm $NAME_PREFIX to delete without a prompt."
  read -r -p "Type '$NAME_PREFIX' to delete it: " answer
  [[ "$answer" == "$NAME_PREFIX" ]] || die "Not confirmed; nothing was deleted."
fi

# Retries a deletion that fails while dependencies are still being released (ENIs, attachments).
retry_delete() {
  local what="$1" err="$WORK_DIR/delete.err"
  shift
  for _ in $(seq 1 30); do
    if aws --region "$AWS_REGION" "$@" >/dev/null 2>"$err"; then
      log "  deleted $what"
      return 0
    fi
    if grep -Eq 'NotFound|does not exist' "$err"; then
      return 0
    fi
    grep -q 'DependencyViolation' "$err" || { cat "$err" >&2; return 1; }
    sleep 10
  done
  cat "$err" >&2
  return 1
}

# --- compute ---
ASG_NAME="${NAME_PREFIX}-asg"
if [[ -n "$(find_asg_name)" ]]; then
  log "Deleting Auto Scaling group $ASG_NAME and its instances"
  awsr autoscaling delete-auto-scaling-group --auto-scaling-group-name "$ASG_NAME" --force-delete
  for _ in $(seq 1 60); do
    [[ -z "$(find_asg_name)" ]] && break
    sleep 10
  done
  [[ -z "$(find_asg_name)" ]] || die "Auto Scaling group still deleting; re-run destroy.sh later."
fi
lt="$(aws_lookup 'NotFound' ec2 describe-launch-templates --launch-template-names "${NAME_PREFIX}-lt" \
  --query 'LaunchTemplates[0].LaunchTemplateId')"
if [[ -n "$lt" ]]; then
  awsr ec2 delete-launch-template --launch-template-id "$lt" >/dev/null
  log "  deleted launch template ${NAME_PREFIX}-lt"
fi

# --- load balancer ---
lb_arn="$(find_lb_arn)"
if [[ -n "$lb_arn" ]]; then
  protected="$(awst elbv2 describe-load-balancer-attributes --load-balancer-arn "$lb_arn" \
    --query "Attributes[?Key=='deletion_protection.enabled'].Value | [0]")"
  if [[ "$protected" == true ]]; then
    die "Load balancer deletion protection is on. Switch it off consciously, then re-run:
  aws elbv2 modify-load-balancer-attributes --region $AWS_REGION --load-balancer-arn $lb_arn --attributes Key=deletion_protection.enabled,Value=false"
  fi
  awsr elbv2 delete-load-balancer --load-balancer-arn "$lb_arn"
  awsr elbv2 wait load-balancers-deleted --load-balancer-arns "$lb_arn"
  log "  deleted load balancer ${NAME_PREFIX}-alb"
fi
for name in api web; do
  tg="$(find_tg_arn "${NAME_PREFIX}-$name")"
  if [[ -n "$tg" ]]; then
    retry_delete "target group ${NAME_PREFIX}-$name" elbv2 delete-target-group --target-group-arn "$tg"
  fi
done

# --- database ---
DB_ID="${NAME_PREFIX}-db"
db_protected="$(aws_lookup 'DBInstanceNotFound' rds describe-db-instances --db-instance-identifier "$DB_ID" \
  --query 'DBInstances[0].DeletionProtection')"
if [[ -n "$db_protected" ]]; then
  if [[ "$db_protected" == True || "$db_protected" == true ]]; then
    die "Database deletion protection is on. Switch it off consciously, then re-run:
  aws rds modify-db-instance --region $AWS_REGION --db-instance-identifier $DB_ID --no-deletion-protection --apply-immediately"
  fi
  if [[ "$SKIP_SNAPSHOT" == true ]]; then
    awsr rds delete-db-instance --db-instance-identifier "$DB_ID" --skip-final-snapshot >/dev/null
  else
    snapshot="${DB_ID}-final-$(date -u +%Y%m%d%H%M%S)"
    awsr rds delete-db-instance --db-instance-identifier "$DB_ID" \
      --final-db-snapshot-identifier "$snapshot" >/dev/null
    log "  final snapshot: $snapshot (billed as backup storage until you delete it)"
  fi
  log "Deleting database $DB_ID (takes several minutes)"
  awsr rds wait db-instance-deleted --db-instance-identifier "$DB_ID"
  log "  deleted database $DB_ID"
fi
subnet_group="$(aws_lookup 'DBSubnetGroupNotFoundFault' rds describe-db-subnet-groups \
  --db-subnet-group-name "${NAME_PREFIX}-db-subnets" --query 'DBSubnetGroups[0].DBSubnetGroupName')"
if [[ -n "$subnet_group" ]]; then
  awsr rds delete-db-subnet-group --db-subnet-group-name "$subnet_group"
  log "  deleted database subnet group $subnet_group"
fi

# --- IAM ---
ROLE="${NAME_PREFIX}-ec2-role"
PROFILE="${NAME_PREFIX}-ec2-profile"
if [[ -n "$(aws_lookup 'NoSuchEntity' iam get-instance-profile --instance-profile-name "$PROFILE" \
  --query InstanceProfile.InstanceProfileName)" ]]; then
  roles="$(awst iam get-instance-profile --instance-profile-name "$PROFILE" --query 'InstanceProfile.Roles[].RoleName')"
  for role in $roles; do
    awsr iam remove-role-from-instance-profile --instance-profile-name "$PROFILE" --role-name "$role"
  done
  awsr iam delete-instance-profile --instance-profile-name "$PROFILE"
  log "  deleted instance profile $PROFILE"
fi
if [[ -n "$(aws_lookup 'NoSuchEntity' iam get-role --role-name "$ROLE" --query Role.RoleName)" ]]; then
  for policy in $(awst iam list-role-policies --role-name "$ROLE" --query 'PolicyNames[]'); do
    awsr iam delete-role-policy --role-name "$ROLE" --policy-name "$policy"
  done
  for arn in $(awst iam list-attached-role-policies --role-name "$ROLE" --query 'AttachedPolicies[].PolicyArn'); do
    awsr iam detach-role-policy --role-name "$ROLE" --policy-arn "$arn"
  done
  awsr iam delete-role --role-name "$ROLE"
  log "  deleted role $ROLE"
fi

# --- network ---
vpc_id="$(find_vpc_id)"
if [[ -n "$vpc_id" ]]; then
  for name in app db alb; do
    sg="$(find_sg_id "$vpc_id" "${NAME_PREFIX}-$name")"
    if [[ -n "$sg" ]]; then
      # The groups reference each other in their rules, which blocks deletion: remove the rules first.
      perms="$(awsr ec2 describe-security-groups --group-ids "$sg" --output json \
        --query 'SecurityGroups[0].IpPermissions')"
      if [[ "$(tr -d '[:space:]' <<<"$perms")" != "[]" ]]; then
        awsr ec2 revoke-security-group-ingress --group-id "$sg" --ip-permissions "$perms" >/dev/null
      fi
    fi
  done
  for name in app db alb; do
    sg="$(find_sg_id "$vpc_id" "${NAME_PREFIX}-$name")"
    [[ -z "$sg" ]] || retry_delete "security group ${NAME_PREFIX}-$name" ec2 delete-security-group --group-id "$sg"
  done

  rt_id="$(awst ec2 describe-route-tables --filters "Name=vpc-id,Values=$vpc_id" \
    "Name=tag:Name,Values=${NAME_PREFIX}-public-rt" --query 'RouteTables[0].RouteTableId')"
  if [[ -n "$rt_id" ]]; then
    for assoc in $(awst ec2 describe-route-tables --route-table-ids "$rt_id" \
      --query 'RouteTables[0].Associations[?!Main].RouteTableAssociationId'); do
      awsr ec2 disassociate-route-table --association-id "$assoc"
    done
    awsr ec2 delete-route-table --route-table-id "$rt_id"
    log "  deleted route table $rt_id"
  fi
  for subnet in $(subnet_ids "$vpc_id" public) $(subnet_ids "$vpc_id" private); do
    retry_delete "subnet $subnet" ec2 delete-subnet --subnet-id "$subnet"
  done
  for igw in $(awst ec2 describe-internet-gateways --filters "Name=attachment.vpc-id,Values=$vpc_id" \
    "Name=tag:Project,Values=$PROJECT_TAG" --query 'InternetGateways[].InternetGatewayId'); do
    awsr ec2 detach-internet-gateway --internet-gateway-id "$igw" --vpc-id "$vpc_id"
    awsr ec2 delete-internet-gateway --internet-gateway-id "$igw"
    log "  deleted internet gateway $igw"
  done
  retry_delete "VPC $vpc_id" ec2 delete-vpc --vpc-id "$vpc_id"
fi

# --- data (only with --purge-data) ---
if [[ "$PURGE_DATA" == true ]]; then
  for repo in api web; do
    if [[ -n "$(aws_lookup 'RepositoryNotFoundException' ecr describe-repositories \
      --repository-names "${NAME_PREFIX}-$repo" --query 'repositories[0].repositoryName')" ]]; then
      awsr ecr delete-repository --repository-name "${NAME_PREFIX}-$repo" --force >/dev/null
      log "  deleted ECR repository ${NAME_PREFIX}-$repo"
    fi
  done
  params="$(awst ssm describe-parameters --parameter-filters "Key=Name,Option=BeginsWith,Values=$SSM_PREFIX/" \
    --query 'Parameters[].Name')"
  if [[ -n "$params" ]]; then
    read -r -a param_names <<<"$(tr '\t' ' ' <<<"$params")"
    awsr ssm delete-parameters --names "${param_names[@]}" >/dev/null
    log "  deleted SSM parameters under $SSM_PREFIX/"
  fi
  if [[ -n "$(awst logs describe-log-groups --log-group-name-prefix "$LOG_GROUP" \
    --query "logGroups[?logGroupName=='$LOG_GROUP'].logGroupName | [0]")" ]]; then
    awsr logs delete-log-group --log-group-name "$LOG_GROUP"
    log "  deleted log group $LOG_GROUP"
  fi
  bucket="$(bucket_name)"
  if [[ -n "$(awst s3api list-buckets --query "Buckets[?Name=='$bucket'].Name | [0]")" ]]; then
    # shellcheck disable=SC2016  # the backticks are JMESPath literals, not shell
    leftovers="$(awst s3api list-object-versions --bucket "$bucket" --max-items 1 \
      --query '[length(Versions || `[]`), length(DeleteMarkers || `[]`)]' | head -n 1 | tr -s '\t ' ' ')"
    if [[ "$leftovers" == "0 0" ]]; then
      awsr s3api delete-bucket --bucket "$bucket"
      log "  deleted bucket $bucket"
    else
      warn "Bucket $bucket is not empty; it was kept. Empty it (all versions) and delete it yourself."
    fi
  fi
fi

log "Environment $NAME_PREFIX deleted."
