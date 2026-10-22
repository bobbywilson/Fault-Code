#!/usr/bin/env bash
set -euo pipefail
# Disposable Linux acceptance with public registration IDs; no credentials or live sign-in.
name="faultcode-admin-ci-${GITHUB_RUN_ID:-$$}"
cleanup() { docker rm -f "$name-web" "$name-api" >/dev/null 2>&1 || true; docker network rm "$name" >/dev/null 2>&1 || true; }
trap cleanup EXIT
trap 'echo "Administration smoke failed at line $LINENO" >&2; docker ps -a --filter "name=$name"; docker logs --tail=30 "$name-web"; docker logs --tail=30 "$name-api"' ERR
docker build --target runtime -t "$name-api" .
docker build -t "$name-web" frontend
docker network create "$name" >/dev/null
docker run -d --name "$name-api" --network "$name" --network-alias api -e ApiTesting__Enabled=true "$name-api" >/dev/null
docker run -d --name "$name-web" --network "$name" -p 127.0.0.1::8080 "$name-web" >/dev/null
port=$(docker inspect --format '{{(index (index .NetworkSettings.Ports "8080/tcp") 0).HostPort}}' "$name-web")
for attempt in {1..40}; do
  if curl --fail --silent "http://127.0.0.1:$port/admin/incidents" > /tmp/administration-index.html; then break; fi
  sleep 1
done
grep -q '<app-root>' /tmp/administration-index.html
test "$(docker exec "$name-web" id -u)" != 0
for attempt in {1..30}; do
  status=$(curl --silent -o /tmp/administration-problem.json -w '%{http_code}' "http://127.0.0.1:$port/api/v1/vehicles")
  if test "$status" = 401; then break; fi
  sleep 1
done
test "$status" = 401
curl --fail --silent "http://127.0.0.1:$port/browser-config.json" > /tmp/administration-config.json
python3 - <<'PY'
import json
assert json.load(open('/tmp/administration-problem.json'))['status']==401
config = json.load(open('/tmp/administration-config.json'))
assert config['tenantId'] == 'c0ff26e9-dd24-4e59-b395-1e9b48354aad'
assert config['clientId'] == 'c09a3b35-fa74-4288-9706-db1287bf5634'
assert config['apiClientId'] == 'ce48cbc2-5349-4228-b5ea-365c9df1e2af'
assert len(config['scopes']) == 15
assert all(scope.startswith('api://' + config['apiClientId'] + '/') for scope in config['scopes'])
assert 'clientSecret' not in config
PY
node frontend/tools/production-smoke.mjs "http://127.0.0.1:$port"
# Test identity and demo token cannot be packaged.
if docker exec "$name-web" sh -c 'grep -R -E "demo-test-token|__test/session" /usr/share/nginx/html'; then exit 1; fi
printf 'Non-root Linux SPA, public registration configuration, fixture exclusion and protected API401 passed.\n'
