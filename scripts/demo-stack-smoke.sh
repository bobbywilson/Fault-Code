#!/usr/bin/env bash
# Part of FAU-23. Disposable loopback packaging test, NOT live Entra/DNS acceptance.
set -euo pipefail
cd "$(dirname "$0")/.."
project="faultcode-demo-ci-${GITHUB_RUN_ID:-$$}-${RANDOM}"
temp=$(mktemp -d)
cleanup() {
  if [[ -s "$temp/compose.json" ]]; then
    docker compose -p "$project" -f "$temp/compose.json" down --volumes --remove-orphans >/dev/null 2>&1 || true
  fi
  rm -rf -- "$temp"
}
trap cleanup EXIT
trap 'echo "Disposable demo stack failed at line $LINENO" >&2; docker compose -p "$project" -f "$temp/compose.json" ps' ERR
source_sha=$(git rev-parse HEAD)
docker build --target runtime --build-arg "SOURCE_REVISION=$source_sha" -t "$project-api:ci" .
docker build --build-arg "SOURCE_REVISION=$source_sha" -t "$project-web:ci" frontend
docker build --build-arg "SOURCE_REVISION=$source_sha" -f mintlify/Dockerfile -t "$project-docs:ci" mintlify
docker build -f docker/Qdrant.Dockerfile --label "org.opencontainers.image.revision=$source_sha" -t "$project-qdrant:ci" .
mkdir -p "$temp/tls" "$temp/acme" "$temp/entra-api"
cp deploy/demo/{compose.yaml,nginx.conf,browser-config.json} "$temp/"
# Private test CA material stays in this temporary directory and is never trusted globally.
openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj /CN=faultcodeai.com \
  -addext subjectAltName=DNS:faultcodeai.com,DNS:www.faultcodeai.com \
  -keyout "$temp/tls/privkey.pem" -out "$temp/tls/fullchain.pem" >/dev/null 2>&1
