#!/bin/bash
# Runs ON an EC2 instance (installed by the user data as /opt/trackmgmt/run-app.sh).
# Starts or restarts the API and web containers with the image tag stored in SSM Parameter Store.
#
#   run-app.sh            start without database migrations (every instance, every boot)
#   run-app.sh --migrate  apply migrations and seeding on startup (deploy.sh passes this to ONE instance)
#
# Secrets are read from SSM with the instance role, written to a root-only env file for `docker run`,
# and the file is removed afterwards. Nothing here prints a secret.
set -euo pipefail
# shellcheck source=/dev/null
source /etc/trackmgmt/app.conf

migrate=false
if [[ "${1:-}" == "--migrate" ]]; then
  migrate=true
fi

# One run at a time (first boot and a deploy can overlap).
exec 9>/var/lock/trackmgmt-run-app.lock
flock 9

# Wait for the first-boot setup (Docker, CA bundle) when a deploy arrives early.
for _ in $(seq 1 60); do
  [[ -f /etc/trackmgmt/bootstrap-done ]] && break
  sleep 5
done
[[ -f /etc/trackmgmt/bootstrap-done ]] || { echo "Instance setup has not finished; try again later." >&2; exit 1; }

param() {
  aws ssm get-parameter --region "$AWS_REGION" --name "$SSM_PREFIX/$1" \
    --query Parameter.Value --output text "${@:2}"
}

if ! tag="$(param image-tag 2>/dev/null)"; then
  echo "No image deployed yet ($SSM_PREFIX/image-tag is missing); nothing to start."
  exit 0
fi

db_host="$(param db-host)"
db_password="$(param db-password --with-decryption)"
jwt_key="$(param jwt-signing-key --with-decryption)"
admin_password="$(param admin-password --with-decryption)"

imds_token="$(curl -fsS -X PUT http://169.254.169.254/latest/api/token \
  -H 'X-aws-ec2-metadata-token-ttl-seconds: 300')"
instance_id="$(curl -fsS -H "X-aws-ec2-metadata-token: $imds_token" \
  http://169.254.169.254/latest/meta-data/instance-id)"

umask 077
env_file="$(mktemp /opt/trackmgmt/api.env.XXXXXX)"
trap 'rm -f "$env_file"' EXIT
cat >"$env_file" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__Default=Host=${db_host};Port=5432;Database=${DB_NAME};Username=${DB_USERNAME};Password=${db_password};SSL Mode=${DB_SSL_MODE};Root Certificate=/etc/trackmgmt/rds-ca.pem
Database__MigrateOnStartup=${migrate}
Jwt__SigningKey=${jwt_key}
Admin__Email=${ADMIN_EMAIL}
Admin__Password=${admin_password}
ReverseProxy__KnownNetworks__0=${VPC_CIDR}
EOF
unset db_password jwt_key admin_password

api_image="${REGISTRY}/${NAME_PREFIX}-api:${tag}"
web_image="${REGISTRY}/${NAME_PREFIX}-web:${tag}"

aws ecr get-login-password --region "$AWS_REGION" \
  | docker login --username AWS --password-stdin "$REGISTRY" >/dev/null
docker pull -q "$api_image" >/dev/null
docker pull -q "$web_image" >/dev/null

docker network inspect trackmgmt-net >/dev/null 2>&1 || docker network create trackmgmt-net >/dev/null

log_opts=(--log-driver awslogs --log-opt "awslogs-region=${AWS_REGION}" --log-opt "awslogs-group=${LOG_GROUP}")

docker rm -f api web >/dev/null 2>&1 || true
# The web container's nginx forwards /api to the container named "api" on this network.
docker run -d --name api --network trackmgmt-net --restart unless-stopped \
  -p 8080:8080 --env-file "$env_file" \
  -v /etc/trackmgmt/rds-ca.pem:/etc/trackmgmt/rds-ca.pem:ro \
  "${log_opts[@]}" --log-opt "awslogs-stream=${instance_id}/api" \
  "$api_image" >/dev/null
docker run -d --name web --network trackmgmt-net --restart unless-stopped \
  -p 8081:8081 \
  "${log_opts[@]}" --log-opt "awslogs-stream=${instance_id}/web" \
  "$web_image" >/dev/null

healthy=false
for _ in $(seq 1 60); do
  if curl -fsS -o /dev/null http://127.0.0.1:8080/health; then
    healthy=true
    break
  fi
  sleep 3
done
if [[ "$healthy" != true ]]; then
  echo "The API did not become healthy within 3 minutes. Last log lines:" >&2
  docker logs --tail 40 api >&2 || true
  exit 1
fi

docker image prune -af --filter "until=168h" >/dev/null 2>&1 || true
echo "Running ${tag} on ${instance_id} (migrations on startup: ${migrate})."
