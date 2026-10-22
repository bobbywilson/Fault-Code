#!/usr/bin/env bash
# Fixed read-only status through the already restricted Fault Code deployment key.
set -euo pipefail
[[ $EUID == 0 && $# == 0 ]] || exit 64
systemctl is-active faultcode-ci-docker.service || true
for file in build.log registration.log; do
  echo "Fault Code CI $file"
  [[ ! -f /opt/faultcode-demo/runner-setup/$file ]] || tail -n 18 "/opt/faultcode-demo/runner-setup/$file"
done
runuser -u faultcode-ci -- env HOME=/var/lib/faultcode-ci DOCKER_HOST=unix:///run/user/$(id -u faultcode-ci)/docker.sock \
  docker ps --format '{{.Names}} {{.Status}}' || true
