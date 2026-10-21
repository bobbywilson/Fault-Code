#!/usr/bin/env bash
# Part of FAU-23. Root-owned forced command; no interactive shell or arbitrary command.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
command_text=${SSH_ORIGINAL_COMMAND:-}
pattern='^deploy [0-9a-f]{40} sha256:[0-9a-f]{64} sha256:[0-9a-f]{64} sha256:[0-9a-f]{64}$'
[[ "$command_text" =~ $pattern ]] || { echo 'Rejected deployment command' >&2; exit 64; }
read -r verb source api web vector <<< "$command_text"
exec sudo -n /usr/local/sbin/faultcode-deploy "$source" "$api" "$web" "$vector"
