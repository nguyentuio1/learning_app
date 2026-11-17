#!/usr/bin/env bash
# LA-47: starts the full Docker package from a clean state and checks it end to end through the
# web proxy: portal page, SPA fallback, login, a database read, an avatar upload to MinIO and the
# public avatar URL. Used by .github/workflows/docker.yml; can also be run locally (needs Docker,
# curl, python3). It creates a throw-away .env only when none exists, and removes its containers
# and volumes at the end unless KEEP_STACK=1.
set -euo pipefail
cd "$(dirname "$0")/.."

PROJECT=${COMPOSE_PROJECT_NAME:-drone-smoke}
WEB=http://localhost:${WEB_HOST_PORT:-8080}
API=http://localhost:${API_HOST_PORT:-5055}
created_env=0

cleanup() {
  status=$?
  if [ $status -ne 0 ]; then
    echo "::group::docker compose logs"; docker compose -p "$PROJECT" logs --no-color --tail=200 || true; echo "::endgroup::"
  fi
  if [ "${KEEP_STACK:-0}" != 1 ]; then docker compose -p "$PROJECT" down -v --remove-orphans >/dev/null 2>&1 || true; fi
  if [ $created_env = 1 ]; then rm -f .env; fi
  exit $status
}
trap cleanup EXIT

echo "1. compose refuses to start without the secrets (LA-328)"
if env -i PATH="$PATH" HOME="$HOME" docker compose --env-file /dev/null config -q 2>/dev/null; then
  echo "expected docker compose to fail without the .env values"; exit 1
fi

if [ ! -f .env ]; then
  rnd() { python3 -c "import secrets; print(secrets.token_urlsafe($1))"; }
  cat > .env <<ENV
POSTGRES_PASSWORD=$(rnd 18)
MINIO_ROOT_USER=smoke-minio
MINIO_ROOT_PASSWORD=$(rnd 18)
JWT_KEY=$(rnd 48)
ADMIN_PASSWORD=Smoke-$(rnd 9)1a
ENV
  created_env=1
fi
set -a; . ./.env; set +a
ADMIN_EMAIL=${ADMIN_EMAIL:-admin@smartdronedelivery.com}

echo "2. one command starts the full system"
docker compose -p "$PROJECT" up -d --build --wait --wait-timeout 300
[ "$(docker inspect -f '{{.State.Health.Status}}' "$(docker compose -p "$PROJECT" ps -q api)")" = healthy ]   || { echo "api container is not healthy"; exit 1; }

wait_for() { # url, expected status, attempts
  for _ in $(seq 1 "${3:-60}"); do
    code=$(curl -s -o /dev/null -w '%{http_code}' "$1" || true)
    [ "$code" = "$2" ] && return 0
    sleep 3
  done
  echo "timeout waiting for $1 to return $2 (last: ${code:-none})"; return 1
}
login_body=$(printf '{"email":"%s","password":"%s"}' "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
login() { curl -sf -X POST "$1/api/auth/login" -H 'Content-Type: application/json' -d "$login_body"; }

wait_for "$WEB/" 200
for _ in $(seq 1 60); do token_json=$(login "$WEB" || true); [ -n "$token_json" ] && break; sleep 3; done
[ -n "$token_json" ] || { echo "administrator login through the web proxy failed"; exit 1; }
TOKEN=$(python3 -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])' <<<"$token_json")

echo "3. portal and SPA fallback"
curl -sf "$WEB/" | grep -qi '<div id="root"' || { echo "portal index.html not served"; exit 1; }
curl -sf "$WEB/orders/some/client/route" | grep -qi '<div id="root"' || { echo "SPA fallback missing"; exit 1; }

echo "4. API reachable directly (mobile apps) and through the proxy, with the database"
curl -sf "$API/health/live" | grep -q '"status":"Healthy"'
curl -sf "$API/health/ready" | grep -q '"status":"Healthy"' || { echo "readiness is not Healthy"; exit 1; }
login "$API" >/dev/null
curl -sf "$WEB/api/landing-stations" -H "Authorization: Bearer $TOKEN" >/dev/null

echo "5. avatar upload goes to MinIO and its public URL is readable"
python3 -c "import base64,sys; sys.stdout.buffer.write(base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=='))" > /tmp/smoke-avatar.png
avatar_json=$(curl -sf -X POST "$WEB/api/users/me/avatar" -H "Authorization: Bearer $TOKEN" -F "file=@/tmp/smoke-avatar.png;type=image/png")
AVATAR_URL=$(python3 -c 'import json,sys; print(json.load(sys.stdin)["avatarUrl"])' <<<"$avatar_json")
echo "   $AVATAR_URL"
curl -sf -o /dev/null "$AVATAR_URL"

echo "6. a restart keeps the data and does not seed twice"
docker compose -p "$PROJECT" restart api
for _ in $(seq 1 40); do login "$API" >/dev/null 2>&1 && break; sleep 3; done
login "$API" >/dev/null
admins=$(docker compose -p "$PROJECT" exec -T postgres psql -U postgres -d drone_delivery_db -tAc \
  "select count(*) from users where \"Email\" = '$ADMIN_EMAIL'")
[ "$admins" = 1 ] || { echo "expected one administrator, found $admins"; exit 1; }

echo "Docker package smoke test passed."
