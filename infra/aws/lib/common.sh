# shellcheck shell=bash
# shellcheck disable=SC2034  # globals set here (TAGS, SSM_PREFIX, LOG_GROUP, REGISTRY ...) are used by the sourcing scripts
# Shared helpers for the Track Management AWS scripts. Sourced by the entry scripts, never run directly.
#
# Rules every script follows:
#   - fail fast (errexit, nounset, pipefail, and errexit inside command substitutions);
#   - no credentials in files: the AWS CLI uses the caller's own profile, SSO session or role;
#   - secrets are generated into SSM Parameter Store and passed to the AWS CLI through private temp
#     files, never on the command line and never printed;
#   - every resource is named "<project>-<environment>-<resource>" and tagged Name/Project/Environment,
#     so the scripts find what they created and never touch anything else.

if (( BASH_VERSINFO[0] < 4 || (BASH_VERSINFO[0] == 4 && BASH_VERSINFO[1] < 4) )); then
  echo "These scripts need bash 4.4 or newer (found ${BASH_VERSION}). On macOS: brew install bash." >&2
  exit 1
fi
set -euo pipefail
shopt -s inherit_errexit
umask 077
export AWS_PAGER=""

INFRA_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_ROOT="$(cd "$INFRA_DIR/../.." && pwd)"
PROJECT_TAG="TrackManagement"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

log()  { printf '[%s] %s\n' "$(date +%H:%M:%S)" "$*" >&2; }
warn() { log "WARNING: $*"; }
die()  { log "ERROR: $*"; exit 1; }

require_cmd() {
  local cmd
  for cmd in "$@"; do
    command -v "$cmd" >/dev/null 2>&1 || die "'$cmd' is required but was not found on PATH."
  done
}

# --- AWS CLI wrappers -------------------------------------------------------------------------------

# aws in the configured region (output as the command prints it).
awsr() { aws --region "$AWS_REGION" "$@"; }

# aws in the configured region with text output; JSON null ("None") becomes an empty string.
awst() {
  local out
  out="$(aws --region "$AWS_REGION" --output text "$@")"
  [[ "$out" == "None" ]] && out=""
  printf '%s' "$out"
}

# Like awst, but an error whose message matches the first argument (an ERE such as
# 'NotFound|NoSuchEntity') means "does not exist" and prints nothing. Any other error fails.
aws_lookup() {
  local not_found="$1" out err rc=0
  shift
  err="$WORK_DIR/lookup.err"
  out="$(aws --region "$AWS_REGION" --output text "$@" 2>"$err")" || rc=$?
  if (( rc != 0 )); then
    if grep -Eq "$not_found" "$err"; then
      return 0
    fi
    cat "$err" >&2
    return "$rc"
  fi
  [[ "$out" == "None" ]] && out=""
  printf '%s' "$out"
}

# file:// argument for --cli-input-json and similar. Converts Git Bash paths for a Windows aws.exe.
file_arg() {
  local path="$1"
  if command -v cygpath >/dev/null 2>&1; then
    path="$(cygpath -m "$path")"
  fi
  printf 'file://%s' "$path"
}

# Tags in the shorthand most services accept: --tags "${TAGS[@]}".
set_tags() {
  TAGS=("Key=Name,Value=$1" "Key=Project,Value=$PROJECT_TAG" "Key=Environment,Value=$ENVIRONMENT")
}

# EC2 --tag-specifications value.
tag_spec() {
  printf 'ResourceType=%s,Tags=[{Key=Name,Value=%s},{Key=Project,Value=%s},{Key=Environment,Value=%s}]' \
    "$1" "$2" "$PROJECT_TAG" "$ENVIRONMENT"
}

# Tags as a JSON array (for --cli-input-json documents).
tags_json() {
  printf '[{"Key":"Name","Value":"%s"},{"Key":"Project","Value":"%s"},{"Key":"Environment","Value":"%s"}]' \
    "$1" "$PROJECT_TAG" "$ENVIRONMENT"
}

sha256_of() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -d' ' -f1
  else
    shasum -a 256 "$1" | cut -d' ' -f1
  fi
}

# Random text without characters that need escaping in JSON, env files or connection strings.
random_secret() {
  openssl rand -base64 "$1" | tr -d '\n+/='
}

# Asks the operator to type a word before a change; --yes skips it. Refuses when nobody can answer.
confirm_or_die() {
  local expected="$1" prompt="$2" answer
  if [[ "${ASSUME_YES:-false}" == true ]]; then
    return 0
  fi
  [[ -t 0 ]] || die "No terminal to confirm on. Re-run interactively, or pass --yes if you are sure."
  read -r -p "$prompt Type '$expected' to continue: " answer
  [[ "$answer" == "$expected" ]] || die "Not confirmed; nothing was changed."
}

# --- configuration ----------------------------------------------------------------------------------

readonly CIDR_RE='^([0-9]{1,3}\.){3}[0-9]{1,3}/([0-9]|[12][0-9]|3[0-2])$'
readonly BOOL_RE='^(true|false)$'
readonly INT_RE='^[0-9]+$'

