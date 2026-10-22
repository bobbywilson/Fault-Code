#!/usr/bin/env bash
# Feed a short-lived repository runner-registration token on stdin; never log it.
set -Eeuo pipefail
[[ $EUID == 0 ]] || exit 64
umask 077
base=/var/lib/faultcode-ci
uid=$(id -u faultcode-ci)
registration=$(mktemp "$base/registration.XXXXXX")
trap 'rm -f "$registration"' EXIT
cat > "$registration"
[[ $(stat -c %s "$registration") -gt 10 && $(stat -c %s "$registration") -lt 4096 ]] || exit 64
chown faultcode-ci:faultcode-ci "$registration"
ci() { runuser -u faultcode-ci -- env HOME="$base" DOCKER_HOST="unix:///run/user/$uid/docker.sock" docker "$@"; }
for kind in ci ci-secondary gate; do
  install -d -m 700 -o faultcode-ci -g faultcode-ci "$base/$kind"
  if [[ ! -f "$base/$kind/runner/.runner" ]]; then
    ci run --rm -e "RUNNER_ROOT=$base/$kind" -v "$base/$kind:$base/$kind" -v "$registration:/run/registration-token:ro" faultcode-ci-runner:2.338.0 configure
  fi
  if ci container inspect "faultcode-$kind-agent" >/dev/null 2>&1; then continue; fi
  extra=()
  if [[ $kind != gate ]]; then extra=(--network host -e DOCKER_HOST=unix:///run/faultcode-docker.sock -v "/run/user/$uid/docker.sock:/run/faultcode-docker.sock"); fi
  memory=2g
  [[ $kind != ci ]] || memory=3g
  [[ $kind != gate ]] || memory=512m
  ci run -d --init --restart unless-stopped --name "faultcode-$kind-agent" --cpus 2 --memory "$memory" --pids-limit 1024 \
    -e "RUNNER_ROOT=$base/$kind" -v "$base/$kind:$base/$kind" "${extra[@]}" faultcode-ci-runner:2.338.0 >/dev/null
  printf 'Fault Code %s runner registered and started in rootless Docker.\n' "$kind"
done
