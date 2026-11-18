# shellcheck shell=bash
# Application secrets in SSM Parameter Store (SecureString, encrypted with the AWS-managed key):
#   <SSM_PREFIX>/db-password, /jwt-signing-key, /admin-password
# Generated once and never overwritten, so re-running setup keeps the database and sign-in working.
# Values go to the AWS CLI through a private temp file, never on the command line, and are not printed.
# Cost: standard parameters are free.
#
# Read the admin password when you need it:
#   aws ssm get-parameter --region <region> --name /<project>/<env>/admin-password \
#     --with-decryption --query Parameter.Value --output text

param_exists() {
  local name
  name="$(aws_lookup 'ParameterNotFound' ssm get-parameter --name "$1" --query Parameter.Name)"
  [[ -n "$name" ]]
}

put_secure_param() {
  local name="$1" value="$2" description="$3" file="$WORK_DIR/param.json"
  printf '{"Name":"%s","Description":"%s","Value":"%s","Type":"SecureString","Tier":"Standard","Tags":%s}' \
    "$name" "$description" "$value" "$(tags_json "$name")" >"$file"
  awsr ssm put-parameter --cli-input-json "$(file_arg "$file")" >/dev/null
  rm -f "$file"
}

# Non-secret values that change (database host, deployed image tag).
put_string_param() {
  local name="$1" value="$2"
  if param_exists "$name"; then
    awsr ssm put-parameter --name "$name" --value "$value" --type String --overwrite >/dev/null
  else
    set_tags "$name"
    awsr ssm put-parameter --name "$name" --value "$value" --type String --tags "${TAGS[@]}" >/dev/null
  fi
}

get_secure_param() {
  awst ssm get-parameter --name "$SSM_PREFIX/$1" --with-decryption --query Parameter.Value
}

ensure_secret() {
  local name="$SSM_PREFIX/$1" value="$2" description="$3"
  if param_exists "$name"; then
    log "  exists  $name"
  else
    put_secure_param "$name" "$value" "$description"
    log "  created $name (SecureString)"
  fi
}

step_secrets() {
  log "Secrets (SSM Parameter Store)"
  require_cmd openssl
  ensure_secret db-password "$(random_secret 24)" "Track Management ${ENVIRONMENT} database master password"
  ensure_secret jwt-signing-key "$(random_secret 48)" "Track Management ${ENVIRONMENT} JWT HS256 signing key"
  # Prefix and suffix guarantee the admin password rules: upper, lower, symbol, digit, 12+ characters.
  ensure_secret admin-password "Tk!$(random_secret 18)9a" "Track Management ${ENVIRONMENT} admin password"
}
