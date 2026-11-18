# shellcheck shell=bash
# Three security groups that reference each other instead of IP ranges:
#   alb  80 (and 443 with a certificate) from ALB_INGRESS_CIDR
#   app  8080 (API) and 8081 (web) only from the alb group; no SSH (instances are managed through SSM)
#   db   5432 only from the app group
# Cost: free. Note: changing ALB_INGRESS_CIDR adds the new rule but does not remove the old one.

ensure_sg() {
  local vpc_id="$1" name="$2" description="$3" id
  id="$(find_sg_id "$vpc_id" "$name")"
  if [[ -z "$id" ]]; then
    id="$(awst ec2 create-security-group --vpc-id "$vpc_id" --group-name "$name" \
      --description "$description" --tag-specifications "$(tag_spec security-group "$name")" \
      --query GroupId)"
    log "  created security group $name $id"
  else
    log "  exists  security group $name $id"
  fi
  printf '%s' "$id"
}

# allow_ingress <group-id> <port> <source>: source is a security group id or a CIDR.
allow_ingress() {
  local group="$1" port="$2" source="$3" permission err="$WORK_DIR/sg.err"
  if [[ "$source" == sg-* ]]; then
    permission="[{\"IpProtocol\":\"tcp\",\"FromPort\":$port,\"ToPort\":$port,\"UserIdGroupPairs\":[{\"GroupId\":\"$source\"}]}]"
  else
    permission="[{\"IpProtocol\":\"tcp\",\"FromPort\":$port,\"ToPort\":$port,\"IpRanges\":[{\"CidrIp\":\"$source\"}]}]"
  fi
  if awsr ec2 authorize-security-group-ingress --group-id "$group" --ip-permissions "$permission" \
    >/dev/null 2>"$err"; then
    log "  allowed tcp/$port from $source"
  elif grep -q 'InvalidPermission.Duplicate' "$err"; then
    log "  exists  tcp/$port from $source"
  else
    cat "$err" >&2
    return 1
  fi
}

step_security_groups() {
  local vpc_id alb_sg app_sg db_sg
  log "Security groups"
  vpc_id="$(find_vpc_id)"
  [[ -n "$vpc_id" ]] || die "VPC ${NAME_PREFIX}-vpc not found; run the network step first."

  alb_sg="$(ensure_sg "$vpc_id" "${NAME_PREFIX}-alb" "Track Management ${ENVIRONMENT} load balancer")"
  app_sg="$(ensure_sg "$vpc_id" "${NAME_PREFIX}-app" "Track Management ${ENVIRONMENT} instances")"
  db_sg="$(ensure_sg "$vpc_id" "${NAME_PREFIX}-db" "Track Management ${ENVIRONMENT} database")"

  allow_ingress "$alb_sg" 80 "$ALB_INGRESS_CIDR"
  if [[ -n "$CERTIFICATE_ARN" ]]; then
    allow_ingress "$alb_sg" 443 "$ALB_INGRESS_CIDR"
  fi
  allow_ingress "$app_sg" 8080 "$alb_sg"
  allow_ingress "$app_sg" 8081 "$alb_sg"
  allow_ingress "$db_sg" 5432 "$app_sg"
}
