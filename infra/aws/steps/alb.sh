# shellcheck shell=bash
# Internet-facing Application Load Balancer in the public subnets with two target groups:
#   <prefix>-api  port 8080, health check /health (includes the database)  ← path /api/*
#   <prefix>-web  port 8081, health check /                                ← everything else
# HTTP on port 80. With CERTIFICATE_ARN set: HTTPS on 443 and port 80 redirects to it.
# Invalid header fields are dropped; deletion protection follows ALB_DELETION_PROTECTION.
# Cost: the load balancer per hour plus load balancer capacity units (LCUs).

ensure_target_group() {
  local name="$1" port="$2" health_path="$3" vpc_id="$4" arn
  arn="$(find_tg_arn "$name")"
  if [[ -z "$arn" ]]; then
    set_tags "$name"
    arn="$(awst elbv2 create-target-group --name "$name" --protocol HTTP --port "$port" \
      --vpc-id "$vpc_id" --target-type instance \
      --health-check-protocol HTTP --health-check-path "$health_path" \
      --health-check-interval-seconds 15 --healthy-threshold-count 2 --unhealthy-threshold-count 3 \
      --matcher HttpCode=200 --tags "${TAGS[@]}" --query 'TargetGroups[0].TargetGroupArn')"
    log "  created target group $name"
  else
    log "  exists  target group $name"
  fi
  awsr elbv2 modify-target-group-attributes --target-group-arn "$arn" \
    --attributes Key=deregistration_delay.timeout_seconds,Value=30 >/dev/null
  printf '%s' "$arn"
}

ensure_listener() {
  local lb_arn="$1" port="$2" protocol="$3" actions="$4" arn
  local -a extra=()
  arn="$(awst elbv2 describe-listeners --load-balancer-arn "$lb_arn" \
    --query "Listeners[?Port==\`$port\`].ListenerArn | [0]")"
  if [[ "$protocol" == HTTPS ]]; then
    extra=(--certificates "CertificateArn=$CERTIFICATE_ARN" --ssl-policy "$ALB_SSL_POLICY")
  fi
  if [[ -z "$arn" ]]; then
    arn="$(awst elbv2 create-listener --load-balancer-arn "$lb_arn" --port "$port" \
      --protocol "$protocol" --default-actions "$actions" "${extra[@]}" \
      --query 'Listeners[0].ListenerArn')"
    log "  created listener $protocol:$port"
  else
    awsr elbv2 modify-listener --listener-arn "$arn" --port "$port" --protocol "$protocol" \
      --default-actions "$actions" "${extra[@]}" >/dev/null
    log "  updated listener $protocol:$port"
  fi
  printf '%s' "$arn"
}

# Rule priority 10: /api/* goes to the API target group.
ensure_api_rule() {
  local listener="$1" tg_api="$2" rule conditions actions
  conditions='[{"Field":"path-pattern","PathPatternConfig":{"Values":["/api/*"]}}]'
  actions="[{\"Type\":\"forward\",\"TargetGroupArn\":\"$tg_api\"}]"
  rule="$(awst elbv2 describe-rules --listener-arn "$listener" \
    --query "Rules[?Priority=='10'].RuleArn | [0]")"
  if [[ -z "$rule" ]]; then
    awsr elbv2 create-rule --listener-arn "$listener" --priority 10 \
      --conditions "$conditions" --actions "$actions" >/dev/null
    log "  created rule /api/* -> api"
  else
    awsr elbv2 modify-rule --rule-arn "$rule" --conditions "$conditions" --actions "$actions" >/dev/null
    log "  exists  rule /api/* -> api"
  fi
}

remove_api_rule() {
  local listener="$1" rule
  rule="$(awst elbv2 describe-rules --listener-arn "$listener" \
    --query "Rules[?Priority=='10'].RuleArn | [0]")"
  if [[ -n "$rule" ]]; then
    awsr elbv2 delete-rule --rule-arn "$rule"
    log "  removed /api/* rule from the HTTP listener (HTTP now redirects to HTTPS)"
  fi
}

step_alb() {
  local vpc_id alb_sg lb_arn tg_api tg_web forward_web http https subnets
  local -a public
  log "Load balancer"
  vpc_id="$(find_vpc_id)"
  [[ -n "$vpc_id" ]] || die "VPC not found; run the network step first."
  alb_sg="$(find_sg_id "$vpc_id" "${NAME_PREFIX}-alb")"
  [[ -n "$alb_sg" ]] || die "Security group ${NAME_PREFIX}-alb not found; run the security-groups step first."
  subnets="$(subnet_ids "$vpc_id" public)"
  read -r -a public <<<"$subnets"

  lb_arn="$(find_lb_arn)"
  if [[ -z "$lb_arn" ]]; then
    set_tags "${NAME_PREFIX}-alb"
    lb_arn="$(awst elbv2 create-load-balancer --name "${NAME_PREFIX}-alb" --type application \
      --scheme internet-facing --ip-address-type ipv4 --subnets "${public[@]}" \
      --security-groups "$alb_sg" --tags "${TAGS[@]}" --query 'LoadBalancers[0].LoadBalancerArn')"
    log "  created load balancer ${NAME_PREFIX}-alb"
  else
    log "  exists  load balancer ${NAME_PREFIX}-alb"
  fi
  awsr elbv2 modify-load-balancer-attributes --load-balancer-arn "$lb_arn" --attributes \
    Key=routing.http.drop_invalid_header_fields.enabled,Value=true \
    "Key=deletion_protection.enabled,Value=$ALB_DELETION_PROTECTION" >/dev/null

  tg_api="$(ensure_target_group "${NAME_PREFIX}-api" 8080 /health "$vpc_id")"
  tg_web="$(ensure_target_group "${NAME_PREFIX}-web" 8081 / "$vpc_id")"
  forward_web="[{\"Type\":\"forward\",\"TargetGroupArn\":\"$tg_web\"}]"

  if [[ -n "$CERTIFICATE_ARN" ]]; then
    https="$(ensure_listener "$lb_arn" 443 HTTPS "$forward_web")"
    ensure_api_rule "$https" "$tg_api"
    http="$(ensure_listener "$lb_arn" 80 HTTP \
      '[{"Type":"redirect","RedirectConfig":{"Protocol":"HTTPS","Port":"443","StatusCode":"HTTP_301"}}]')"
    remove_api_rule "$http"
  else
    http="$(ensure_listener "$lb_arn" 80 HTTP "$forward_web")"
    ensure_api_rule "$http" "$tg_api"
  fi
}
