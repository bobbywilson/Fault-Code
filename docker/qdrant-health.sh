#!/usr/bin/env bash
# Debian upstream provides bash/coreutils. Do not print headers, keys or bodies.
set -euo pipefail
exec 3<>/dev/tcp/127.0.0.1/6333
printf 'GET /collections HTTP/1.1\r\nHost: qdrant\r\napi-key: %s\r\nConnection: close\r\n\r\n' "$QDRANT__SERVICE__API_KEY" >&3
IFS= read -r status <&3
[[ "$status" == 'HTTP/1.1 200 '* ]]
