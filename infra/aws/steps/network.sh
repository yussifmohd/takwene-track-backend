# shellcheck shell=bash
# VPC with two public subnets (load balancer and instances) and two private subnets (database),
# an internet gateway and a public route table. No NAT gateway: the instances have public IPs for
# outbound traffic (ECR, SSM, package updates) but their security group accepts traffic only from
# the load balancer; the private subnets have no route out at all.
# Cost: VPC, subnets, route tables and the internet gateway are free; public IPv4 addresses
# (instances and load balancer) are billed per hour.

availability_zones() {
  if [[ -n "${AVAILABILITY_ZONES:-}" ]]; then
    printf '%s' "$AVAILABILITY_ZONES"
    return
  fi
  awst ec2 describe-availability-zones \
    --filters Name=state,Values=available Name=zone-type,Values=availability-zone \
    --query 'AvailabilityZones[].ZoneName' | tr '\t' ' '
}

ensure_subnet() {
  local vpc_id="$1" name="$2" cidr="$3" az="$4" public="$5" id
  id="$(awst ec2 describe-subnets \
    --filters "Name=vpc-id,Values=$vpc_id" "Name=tag:Name,Values=$name" --query 'Subnets[0].SubnetId')"
  if [[ -z "$id" ]]; then
    id="$(awst ec2 create-subnet --vpc-id "$vpc_id" --cidr-block "$cidr" --availability-zone "$az" \
      --tag-specifications "$(tag_spec subnet "$name")" --query Subnet.SubnetId)"
    log "  created subnet $name $id ($cidr, $az)"
  else
    log "  exists  subnet $name $id"
  fi
  if [[ "$public" == true ]]; then
    awsr ec2 modify-subnet-attribute --subnet-id "$id" --map-public-ip-on-launch
  fi
}

step_network() {
  local vpc_id zones igw_id attached rt_id route subnet assoc i
  local -a azs public private public_ids
  log "Network"

  vpc_id="$(find_vpc_id)"
  if [[ -z "$vpc_id" ]]; then
    vpc_id="$(awst ec2 create-vpc --cidr-block "$VPC_CIDR" \
      --tag-specifications "$(tag_spec vpc "${NAME_PREFIX}-vpc")" --query Vpc.VpcId)"
    awsr ec2 wait vpc-available --vpc-ids "$vpc_id"
    log "  created vpc $vpc_id ($VPC_CIDR)"
  else
    log "  exists  vpc $vpc_id"
  fi
  awsr ec2 modify-vpc-attribute --vpc-id "$vpc_id" --enable-dns-support '{"Value":true}'
  awsr ec2 modify-vpc-attribute --vpc-id "$vpc_id" --enable-dns-hostnames '{"Value":true}'

  zones="$(availability_zones)"
  read -r -a azs <<<"$zones"
  read -r -a public <<<"$PUBLIC_SUBNET_CIDRS"
  read -r -a private <<<"$PRIVATE_SUBNET_CIDRS"
  (( ${#azs[@]} >= ${#public[@]} && ${#azs[@]} >= ${#private[@]} )) \
    || die "Region $AWS_REGION offers ${#azs[@]} zone(s); the configuration needs ${#public[@]}."
  for i in "${!public[@]}"; do
    ensure_subnet "$vpc_id" "${NAME_PREFIX}-public-$((i + 1))" "${public[$i]}" "${azs[$i]}" true
  done
  for i in "${!private[@]}"; do
    ensure_subnet "$vpc_id" "${NAME_PREFIX}-private-$((i + 1))" "${private[$i]}" "${azs[$i]}" false
  done

  igw_id="$(awst ec2 describe-internet-gateways \
    --filters "Name=tag:Name,Values=${NAME_PREFIX}-igw" "Name=tag:Project,Values=${PROJECT_TAG}" \
    --query 'InternetGateways[0].InternetGatewayId')"
  if [[ -z "$igw_id" ]]; then
    igw_id="$(awst ec2 create-internet-gateway \
      --tag-specifications "$(tag_spec internet-gateway "${NAME_PREFIX}-igw")" \
      --query InternetGateway.InternetGatewayId)"
    log "  created internet gateway $igw_id"
  else
    log "  exists  internet gateway $igw_id"
  fi
  attached="$(awst ec2 describe-internet-gateways --internet-gateway-ids "$igw_id" \
    --query 'InternetGateways[0].Attachments[0].VpcId')"
  if [[ -z "$attached" ]]; then
    awsr ec2 attach-internet-gateway --internet-gateway-id "$igw_id" --vpc-id "$vpc_id"
  elif [[ "$attached" != "$vpc_id" ]]; then
    die "Internet gateway $igw_id is attached to another VPC ($attached)."
  fi

  rt_id="$(awst ec2 describe-route-tables \
    --filters "Name=vpc-id,Values=$vpc_id" "Name=tag:Name,Values=${NAME_PREFIX}-public-rt" \
    --query 'RouteTables[0].RouteTableId')"
  if [[ -z "$rt_id" ]]; then
    rt_id="$(awst ec2 create-route-table --vpc-id "$vpc_id" \
      --tag-specifications "$(tag_spec route-table "${NAME_PREFIX}-public-rt")" \
      --query RouteTable.RouteTableId)"
    log "  created route table $rt_id"
  else
    log "  exists  route table $rt_id"
  fi
  route="$(awst ec2 describe-route-tables --route-table-ids "$rt_id" \
    --query "RouteTables[0].Routes[?DestinationCidrBlock=='0.0.0.0/0'].GatewayId | [0]")"
  if [[ -z "$route" ]]; then
    awsr ec2 create-route --route-table-id "$rt_id" --destination-cidr-block 0.0.0.0/0 \
      --gateway-id "$igw_id" >/dev/null
  fi

  subnet="$(subnet_ids "$vpc_id" public)"
  read -r -a public_ids <<<"$subnet"
  for subnet in "${public_ids[@]}"; do
    assoc="$(awst ec2 describe-route-tables --route-table-ids "$rt_id" \
      --query "RouteTables[0].Associations[?SubnetId=='$subnet'].RouteTableAssociationId | [0]")"
    if [[ -z "$assoc" ]]; then
      awsr ec2 associate-route-table --route-table-id "$rt_id" --subnet-id "$subnet" >/dev/null
    fi
  done
  # Private subnets stay on the VPC's main route table, which only routes inside the VPC.
}