# Container UID0 has no DAC override and differs from the host runner UID. This
# disposable test key is readable only through its private0700 parent or the
# proxy-only bind mount. Production keys remain root-owned0600.
chmod 644 "$temp/tls/privkey.pem"
export API_IMAGE="$project-api:ci" WEB_IMAGE="$project-web:ci" QDRANT_IMAGE="$project-qdrant:ci" DOCS_IMAGE="$project-docs:ci"
export FAULTCODE_QDRANT_API_KEY="$(openssl rand -hex 32)"
export FAULTCODE_SQL_SA_PASSWORD="Fc1!$(openssl rand -hex 24)"
export FAULTCODE_SQL_CONNECTION="Server=sql;Database=FaultCode;User ID=sa;Password=$FAULTCODE_SQL_SA_PASSWORD;Encrypt=True;TrustServerCertificate=True"
export FAULTCODE_AGENT_MODEL=gpt-4.1-mini-2025-04-14 FAULTCODE_EMBEDDING_MODEL=text-embedding-3-small
export FAULTCODE_OPENAI_KEY=isolated-test-key FAULTCODE_DATADOG_INCIDENT_TOKEN=isolated-test-key
export FAULTCODE_AGENTS_ENABLED=false FAULTCODE_EMBEDDINGS_ENABLED=false
export FAULTCODE_DEMO_LOGIN_ENABLED=true FAULTCODE_DEMO_USERNAME=smoketest
export FAULTCODE_DEMO_USER_ID=11111111-1111-4111-8111-111111111111
export FAULTCODE_DEMO_CLIENT_ID=22222222-2222-4222-8222-222222222222
FAULTCODE_DEMO_PASSWORD_HASH=$(python3 - <<'PY'
import base64,hashlib,secrets,struct
salt=secrets.token_bytes(16)
print(base64.b64encode(b'\x01'+struct.pack('>III',2,100000,16)+salt+hashlib.pbkdf2_hmac('sha512',b'isolated-smoke-password',salt,100000,32)).decode())
PY
)
export FAULTCODE_DEMO_PASSWORD_HASH
docker compose -p "$project" -f "$temp/compose.yaml" config --format json > "$temp/compose.json"
python3 - "$temp/compose.json" <<'PY'
import json,sys
from pathlib import Path
p=Path(sys.argv[1]); d=json.loads(p.read_text())
# No host80/443 or public binding in disposable tests; retain all other production settings.
d['services']['proxy']['ports']=[{'target':port,'published':'0','host_ip':'127.0.0.1','protocol':'tcp'} for port in [8080,8443]]
assert d['services']['api']['environment']['ASPNETCORE_ENVIRONMENT']=='Production'
assert str(d['services']['api']['environment']['Entra__Enabled']).lower()=='true'
assert not any(service.get('privileged') or service.get('network_mode')=='host' for service in d['services'].values())
assert all('cpus' in service and 'mem_limit' in service for service in d['services'].values())
assert d['networks']['data']['internal']
p.write_text(json.dumps(d))
PY
docker compose -p "$project" -f "$temp/compose.json" up -d --wait --wait-timeout 240 sql qdrant mailpit documentation
docker compose -p "$project" -f "$temp/compose.json" run --rm --no-deps -e Database__Command=migrate api
docker compose -p "$project" -f "$temp/compose.json" up -d --wait --wait-timeout 180
port=$(docker compose -p "$project" -f "$temp/compose.json" port proxy 8443 | tail -1 | awk -F: '{print $NF}')
curl_flags=(--connect-timeout 5 --max-time 15 --resolve "faultcodeai.com:$port:127.0.0.1" --cacert "$temp/tls/fullchain.pem")
curl "${curl_flags[@]}" --fail --silent --show-error "https://faultcodeai.com:$port/admin/incidents" | grep -q '<app-root>'
curl "${curl_flags[@]}" --fail --silent --show-error "https://faultcodeai.com:$port/health/ready" >/dev/null
status=$(curl "${curl_flags[@]}" --silent --output /dev/null --write-out '%{http_code}' "https://faultcodeai.com:$port/api/v1/vehicles")
test "$status" = 401
if curl "${curl_flags[@]}" --fail --silent -H 'Host: unrelated.invalid' "https://faultcodeai.com:$port/" >/dev/null; then
  echo 'Unrelated hostname unexpectedly accepted' >&2; exit 1
fi
curl "${curl_flags[@]}" --fail --silent --show-error "https://faultcodeai.com:$port/docs/" >/dev/null
curl "${curl_flags[@]}" --fail --silent --show-error "https://faultcodeai.com:$port/swagger/index.html" >/dev/null
curl "${curl_flags[@]}" --fail --silent --show-error -c "$temp/cookies" "https://faultcodeai.com:$port/session" > "$temp/session.json"
csrf=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["csrfToken"])' "$temp/session.json")
curl "${curl_flags[@]}" --fail --silent --show-error -b "$temp/cookies" -c "$temp/cookies" \
  -H 'Content-Type: application/json' -H "X-FaultCode-CSRF: $csrf" \
  --data '{"username":"smoketest","password":"isolated-smoke-password"}' "https://faultcodeai.com:$port/session/login" >/dev/null
curl "${curl_flags[@]}" --fail --silent --show-error -b "$temp/cookies" "https://faultcodeai.com:$port/session" > "$temp/authenticated.json"
python3 - "$temp/authenticated.json" <<'PY'
import json,sys
session=json.load(open(sys.argv[1])); assert session['user']['displayName']=='John Henry'
PY
curl "${curl_flags[@]}" --fail --silent --show-error -b "$temp/cookies" "https://faultcodeai.com:$port/mailpit/" >/dev/null
for service in api administration qdrant documentation; do
  container=$(docker compose -p "$project" -f "$temp/compose.json" ps -q "$service")
  test "$(docker inspect "$container" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')" = "$source_sha"
done
echo 'Disposable bounded demo stack, trusted test TLS, readiness, source labels and real API401 passed; no live login claimed.'
