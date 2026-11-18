# shellcheck shell=bash
# RDS PostgreSQL in the private subnets: encrypted gp3 storage, not publicly accessible, reachable
# only from the app security group, automatic minor upgrades, backups kept DB_BACKUP_RETENTION_DAYS.
# RDS for PostgreSQL 15+ forces TLS (rds.force_ssl=1); the instances verify the certificate.
# The master password comes from SSM (steps/secrets.sh). Creating the database takes 5-15 minutes.
# Cost: the DB instance per hour plus storage and backup storage beyond the free allowance.

resolve_engine_version() {
  local versions
  if [[ "$DB_ENGINE_VERSION" == *.* ]]; then
    printf '%s' "$DB_ENGINE_VERSION"
    return
  fi
  versions="$(awst rds describe-db-engine-versions --engine postgres \
    --query "DBEngineVersions[?starts_with(EngineVersion, '${DB_ENGINE_VERSION}.')].EngineVersion")"
  [[ -n "$versions" ]] || die "RDS offers no PostgreSQL ${DB_ENGINE_VERSION}.x in $AWS_REGION."
  tr '\t' '\n' <<<"$versions" | sort -t. -k1,1n -k2,2n | tail -n 1
}

step_rds() {
  local id="${NAME_PREFIX}-db" subnet_group="${NAME_PREFIX}-db-subnets" vpc_id db_sg status
  local version password endpoint subnets file="$WORK_DIR/rds.json"
  local -a private
  log "Database (RDS PostgreSQL)"

  vpc_id="$(find_vpc_id)"
  [[ -n "$vpc_id" ]] || die "VPC not found; run the network step first."
  db_sg="$(find_sg_id "$vpc_id" "${NAME_PREFIX}-db")"
  [[ -n "$db_sg" ]] || die "Security group ${NAME_PREFIX}-db not found; run the security-groups step first."
  subnets="$(subnet_ids "$vpc_id" private)"
  read -r -a private <<<"$subnets"
  (( ${#private[@]} >= 2 )) || die "Two private subnets are needed for the database."

  status="$(aws_lookup 'DBSubnetGroupNotFoundFault' rds describe-db-subnet-groups \
    --db-subnet-group-name "$subnet_group" --query 'DBSubnetGroups[0].DBSubnetGroupName')"
  if [[ -z "$status" ]]; then
    set_tags "$subnet_group"
    awsr rds create-db-subnet-group --db-subnet-group-name "$subnet_group" \
      --db-subnet-group-description "Track Management ${ENVIRONMENT} database subnets" \
      --subnet-ids "${private[@]}" --tags "${TAGS[@]}" >/dev/null
    log "  created subnet group $subnet_group"
  else
    log "  exists  subnet group $subnet_group"
  fi

  status="$(aws_lookup 'DBInstanceNotFound' rds describe-db-instances \
    --db-instance-identifier "$id" --query 'DBInstances[0].DBInstanceStatus')"
  if [[ -z "$status" ]]; then
    version="$(resolve_engine_version)"
    password="$(get_secure_param db-password)"
    [[ -n "$password" ]] || die "Secret $SSM_PREFIX/db-password missing; run the secrets step first."
    cat >"$file" <<EOF
{
  "DBInstanceIdentifier": "$id",
  "DBInstanceClass": "$DB_INSTANCE_CLASS",
  "Engine": "postgres",
  "EngineVersion": "$version",
  "AllocatedStorage": $DB_ALLOCATED_STORAGE,
  "StorageType": "gp3",
  "StorageEncrypted": true,
  "MasterUsername": "$DB_USERNAME",
  "MasterUserPassword": "$password",
  "DBName": "$DB_NAME",
  "DBSubnetGroupName": "$subnet_group",
  "VpcSecurityGroupIds": ["$db_sg"],
  "PubliclyAccessible": false,
  "MultiAZ": $DB_MULTI_AZ,
  "BackupRetentionPeriod": $DB_BACKUP_RETENTION_DAYS,
  "DeletionProtection": $DB_DELETION_PROTECTION,
  "AutoMinorVersionUpgrade": true,
  "CopyTagsToSnapshot": true,
  "Tags": $(tags_json "$id")
}
EOF
    unset password
    awsr rds create-db-instance --cli-input-json "$(file_arg "$file")" >/dev/null
    rm -f "$file"
    log "  creating $id (PostgreSQL $version, $DB_INSTANCE_CLASS); waiting until it is available (5-15 min)"
  else
    log "  exists  $id ($status)"
    if [[ "$status" == available ]]; then
      # Keep the settings that are safe to change in place in line with the configuration.
      local protection=--no-deletion-protection
      [[ "$DB_DELETION_PROTECTION" == true ]] && protection=--deletion-protection
      awsr rds modify-db-instance --db-instance-identifier "$id" \
        --backup-retention-period "$DB_BACKUP_RETENTION_DAYS" "$protection" \
        --apply-immediately >/dev/null
    fi
  fi

  awsr rds wait db-instance-available --db-instance-identifier "$id"
  endpoint="$(awst rds describe-db-instances --db-instance-identifier "$id" \
    --query 'DBInstances[0].Endpoint.Address')"
  put_string_param "$SSM_PREFIX/db-host" "$endpoint"
  log "  available at $endpoint (stored in $SSM_PREFIX/db-host)"
}
