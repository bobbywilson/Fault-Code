#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Disposable project only; never reuse the developer's faultcode project/volumes.
export COMPOSE_PROJECT_NAME="faultcode-smoke-$(date +%s)-$$"
export FAULTCODE_QDRANT_API_KEY="$(openssl rand -hex 32)"
export FAULTCODE_API_PORT=0 FAULTCODE_PROXY_PORT=0
dc() { docker compose -p "$COMPOSE_PROJECT_NAME" "$@"; }
cleanup() {
  if [[ -n "${canary_path:-}" ]]; then rm -f "$canary_path"; fi
  dc down --volumes --remove-orphans >/dev/null 2>&1 || true
}
trap cleanup EXIT
python3 scripts/check-build-inputs.py
docker info >/dev/null
if env -u FAULTCODE_QDRANT_API_KEY docker compose --env-file /dev/null config --quiet 2>/dev/null; then
  echo 'Missing credential unexpectedly accepted' >&2; exit 1
fi
dc config --quiet
dc build --pull
dc up -d --wait --wait-timeout 180
port() { dc port "$1" "$2" | tail -1; }
api="http://$(port api 8080)"
proxy="http://$(port proxy 80)"
test "$(curl -fsS "$api/health/live")" = Healthy
test "$(curl -fsS "$api/health/ready")" = Healthy
test "$(curl -fsS -H 'Host: faultcode.test' "$proxy/health/live")" = Healthy
test "$(dc exec -T api id -u)" != 0
test -z "$(docker inspect "$(dc ps -q qdrant)" --format '{{range $k, $v := .NetworkSettings.Ports}}{{if $v}}published{{end}}{{end}}')"
# Secret canary never enters the API build context/image.
canary="FAU6_SECRET_CANARY_$(openssl rand -hex 12)"
canary_path="src/FaultCode.Api/.env.smoke"
test ! -e "$canary_path"
printf '%s' "$canary" > "$canary_path"
docker build --target context-check -f Dockerfile .
rm "$canary_path"
canary_path=""
image="$(dc images -q api | head -1)"
if docker run --rm --entrypoint sh "$image" -c 'find /app -name ".env*" | grep .' ; then
  echo 'Secret file entered runtime image' >&2; exit 1
fi
# Run Qdrant HTTP calls inside its network namespace, never publish its port.
q() { dc exec -T qdrant bash -c 'exec 3<>/dev/tcp/127.0.0.1/6333; body="$2"; printf "%s %s HTTP/1.1\r\nHost: qdrant\r\napi-key: %s\r\nContent-Type: application/json\r\nContent-Length: %s\r\nConnection: close\r\n\r\n%s" "$1" "$3" "$QDRANT__SERVICE__API_KEY" "${#body}" "$body" >&3; cat <&3' -- "$1" "${3:-}" "$2"; }
q PUT /collections/fau6_smoke '{"vectors":{"size":2,"distance":"Cosine"}}' | grep -q '"status":"ok"'
q PUT '/collections/fau6_smoke/points?wait=true' '{"points":[{"id":1,"vector":[0.1,0.2],"payload":{"source":"synthetic EPS"}}]}' | grep -q '"status":"ok"'
assert_point() { q GET /collections/fau6_smoke/points/1 | grep -q 'synthetic EPS'; }
dc restart qdrant api
dc up -d --wait --wait-timeout 180
assert_point
dc stop qdrant
test "$(curl -s -o /dev/null -w '%{http_code}' "$api/health/ready")" = 503
test "$(curl -fsS "$api/health/live")" = Healthy
dc start qdrant
dc up -d --wait --wait-timeout 180
test "$(curl -fsS "$api/health/ready")" = Healthy
dc down
dc up -d --wait --wait-timeout 180
assert_point
dc down --volumes
dc up -d --wait --wait-timeout 180
q GET /collections/fau6_smoke | grep -q '404 Not Found'
echo 'FAU-6 Linux container smoke passed'
