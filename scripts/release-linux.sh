#!/usr/bin/env bash
# Part of FAU-23. Disposable Linux acceptance; no registry, host or developer volumes.
set -euo pipefail
cd "$(dirname "$0")/.."
git diff --quiet HEAD -- || { echo 'Candidate requires a clean tracked checkout' >&2; exit 1; }
test -z "$(git ls-files --others --exclude-standard)" || { echo 'Candidate has untracked files' >&2; exit 1; }
source_sha="$(git rev-parse HEAD)"
base_sha="$(git rev-parse "${RELEASE_BASE_SHA:?Set RELEASE_BASE_SHA to the approved prior commit}")"
[[ "$source_sha" =~ ^[0-9a-f]{40}$ && "$base_sha" =~ ^[0-9a-f]{40}$ ]]
task="faultcode-release-${source_sha:0:12}-$$"
previous="faultcode-api:$base_sha"
candidate="faultcode-api:$source_sha"
admin="faultcode-admin:$source_sha"
# Separate tags remain distinct even when replaying this script on an unchanged source.
[[ "$base_sha" != "$source_sha" ]]
scratch="$(mktemp -d)"
mkdir -p artifacts/release
cleanup() {
  docker rm -f "$task" "$task-invalid" >/dev/null 2>&1 || true
  docker network rm "$task" >/dev/null 2>&1 || true
  rm -rf "$scratch"
}
trap cleanup EXIT
docker info >/dev/null
git archive "$base_sha" | tar -x -C "$scratch"
docker build --label "org.opencontainers.image.revision=$base_sha" -t "$previous" "$scratch"
docker build --label "org.opencontainers.image.revision=$source_sha" -t "$candidate" .
docker build --label "org.opencontainers.image.revision=$source_sha" -t "$admin" -f frontend/Dockerfile frontend
docker network create "$task" >/dev/null
: > artifacts/release/rollback.jsonl
start() {
  local image="$1" revision="$2" config="$3" stage="$4"
  docker run -d --name "$task" --network "$task" -p 127.0.0.1::8080 \
    -e "FaultCode__ApplicationName=$config" "$image" >/dev/null
  probe "$image" "$revision" "$config" "$stage"
}
probe() {
  local image="$1" revision="$2" config="$3" stage="$4" address
  test "$(docker inspect "$task" --format '{{.Image}}')" = "$(docker image inspect "$image" --format '{{.Id}}')"
  test "$(docker inspect "$task" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')" = "$revision"
  docker inspect "$task" --format '{{range .Config.Env}}{{println .}}{{end}}' | grep -Fxq "FaultCode__ApplicationName=$config"
  address="http://$(docker port "$task" 8080/tcp | tail -1)"
  local healthy=false
  for attempt in {1..60}; do
    if [[ "$(curl --connect-timeout 2 --max-time 5 -s "$address/health/live" || true)" = Healthy ]]; then healthy=true; break; fi
    sleep 1
  done
  test "$healthy" = true
  test "$(curl --connect-timeout 2 --max-time 15 -fsS "$address/health/ready")" = Healthy
  test "$(curl --connect-timeout 2 --max-time 15 -s -o /dev/null -w '%{http_code}' "$address/api/v1/vehicles")" = 401
  test "$(docker exec "$task" id -u)" != 0
  # Public fixtures must remain identical to the running prior source after rollback.
  curl --connect-timeout 2 --max-time 15 -fsS "$address/openapi/v1.json" > "artifacts/release/$stage-contract.json"
  python3 - "$stage" "$revision" "$config" "$(docker inspect "$task" --format '{{.Image}}')" <<'PY'
import json,sys
with open('artifacts/release/rollback.jsonl', 'a') as f:
    f.write(json.dumps(dict(zip(['stage','source','applicationName','imageId'],sys.argv[1:])),sort_keys=True)+'\n')
PY
}
start "$previous" "$base_sha" 'Demo prior configuration' prior
docker rm -f "$task" >/dev/null
start "$candidate" "$source_sha" 'Demo candidate configuration' candidate
docker restart "$task" >/dev/null
probe "$candidate" "$source_sha" 'Demo candidate configuration' restart
docker rm -f "$task" >/dev/null
start "$previous" "$base_sha" 'Demo prior configuration' rollback
python3 - <<'PY'
import json
from pathlib import Path
prior=json.loads(Path('artifacts/release/prior-contract.json').read_text())
rollback=json.loads(Path('artifacts/release/rollback-contract.json').read_text())
# Older releases advertise the request origin. Disposable containers receive a
# new loopback port on recreation; all endpoint/schema/auth content must match.
prior.pop('servers',None); rollback.pop('servers',None)
assert prior==rollback, 'Rollback changed the prior endpoint contract'
PY
# Invalid configuration must stop startup rather than silently start with defaults.
docker run -d --name "$task-invalid" --network "$task" -e 'FaultCode__ApplicationName= ' "$candidate" >/dev/null
invalid_stopped=false
for attempt in {1..30}; do
  if [[ "$(docker inspect "$task-invalid" --format '{{.State.Status}}')" = exited ]]; then invalid_stopped=true; break; fi
  sleep 1
done
test "$invalid_stopped" = true
test "$(docker inspect "$task-invalid" --format '{{.State.ExitCode}}')" != 0
# Save private, source-versioned Linux artifacts; publication/promotion is separate.
docker save "$candidate" | gzip -n > "artifacts/release/api-$source_sha.tar.gz"
docker save "$admin" | gzip -n > "artifacts/release/administration-$source_sha.tar.gz"
python3 - "$source_sha" "$base_sha" "$candidate" "$admin" <<'PY'
import hashlib,json,pathlib,subprocess,sys
p=pathlib.Path('artifacts/release')
images=[]
for tag in sys.argv[3:]:
    item=json.loads(subprocess.check_output(['docker','image','inspect',tag]))[0]
    assert item['Os']=='linux'
    assert item['Config']['Labels']['org.opencontainers.image.revision']==sys.argv[1]
    images.append({'tag':tag,'id':item['Id'],'platform':item['Os']+'/'+item['Architecture'],'source':sys.argv[1]})
manifest={'source':sys.argv[1],'rollbackSource':sys.argv[2],'images':images,'scope':'disposable image rollback; no durable schema, host or live provider acceptance'}
(p/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
with (p/'SHA256SUMS').open('w') as sums:
    for f in sorted(p.iterdir()):
        if f.name!='SHA256SUMS' and f.is_file():
            h=hashlib.file_digest(f.open('rb'),'sha256').hexdigest()
            sums.write(f'{h}  {f.name}\n')
print(json.dumps(manifest))
PY
echo 'FAU-23 Linux artifact/restart/rollback/configuration acceptance passed'
