# shellcheck shell=bash
# Instance role and profile with only what the instances need:
#   - AmazonSSMManagedInstanceCore: deploys and shell access through Systems Manager (no SSH, no port 22);
#   - read this environment's parameters (/<project>/<env>/*);
#   - pull from the two ECR repositories;
#   - write container logs to this environment's log group;
#   - the S3 bucket, when enabled.
# Cost: free.

step_iam() {
  local role="${NAME_PREFIX}-ec2-role" profile="${NAME_PREFIX}-ec2-profile" existing roles
  local trust="$WORK_DIR/trust.json" policy="$WORK_DIR/policy.json" s3_statement=""
  log "IAM (instance role)"

  printf '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ec2.amazonaws.com"},"Action":"sts:AssumeRole"}]}' >"$trust"

  existing="$(aws_lookup 'NoSuchEntity' iam get-role --role-name "$role" --query Role.RoleName)"
  if [[ -z "$existing" ]]; then
    set_tags "$role"
    awsr iam create-role --role-name "$role" --assume-role-policy-document "$(file_arg "$trust")" \
      --description "Track Management ${ENVIRONMENT} EC2 instances" --tags "${TAGS[@]}" >/dev/null
    log "  created role $role"
  else
    log "  exists  role $role"
  fi

  awsr iam attach-role-policy --role-name "$role" \
    --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore

  if [[ "$S3_BUCKET_ENABLED" == true ]]; then
    s3_statement=",{\"Sid\":\"AppBucket\",\"Effect\":\"Allow\",\"Action\":[\"s3:GetObject\",\"s3:PutObject\",\"s3:DeleteObject\",\"s3:ListBucket\"],\"Resource\":[\"arn:aws:s3:::$(bucket_name)\",\"arn:aws:s3:::$(bucket_name)/*\"]}"
  fi
  cat >"$policy" <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {"Sid": "ReadAppParameters", "Effect": "Allow",
     "Action": ["ssm:GetParameter", "ssm:GetParameters"],
     "Resource": "arn:aws:ssm:${AWS_REGION}:${ACCOUNT_ID}:parameter${SSM_PREFIX}/*"},
    {"Sid": "EcrLogin", "Effect": "Allow", "Action": "ecr:GetAuthorizationToken", "Resource": "*"},
    {"Sid": "EcrPull", "Effect": "Allow",
     "Action": ["ecr:BatchGetImage", "ecr:GetDownloadUrlForLayer", "ecr:BatchCheckLayerAvailability"],
     "Resource": ["arn:aws:ecr:${AWS_REGION}:${ACCOUNT_ID}:repository/${NAME_PREFIX}-api",
                  "arn:aws:ecr:${AWS_REGION}:${ACCOUNT_ID}:repository/${NAME_PREFIX}-web"]},
    {"Sid": "ContainerLogs", "Effect": "Allow",
     "Action": ["logs:CreateLogStream", "logs:PutLogEvents"],
     "Resource": "arn:aws:logs:${AWS_REGION}:${ACCOUNT_ID}:log-group:${LOG_GROUP}:*"}${s3_statement}
  ]
}
EOF
  awsr iam put-role-policy --role-name "$role" --policy-name "${NAME_PREFIX}-app-access" \
    --policy-document "$(file_arg "$policy")"

  existing="$(aws_lookup 'NoSuchEntity' iam get-instance-profile --instance-profile-name "$profile" \
    --query InstanceProfile.InstanceProfileName)"
  if [[ -z "$existing" ]]; then
    set_tags "$profile"
    awsr iam create-instance-profile --instance-profile-name "$profile" --tags "${TAGS[@]}" >/dev/null
    awsr iam wait instance-profile-exists --instance-profile-name "$profile"
    log "  created instance profile $profile"
  else
    log "  exists  instance profile $profile"
  fi
  roles="$(awst iam get-instance-profile --instance-profile-name "$profile" \
    --query 'InstanceProfile.Roles[].RoleName')"
  if [[ "$roles" != *"$role"* ]]; then
    awsr iam add-role-to-instance-profile --instance-profile-name "$profile" --role-name "$role"
    log "  added $role to $profile; waiting for IAM to propagate"
    sleep 15
  fi
}
