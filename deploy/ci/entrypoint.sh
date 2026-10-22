#!/usr/bin/env bash
set -euo pipefail
[[ ${RUNNER_ROOT:-} =~ ^/var/lib/faultcode-ci/(ci|gate)$ ]] || exit 64
mkdir -p "$RUNNER_ROOT/runner" "$RUNNER_ROOT/toolcache"
cp -an /runner-template/. "$RUNNER_ROOT/runner/"
cd "$RUNNER_ROOT/runner"
export RUNNER_TOOL_CACHE="$RUNNER_ROOT/toolcache" AGENT_TOOLSDIRECTORY="$RUNNER_ROOT/toolcache"
export DOTNET_INSTALL_DIR="$RUNNER_ROOT/dotnet"
if [[ ${1:-} == configure ]]; then
  ./config.sh --unattended --url https://github.com/bobbywilson/Fault-Code \
    --token "$(cat /run/registration-token)" --name "faultcode-${RUNNER_ROOT##*/}" \
    --labels "faultcode-${RUNNER_ROOT##*/}" --work "$RUNNER_ROOT/work" --disableupdate
else
  exec ./run.sh
fi
