#!/usr/bin/env bash
# Part of FAU-23. Read-only checks; never repair/restart another application.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
[[ $EUID == 0 && $(hostname -s) == lalai-staging-runner ]] || exit 65
available=$(awk '/MemAvailable:/ {print $2}' /proc/meminfo)
free_disk=$(df -Pk /opt/faultcode-demo | awk 'NR==2 {print $4}')
printf 'Host available_memory_KiB=%s free_disk_KiB=%s\n' "$available" "$free_disk"
[[ $available -ge 1048576 && $free_disk -ge 10485760 ]] || {
  echo 'ALERT: host headroom below Fault Code monitoring threshold' >&2; exit 1;
}
mapfile -t containers < <(docker ps -aq --filter label=com.docker.compose.project=faultcode-demo)
[[ ${#containers[@]} == 7 ]] || { echo 'ALERT: expected exactly seven Fault Code containers' >&2; exit 1; }
declare -A observed=()
for container in "${containers[@]}"; do
  read -r service state health < <(docker inspect --format '{{index .Config.Labels "com.docker.compose.service"}} {{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}missing{{end}}' "$container")
  [[ "$service" =~ ^(api|administration|qdrant|proxy|sql|mailpit|documentation)$ && -z ${observed[$service]:-} ]] || exit 1
  observed[$service]=1
  printf 'Fault Code service=%s state=%s health=%s\n' "$service" "$state" "$health"
  [[ "$state" == running && "$health" == healthy ]] || { echo 'ALERT: Fault Code service is unhealthy' >&2; exit 1; }
done
docker stats --no-stream --format '{{.Name}} cpu={{.CPUPerc}} memory={{.MemUsage}} pids={{.PIDs}}' "${containers[@]}"
