# shellcheck shell=bash
# CloudWatch log group for the containers, and the optional private S3 bucket.
# Cost: log ingestion and storage per GB (kept LOG_RETENTION_DAYS); the bucket costs nothing while empty.

step_logs() {
  local existing
  log "Logs (CloudWatch)"
  existing="$(awst logs describe-log-groups --log-group-name-prefix "$LOG_GROUP" \
    --query "logGroups[?logGroupName=='$LOG_GROUP'].logGroupName | [0]")"
  if [[ -z "$existing" ]]; then
    awsr logs create-log-group --log-group-name "$LOG_GROUP" \
      --tags "Project=${PROJECT_TAG},Environment=${ENVIRONMENT}"
    log "  created $LOG_GROUP"
  else
    log "  exists  $LOG_GROUP"
  fi
  awsr logs put-retention-policy --log-group-name "$LOG_GROUP" --retention-in-days "$LOG_RETENTION_DAYS"
}

step_s3() {
  local bucket existing file="$WORK_DIR/s3.json"
  if [[ "$S3_BUCKET_ENABLED" != true ]]; then
    log "S3 bucket: skipped (S3_BUCKET_ENABLED=false)"
    return 0
  fi
  log "S3 bucket"
  bucket="$(bucket_name)"
  existing="$(awst s3api list-buckets --query "Buckets[?Name=='$bucket'].Name | [0]")"
  if [[ -z "$existing" ]]; then
    if [[ "$AWS_REGION" == us-east-1 ]]; then
      awsr s3api create-bucket --bucket "$bucket" >/dev/null
    else
      awsr s3api create-bucket --bucket "$bucket" \
        --create-bucket-configuration "LocationConstraint=$AWS_REGION" >/dev/null
    fi
    awsr s3api wait bucket-exists --bucket "$bucket"
    log "  created s3://$bucket"
  else
    log "  exists  s3://$bucket"
  fi

  awsr s3api put-public-access-block --bucket "$bucket" --public-access-block-configuration \
    BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true
  awsr s3api put-bucket-ownership-controls --bucket "$bucket" \
    --ownership-controls 'Rules=[{ObjectOwnership=BucketOwnerEnforced}]'
  awsr s3api put-bucket-encryption --bucket "$bucket" --server-side-encryption-configuration \
    '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"},"BucketKeyEnabled":true}]}'
  awsr s3api put-bucket-versioning --bucket "$bucket" --versioning-configuration Status=Enabled
  awsr s3api put-bucket-tagging --bucket "$bucket" --tagging \
    "TagSet=[{Key=Name,Value=$bucket},{Key=Project,Value=$PROJECT_TAG},{Key=Environment,Value=$ENVIRONMENT}]"

  printf '{"Rules":[{"ID":"expire-old-versions","Status":"Enabled","Filter":{},"NoncurrentVersionExpiration":{"NoncurrentDays":30},"AbortIncompleteMultipartUpload":{"DaysAfterInitiation":7}}]}' >"$file"
  awsr s3api put-bucket-lifecycle-configuration --bucket "$bucket" \
    --lifecycle-configuration "$(file_arg "$file")"

  printf '{"Version":"2012-10-17","Statement":[{"Sid":"DenyInsecureTransport","Effect":"Deny","Principal":"*","Action":"s3:*","Resource":["arn:aws:s3:::%s","arn:aws:s3:::%s/*"],"Condition":{"Bool":{"aws:SecureTransport":"false"}}}]}' \
    "$bucket" "$bucket" >"$file"
  awsr s3api put-bucket-policy --bucket "$bucket" --policy "$(file_arg "$file")"
}