# Loads config/<env>.env (and the optional, git-ignored config/<env>.local.env) and validates it.
load_config() {
  local env="${1:-}" file override
  [[ "$env" =~ ^(dev|staging|prod)$ ]] || die "--env must be dev, staging or prod."
  file="$INFRA_DIR/config/$env.env"
  override="$INFRA_DIR/config/$env.local.env"
  [[ -f "$file" ]] || die "Missing configuration file $file."
  # shellcheck source=/dev/null
  source "$file"
  if [[ -f "$override" ]]; then
    # shellcheck source=/dev/null
    source "$override"
    log "Using overrides from config/$env.local.env"
  fi
  validate_config "$env"
  NAME_PREFIX="${PROJECT}-${ENVIRONMENT}"
  SSM_PREFIX="/${PROJECT}/${ENVIRONMENT}"
  LOG_GROUP="/${PROJECT}/${ENVIRONMENT}/app"
  export AWS_REGION
}

validate_config() {
  local expected="$1" errors=0 name cidr count
  local -a list

  check() { # variable-name regex explanation
    local value="${!1-}"
    if [[ ! "$value" =~ $2 ]]; then
      log "config: $1='${value}' is invalid ($3)"
      errors=$((errors + 1))
    fi
  }

  check ENVIRONMENT '^(dev|staging|prod)$' 'dev, staging or prod'
  if [[ "${ENVIRONMENT-}" != "$expected" ]]; then
    log "config: ENVIRONMENT='${ENVIRONMENT-}' does not match the file name ($expected)"
    errors=$((errors + 1))
  fi
  check PROJECT '^[a-z][a-z0-9-]{1,14}$' 'lower case, 2-15 characters'
  check AWS_REGION '^[a-z]{2}(-[a-z]+)+-[0-9]$' 'e.g. eu-central-1'
  check AVAILABILITY_ZONES '^([a-z0-9-]+( [a-z0-9-]+)*)?$' 'empty, or zone names separated by spaces'
  check VPC_CIDR "$CIDR_RE" 'a CIDR such as 10.10.0.0/16'
  check ALB_INGRESS_CIDR "$CIDR_RE" 'a CIDR such as 0.0.0.0/0'
  for name in PUBLIC_SUBNET_CIDRS PRIVATE_SUBNET_CIDRS; do
    read -r -a list <<<"${!name-}"
    count=${#list[@]}
    if (( count < 2 || count > 3 )); then
      log "config: $name needs 2 or 3 CIDRs (one per availability zone)"
      errors=$((errors + 1))
    fi
    for cidr in "${list[@]}"; do
      [[ "$cidr" =~ $CIDR_RE ]] || { log "config: $name contains an invalid CIDR '$cidr'"; errors=$((errors + 1)); }
    done
  done
  check CERTIFICATE_ARN '^(arn:aws:acm:[a-z0-9-]+:[0-9]{12}:certificate/[0-9a-f-]+)?$' 'empty, or an ACM certificate ARN'
  check ALB_SSL_POLICY '^ELBSecurityPolicy-[A-Za-z0-9-]+$' 'an ELB security policy name'
  check ALB_DELETION_PROTECTION "$BOOL_RE" 'true or false'
  check INSTANCE_TYPE '^[a-z][a-z0-9]*\.[a-z0-9]+$' 'e.g. t3.small'
  check EBS_VOLUME_GB "$INT_RE" 'a number of GiB'
  check ASG_MIN "$INT_RE" 'a number'
  check ASG_DESIRED "$INT_RE" 'a number'
  check ASG_MAX "$INT_RE" 'a number'
  check SCALE_CPU_TARGET '^([1-9][0-9]?)$' '1-99 (% average CPU)'
  check DB_INSTANCE_CLASS '^db\.[a-z0-9]+\.[a-z0-9]+$' 'e.g. db.t4g.micro'
  check DB_ENGINE_VERSION '^[0-9]{2}(\.[0-9]+)?$' 'a PostgreSQL major version (17) or an exact version (17.6)'
  check DB_ALLOCATED_STORAGE "$INT_RE" 'a number of GiB'
  check DB_MULTI_AZ "$BOOL_RE" 'true or false'
  check DB_BACKUP_RETENTION_DAYS '^([0-9]|[12][0-9]|3[0-5])$' '0-35'
  check DB_DELETION_PROTECTION "$BOOL_RE" 'true or false'
  check DB_NAME '^[a-z][a-z0-9_]{0,62}$' 'lower case letters, digits, underscore'
  check DB_USERNAME '^[a-z][a-z0-9_]{0,15}$' 'lower case letters, digits, underscore; max 16'
  check DB_SSL_MODE '^(VerifyFull|Require)$' 'VerifyFull (checks the RDS certificate) or Require'
  check ADMIN_EMAIL '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$' 'an e-mail address'
  check ECR_KEEP_IMAGES "$INT_RE" 'a number'
  check LOG_RETENTION_DAYS '^(1|3|5|7|14|30|60|90|120|150|180|365)$' 'a CloudWatch retention value'
  check S3_BUCKET_ENABLED "$BOOL_RE" 'true or false'
  check FRONTEND_DIR '^[^;&|`$<>]*$' 'a folder path'

  if [[ "${ASG_MIN-}" =~ $INT_RE && "${ASG_DESIRED-}" =~ $INT_RE && "${ASG_MAX-}" =~ $INT_RE ]]; then
    if (( ASG_MIN > ASG_DESIRED || ASG_DESIRED > ASG_MAX )); then
      log "config: ASG_MIN <= ASG_DESIRED <= ASG_MAX is required"
      errors=$((errors + 1))
    fi
    if (( ASG_MIN < 2 )); then
      log "config: ASG_MIN must be at least 2 (two instances behind the load balancer)"
      errors=$((errors + 1))
    fi
  fi
  if [[ "${DB_ALLOCATED_STORAGE-}" =~ $INT_RE ]] && (( DB_ALLOCATED_STORAGE < 20 )); then
    log "config: DB_ALLOCATED_STORAGE must be at least 20 (RDS gp3 minimum)"
    errors=$((errors + 1))
  fi
  if [[ "${EBS_VOLUME_GB-}" =~ $INT_RE ]] && (( EBS_VOLUME_GB < 8 )); then
    log "config: EBS_VOLUME_GB must be at least 8"
    errors=$((errors + 1))
  fi
  if [[ "$expected" == prod && "${DB_DELETION_PROTECTION-}" != true ]]; then
    warn "prod runs without database deletion protection."
  fi

  unset -f check
  (( errors == 0 )) || die "$errors configuration error(s) in config/$expected.env."
}

# Resolves the frontend repository folder (sibling clone named "frontend" or "takwene-track-frontend").
resolve_frontend_dir() {
  local candidate
  if [[ -n "${FRONTEND_DIR:-}" ]]; then
    [[ -d "$FRONTEND_DIR" ]] || die "FRONTEND_DIR '$FRONTEND_DIR' does not exist."
    (cd "$FRONTEND_DIR" && pwd)
    return
  fi
  for candidate in "$REPO_ROOT/../frontend" "$REPO_ROOT/../takwene-track-frontend"; do
    if [[ -f "$candidate/Dockerfile" ]]; then
      (cd "$candidate" && pwd)
      return
    fi
  done
  die "Frontend repository not found next to the backend. Pass --frontend-dir or set FRONTEND_DIR."
}

# --- AWS session ------------------------------------------------------------------------------------

# Checks the CLI and the caller's identity; sets ACCOUNT_ID and REGISTRY.
init_aws() {
  local version caller
  require_cmd aws
  version="$(aws --version 2>&1)"
  [[ "$version" == aws-cli/2.* ]] || die "AWS CLI v2 is required (found: $version)."
  ACCOUNT_ID="$(awst sts get-caller-identity --query Account)" \
    || die "No AWS credentials. Sign in first (aws configure sso / aws sso login, or an assumed role)."
  caller="$(awst sts get-caller-identity --query Arn)"
  [[ "$caller" == *":root" ]] && warn "You are using the account root user; use an IAM role or SSO user instead."
  REGISTRY="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
  log "AWS account $ACCOUNT_ID, region $AWS_REGION, caller $caller"
}

# --- lookups shared by several scripts --------------------------------------------------------------

find_vpc_id() {
  awst ec2 describe-vpcs \
    --filters "Name=tag:Name,Values=${NAME_PREFIX}-vpc" "Name=tag:Project,Values=${PROJECT_TAG}" \
    --query 'Vpcs[0].VpcId'
}

# Subnet ids of one kind (public|private), ordered by CIDR, separated by spaces.
subnet_ids() {
  local vpc_id="$1" kind="$2"
  awst ec2 describe-subnets \
    --filters "Name=vpc-id,Values=$vpc_id" "Name=tag:Name,Values=${NAME_PREFIX}-${kind}-*" \
    --query 'sort_by(Subnets,&CidrBlock)[].SubnetId' | tr '\t' ' '
}

find_sg_id() {
  local vpc_id="$1" name="$2"
  [[ -n "$vpc_id" ]] || return 0
  awst ec2 describe-security-groups \
    --filters "Name=vpc-id,Values=$vpc_id" "Name=group-name,Values=$name" \
    --query 'SecurityGroups[0].GroupId'
}

find_lb_arn() {
  aws_lookup 'LoadBalancerNotFound' elbv2 describe-load-balancers \
    --names "${NAME_PREFIX}-alb" --query 'LoadBalancers[0].LoadBalancerArn'
}

find_tg_arn() {
  aws_lookup 'TargetGroupNotFound' elbv2 describe-target-groups \
    --names "$1" --query 'TargetGroups[0].TargetGroupArn'
}

find_asg_name() {
  awst autoscaling describe-auto-scaling-groups \
    --auto-scaling-group-names "${NAME_PREFIX}-asg" --query 'AutoScalingGroups[0].AutoScalingGroupName'
}

bucket_name() {
  printf '%s-%s-%s' "$NAME_PREFIX" "$ACCOUNT_ID" "$AWS_REGION"
}
