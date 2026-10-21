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
docker build -f docker/Qdrant.Dockerfile --label "org.opencontainers.image.revision=$source_sha" -t "$project-qdrant:ci" .
mkdir -p "$temp/tls" "$temp/acme"
cp deploy/demo/{compose.yaml,nginx.conf,browser-config.json} "$temp/"
# Private test CA material stays in this temporary directory and is never trusted globally.
openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj /CN=faultcodeai.com \
  -addext subjectAltName=DNS:faultcodeai.com,DNS:www.faultcodeai.com \
  -keyout "$temp/tls/privkey.pem" -out "$temp/tls/fullchain.pem" >/dev/null 2>&1
# Container UID0 has no DAC override and differs from the host runner UID. This
# disposable test key is readable only through its private0700 parent or the
# proxy-only bind mount. Production keys remain root-owned0600.
chmod 644 "$temp/tls/privkey.pem"
export API_IMAGE="$project-api:ci" WEB_IMAGE="$project-web:ci" QDRANT_IMAGE="$project-qdrant:ci"
export FAULTCODE_QDRANT_API_KEY="$(openssl rand -hex 32)"
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
docker compose -p "$project" -f "$temp/compose.json" up -d --wait --wait-timeout 180
port=$(docker compose -p "$project" -f "$temp/compose.json" port proxy 8443 | tail -1 | awk -F: '{print $NF}')
curl_flags=(--connect-timeout 5 --max-time 15 --resolve "faultcodeai.com:$port:127.0.0.1" --cacert "$temp/tls/fullchain.pem")
curl "${curl_flags[@]}" --fail --silent --show-error "https://faultcodeai.com:$port/admin/incidents" | grep -q '<app-root>'
curl "${curl_flags[@]}" --fail --silent --show-error "https://faultcodeai.com:$port/api/v1/health" >/dev/null
status=$(curl "${curl_flags[@]}" --silent --output /dev/null --write-out '%{http_code}' "https://faultcodeai.com:$port/api/v1/authorization/me")
test "$status" = 401
for service in api administration qdrant; do
  container=$(docker compose -p "$project" -f "$temp/compose.json" ps -q "$service")
  test "$(docker inspect "$container" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')" = "$source_sha"
done
echo 'Disposable bounded demo stack, trusted test TLS, readiness, source labels and real API401 passed; no live login claimed.'
