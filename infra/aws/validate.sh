#!/usr/bin/env bash
# Local checks for the infrastructure scripts. Needs no AWS account and makes no AWS calls
# (a fake `aws` command stands in, and fails if anything beyond the identity check is attempted).
#
#   ./validate.sh                      syntax, line endings, config for dev/staging/prod, user data, guards
#   ./validate.sh --shellcheck-docker  also run ShellCheck from its official container image
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/common.sh"

readonly SHELLCHECK_IMAGE="koalaman/shellcheck:v0.11.0"
USE_DOCKER_SHELLCHECK=false
[[ "${1:-}" == "--shellcheck-docker" ]] && USE_DOCKER_SHELLCHECK=true

failures=0
pass() { printf '  ok    %s\n' "$*"; }
fail() { printf '  FAIL  %s\n' "$*"; failures=$((failures + 1)); }

cd "$INFRA_DIR"
scripts=(lib/common.sh steps/*.sh instance/*.sh setup.sh deploy.sh status.sh preflight.sh destroy.sh validate.sh tests/fake-aws/aws)

echo "== Line endings"
for file in "${scripts[@]}" config/*.env; do
  if grep -q $'\r' "$file"; then fail "$file has CRLF line endings"; fi
done
(( failures == 0 )) && pass "all files use LF"

echo "== Bash syntax (bash -n)"
for file in "${scripts[@]}"; do
  if bash -n "$file" 2>"$WORK_DIR/syntax.err"; then pass "$file"; else fail "$file: $(cat "$WORK_DIR/syntax.err")"; fi
done

echo "== ShellCheck"
if command -v shellcheck >/dev/null 2>&1; then
  if shellcheck -x -s bash "${scripts[@]}"; then pass "shellcheck $(shellcheck --version | sed -n 's/^version: //p')"; else fail "shellcheck findings above"; fi
elif [[ "$USE_DOCKER_SHELLCHECK" == true ]]; then
  mount_dir="$INFRA_DIR"
  command -v cygpath >/dev/null 2>&1 && mount_dir="$(cygpath -m "$INFRA_DIR")"
  if MSYS_NO_PATHCONV=1 docker run --rm -v "$mount_dir:/mnt:ro" -w /mnt "$SHELLCHECK_IMAGE" \
    -x -s bash "${scripts[@]}"; then
    pass "shellcheck ($SHELLCHECK_IMAGE)"
  else
    fail "shellcheck findings above"
  fi
else
  echo "  skip  shellcheck not installed (install it, or pass --shellcheck-docker)"
fi

echo "== Configuration"
for env in dev staging prod; do
  if (load_config "$env") 2>"$WORK_DIR/config.err"; then pass "config/$env.env"; else fail "config/$env.env: $(cat "$WORK_DIR/config.err")"; fi
done

echo "== Rendered user data"
for env in dev staging prod; do
  out="$WORK_DIR/user-data-$env.sh"
  (
    load_config "$env" 2>/dev/null
    ACCOUNT_ID=123456789012
    REGISTRY="${ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
    # shellcheck source=steps/compute.sh
    source "$INFRA_DIR/steps/compute.sh"
    render_user_data
  ) >"$out"
  size=$(wc -c <"$out")
  if ! bash -n "$out" 2>"$WORK_DIR/ud.err"; then
    fail "$env user data does not parse: $(cat "$WORK_DIR/ud.err")"
  elif (( size > 16000 )); then
    fail "$env user data is $size bytes (EC2 limit 16 KB)"
  elif ! grep -q "^SSM_PREFIX=/trackmgmt/$env$" "$out"; then
    fail "$env user data has the wrong SSM prefix"
  else
    pass "$env user data parses ($size bytes)"
  fi
done

echo "== Safety guards (fake aws: only the identity check may run)"
chmod +x "$INFRA_DIR/tests/fake-aws/aws" 2>/dev/null || true
export FAKE_AWS_LOG="$WORK_DIR/fake-aws.log"
fake_path="$INFRA_DIR/tests/fake-aws:$PATH"

expect_refusal() { # description expected-message command...
  local description="$1" expected="$2" output rc=0
  shift 2
  : >"$FAKE_AWS_LOG"
  output="$(PATH="$fake_path" "$@" </dev/null 2>&1)" || rc=$?
  if (( rc == 0 )); then
    fail "$description: exited 0"
  elif ! grep -q "$expected" <<<"$output"; then
    fail "$description: unexpected output: $(tail -n 2 <<<"$output")"
  elif grep -qv 'sts get-caller-identity\|^--version$' "$FAKE_AWS_LOG"; then
    fail "$description: made AWS calls: $(grep -v 'sts get-caller-identity\|^--version$' "$FAKE_AWS_LOG" | head -n 1)"
  else
    pass "$description"
  fi
}

expect_refusal "setup rejects an unknown environment" "must be dev, staging or prod" bash ./setup.sh --env qa
expect_refusal "setup rejects an unknown step" "Unknown step" bash ./setup.sh --env dev --only database
expect_refusal "setup needs a confirmation" "No terminal to confirm" bash ./setup.sh --env dev
expect_refusal "destroy refuses prod without --allow-prod" "without --allow-prod" bash ./destroy.sh --env prod
expect_refusal "destroy needs the exact prefix" "must be exactly" bash ./destroy.sh --env dev --confirm trackmgmt-prod
expect_refusal "destroy needs a confirmation" "No terminal to confirm" bash ./destroy.sh --env staging
expect_refusal "deploy rejects a malformed image tag" "Invalid --image-tag" bash ./deploy.sh --env dev --image-tag 'bad tag;'

echo
if (( failures == 0 )); then
  echo "All checks passed."
else
  echo "$failures check(s) failed."
  exit 1
fi
