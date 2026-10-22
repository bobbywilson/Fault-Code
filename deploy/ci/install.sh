#!/usr/bin/env bash
# Root operator only. Does not restart/change the host Docker daemon or other runners.
set -Eeuo pipefail
[[ $EUID == 0 ]] || exit 64
export DEBIAN_FRONTEND=noninteractive NEEDRESTART_MODE=l
apt-get update
apt-get install -y --no-upgrade --no-install-recommends uidmap slirp4netns
id faultcode-ci >/dev/null 2>&1 || useradd --create-home --home-dir /var/lib/faultcode-ci --shell /usr/sbin/nologin faultcode-ci
base=/var/lib/faultcode-ci
uid=$(id -u faultcode-ci)
chmod 700 "$base"
install -d -m 700 -o faultcode-ci -g faultcode-ci "$base/bin" "$base/build" "$base/ci" "$base/gate" "$base/.config/systemd/user/docker.service.d"
chown -R faultcode-ci:faultcode-ci "$base/.config"
curl -fsSL --retry 3 https://download.docker.com/linux/static/stable/x86_64/docker-rootless-extras-29.1.3.tgz -o "$base/rootless.tgz"
tar xzf "$base/rootless.tgz" --strip-components=1 -C "$base/bin"
chown -R faultcode-ci:faultcode-ci "$base/bin"
# Allow only this official RootlessKit binary to create its namespace; no global AppArmor/sysctl changes.
cat > /etc/apparmor.d/faultcode-rootlesskit <<PROFILE
abi <abi/4.0>,
include <tunables/global>
"$base/bin/rootlesskit" flags=(unconfined) { userns, }
PROFILE
apparmor_parser -r /etc/apparmor.d/faultcode-rootlesskit
loginctl enable-linger faultcode-ci
systemctl start "user@$uid.service"
# Bound the entire CI user, including Docker build/test descendants.
systemctl set-property "user-$uid.slice" MemoryHigh=4G MemoryMax=4500M CPUQuota=200% TasksMax=4096
ci() { runuser -u faultcode-ci -- env HOME="$base" XDG_RUNTIME_DIR="/run/user/$uid" DBUS_SESSION_BUS_ADDRESS="unix:path=/run/user/$uid/bus" PATH="$base/bin:/usr/local/bin:/usr/bin:/bin" "$@"; }
ci "$base/bin/dockerd-rootless-setuptool.sh" install --force
cat > "$base/.config/systemd/user/docker.service.d/isolation.conf" <<'UNIT'
[Service]
ProtectHome=true
InaccessiblePaths=-/opt -/srv -/var/www -/var/lib/docker -/run/docker.sock -/var/run/docker.sock
UNIT
chown -R faultcode-ci:faultcode-ci "$base/.config"
ci systemctl --user daemon-reload
ci systemctl --user restart docker
ci docker --host "unix:///run/user/$uid/docker.sock" info --format '{{json .SecurityOptions}}'
cp "$(dirname "$0")/Dockerfile" "$(dirname "$0")/entrypoint.sh" "$base/build/"
chown -R faultcode-ci:faultcode-ci "$base/build"
ci docker --host "unix:///run/user/$uid/docker.sock" build -t faultcode-ci-runner:2.338.0 "$base/build"
printf 'Fault Code rootless runner image prepared; registration remains required. uid=%s\n' "$uid"
