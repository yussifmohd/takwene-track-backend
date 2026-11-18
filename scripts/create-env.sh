#!/usr/bin/env bash
# Creates .env from .env.example with fresh random local secrets, so the project starts with
# `docker compose up -d --build` and nothing to fill in by hand.
#
# Usage, from the repository root (macOS, Linux, Git Bash on Windows):
#     bash scripts/create-env.sh
#
# - Never overwrites an existing .env.
# - Generates the JWT signing key, the database password and the admin password.
# - Picks the first free local port for the database (5432, then 5433-5435).
# - Prints the admin email and password to sign in with (also stored in .env, which git ignores).
set -euo pipefail
cd "$(dirname "$0")/.."

if [ -e .env ]; then
  echo ".env already exists - nothing changed. Delete it first if you want a new one."
  exit 1
fi

random_text() { head -c "$1" /dev/urandom | base64 | tr -d '\n'; }

signing_key=$(random_text 48)
db_password=$(random_text 24 | tr -d '+/=')
# Prefix and suffix guarantee the password rules: upper case, lower case, a symbol and a digit.
admin_password="Tk!$(random_text 18 | tr -d '+/=')9a"

port=""
for candidate in 5432 5433 5434 5435; do
  if ! (exec 3<>"/dev/tcp/127.0.0.1/$candidate") 2>/dev/null; then port=$candidate; break; fi
done
if [ -z "$port" ]; then
  echo "Ports 5432-5435 are all in use; set POSTGRES_HOST_PORT in .env to a free port before starting."
  port=5432
fi

tr -d '\r' < .env.example | sed \
  -e "s|^JWT__SIGNINGKEY=.*|JWT__SIGNINGKEY=$signing_key|" \
  -e "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$db_password|" \
  -e "s|^ADMIN__PASSWORD=.*|ADMIN__PASSWORD=$admin_password|" \
  -e "s|^POSTGRES_HOST_PORT=.*|POSTGRES_HOST_PORT=$port|" > .env

admin_email=$(sed -n 's/^ADMIN__EMAIL=//p' .env)
echo ""
echo "Created .env with new random secrets (database on localhost:$port)."
echo "Sign in to the web app with:"
echo "  email:    $admin_email"
echo "  password: $admin_password"
echo ""
echo "Next: docker compose up -d --build"
