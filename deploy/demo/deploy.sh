#!/usr/bin/env bash
# Part of FAU-23. Install root-owned; deployment account must not own configuration.
set -Eeuo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
umask 077
[[ $EUID == 0 && $# == 5 ]] || exit 64
if [[ ${FAULTCODE_BOUNDED_DEPLOY:-} != 1 ]]; then
  exec env FAULTCODE_BOUNDED_DEPLOY=1 timeout --signal=TERM --kill-after=240s 600 "$0" "$@"
fi
source_sha=$1; api=$2; web=$3; vector=$4; docs=$5
[[ "$source_sha" =~ ^[0-9a-f]{40}$ ]] || exit 64
for digest in "$api" "$web" "$vector" "$docs"; do [[ "$digest" =~ ^sha256:[0-9a-f]{64}$ ]] || exit 64; done
[[ $(hostname -s) == lalai-staging-runner ]] || { echo 'Unexpected host' >&2; exit 65; }
root=/opt/faultcode-demo
cd "$root"
for path in "$root" compose.yaml runtime.env nginx.conf browser-config.json; do
  [[ $(stat -c %u "$path") == 0 ]] || { echo 'Unsafe deployment ownership' >&2; exit 65; }
  [[ -z $(find "$path" -maxdepth 0 -perm /022 -print) ]] || { echo 'Writable deployment configuration' >&2; exit 65; }
done
[[ $(stat -c %a runtime.env) == 600 ]] || { echo 'Secret file permissions must be0600' >&2; exit 65; }
[[ -s tls/fullchain.pem && -s tls/privkey.pem ]] || { echo 'Valid TLS bootstrap required' >&2; exit 65; }
openssl x509 -in tls/fullchain.pem -noout -checkend 86400 >/dev/null
docker compose version >/dev/null
[[ $(awk '/MemAvailable:/ {print $2}' /proc/meminfo) -ge 3145728 ]] || { echo 'Insufficient memory headroom' >&2; exit 65; }
[[ $(df -Pk "$root" | awk 'NR==2 {print $4}') -ge 10485760 ]] || { echo 'Insufficient disk headroom' >&2; exit 65; }
exec 9>/run/lock/faultcode-demo-deploy.lock
flock -n 9 || { echo 'Fault Code deployment already running' >&2; exit 75; }
mkdir -p releases
prior_release=''
if [[ -f current.env || -f current-release ]]; then
  [[ -f current.env && -f current-release ]] || { echo 'Incomplete accepted release state' >&2; exit 65; }
  prior_release=$(cat current-release)
  [[ "$prior_release" =~ ^[0-9a-f]{40}-[0-9a-f]{64}$ ]] || exit 65
  [[ -s "releases/$prior_release/manifest.env" ]] || exit 65
fi
authdir=$(mktemp -d /run/faultcode-registry.XXXXXX)
trap 'rm -rf -- "$authdir"' EXIT
# Short-lived read-only job token arrives on stdin, never in arguments or logs.
docker --config "$authdir" login ghcr.io -u bobbywilson --password-stdin >/dev/null
export DOCKER_CONFIG="$authdir"
candidate=$(mktemp "$root/releases/candidate.XXXXXX")
printf 'API_IMAGE=ghcr.io/bobbywilson/fault-code-api@%s\nWEB_IMAGE=ghcr.io/bobbywilson/fault-code-administration@%s\nQDRANT_IMAGE=ghcr.io/bobbywilson/fault-code-qdrant@%s\nDOCS_IMAGE=ghcr.io/bobbywilson/fault-code-docs@%s\n' "$api" "$web" "$vector" "$docs" > "$candidate"
compose() { docker compose --project-name faultcode-demo --env-file runtime.env --env-file "$1" --file compose.yaml "${@:2}"; }
# Pull immutable images before accepting any image-supplied configuration.
for image in "ghcr.io/bobbywilson/fault-code-api@$api" "ghcr.io/bobbywilson/fault-code-administration@$web" "ghcr.io/bobbywilson/fault-code-qdrant@$vector" "ghcr.io/bobbywilson/fault-code-docs@$docs"; do docker pull --quiet "$image" >/dev/null; done
for image in "ghcr.io/bobbywilson/fault-code-api@$api" "ghcr.io/bobbywilson/fault-code-administration@$web" "ghcr.io/bobbywilson/fault-code-qdrant@$vector" "ghcr.io/bobbywilson/fault-code-docs@$docs"; do
  [[ $(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$source_sha" ]] || { echo 'Image source mismatch' >&2; exit 65; }
done
verify() {
  curl --connect-timeout 5 --max-time 15 --fail --silent --show-error --resolve faultcodeai.com:443:127.0.0.1 https://faultcodeai.com/ >/dev/null
  curl --connect-timeout 5 --max-time 15 --fail --silent --show-error --resolve faultcodeai.com:443:127.0.0.1 https://faultcodeai.com/health/ready >/dev/null
  [[ $(curl --connect-timeout 5 --max-time 15 --silent --output /dev/null --write-out '%{http_code}' --resolve faultcodeai.com:443:127.0.0.1 https://faultcodeai.com/api/v1/vehicles) == 401 ]]
  curl --connect-timeout 5 --max-time 15 --fail --silent --show-error --resolve faultcodeai.com:443:127.0.0.1 https://faultcodeai.com/docs/ >/dev/null
  [[ $(curl --connect-timeout 5 --max-time 15 --silent --output /dev/null --write-out '%{http_code}' --resolve faultcodeai.com:443:127.0.0.1 https://faultcodeai.com/mailpit/) == 302 ]]
}
rollback() {
  trap - ERR HUP INT TERM
  if [[ -n "$prior_release" ]]; then
    for file in compose.yaml nginx.conf browser-config.json runtime.env; do
      cp -p "releases/$prior_release/$file" "$file"
    done
    cp "releases/$prior_release/manifest.env" current.env
    printf '%s\n' "$prior_release" > current-release
    printf '%s\n' "${prior_release%%-*}" > current-source
    if compose current.env up -d --remove-orphans --wait --wait-timeout 180 &&
       compose current.env exec -T proxy nginx -t &&
       compose current.env exec -T proxy nginx -s reload && verify; then
      echo 'Candidate failed; previous Fault Code release restored and verified' >&2
    else echo 'Fault Code rollback needs operator attention; LALAI untouched by deployment commands' >&2; fi
  else
    compose "$candidate" down || true
    rm -f current.env current-source current-release
    echo 'First candidate failed; no prior release exists; Fault Code volumes retained' >&2
  fi
  exit 1
}
trap rollback ERR HUP INT TERM
config_container=$(docker create "ghcr.io/bobbywilson/fault-code-api@$api")
configuration_dir=$(mktemp -d "$root/releases/configuration.XXXXXX")
docker cp "$config_container:/deployment/." "$configuration_dir/"
docker rm "$config_container" >/dev/null
# Preserve existing bind-mount inodes so nginx reload and browser config reads
# see this release's contents. install replaces the inode behind a running mount.
for file in compose.yaml nginx.conf browser-config.json; do
  cat "$configuration_dir/$file" > "$root/$file"
  chmod 644 "$root/$file"
done
compose "$candidate" config --quiet
compose "$candidate" pull --quiet
compose "$candidate" up -d --wait --wait-timeout 240 sql qdrant mailpit documentation
python3 "$configuration_dir/prepare-database.py" "$candidate"
compose "$candidate" up -d --wait --wait-timeout 180
compose "$candidate" exec -T proxy nginx -t
compose "$candidate" exec -T proxy nginx -s reload
verify
for service in api administration qdrant documentation; do
  container=$(compose "$candidate" ps -q "$service")
  [[ -n "$container" && $(docker inspect "$container" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}') == "$source_sha" ]]
done
configuration=$(sha256sum compose.yaml nginx.conf browser-config.json runtime.env | sha256sum | awk '{print $1}')
release_id="$source_sha-$configuration"
mkdir -p "releases/$release_id"
for file in compose.yaml nginx.conf browser-config.json runtime.env; do
  cp -p "$file" "releases/$release_id/$file"
done
cp "$candidate" "releases/$release_id/manifest.env"
if [[ -n "$prior_release" && "$prior_release" != "$release_id" ]]; then
  cp current.env previous.env
  printf '%s\n' "$prior_release" > previous-release
fi
mv "$candidate" current.env
printf '%s\n' "$source_sha" > current-source
printf '%s\n' "$release_id" > current-release.next
mv current-release.next current-release
printf 'Fault Code deployed source=%s; scoped services verified\n' "$source_sha"
