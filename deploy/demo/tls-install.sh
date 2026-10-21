#!/usr/bin/env bash
# Part of FAU-23. Install root-owned as /usr/local/sbin/faultcode-tls-install.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
umask 077
[[ $EUID == 0 && $(hostname -s) == lalai-staging-runner ]] || exit 65
root=/opt/faultcode-demo
lineage=$root/letsencrypt/live/faultcode-demo
[[ ${RENEWED_LINEAGE:-$lineage} == "$lineage" ]] || exit 65
openssl x509 -in "$lineage/fullchain.pem" -noout -checkend 604800 >/dev/null
for host in faultcodeai.com www.faultcodeai.com; do
  openssl x509 -in "$lineage/fullchain.pem" -noout -checkhost "$host" >/dev/null
done
cert_key=$(openssl x509 -in "$lineage/fullchain.pem" -pubkey -noout | openssl pkey -pubin -outform DER | sha256sum)
private_key=$(openssl pkey -in "$lineage/privkey.pem" -pubout -outform DER | sha256sum)
[[ "$cert_key" == "$private_key" ]] || { echo 'Certificate/key mismatch' >&2; exit 65; }
exec 9>/run/lock/faultcode-demo-deploy.lock
flock -w 900 9
cd "$root"
generation=$(openssl x509 -in "$lineage/fullchain.pem" -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :)
[[ "$generation" =~ ^[0-9A-F]{64}$ ]] || exit 65
install -d -m 700 "tls/generations/$generation"
install -m 644 "$lineage/fullchain.pem" "tls/generations/$generation/fullchain.pem"
install -m 600 "$lineage/privkey.pem" "tls/generations/$generation/privkey.pem"
previous=$(readlink tls/current || true)
ln -s "generations/$generation" tls/current.next
mv -Tf tls/current.next tls/current
[[ -L tls/fullchain.pem ]] || ln -s current/fullchain.pem tls/fullchain.pem
[[ -L tls/privkey.pem ]] || ln -s current/privkey.pem tls/privkey.pem
compose() { docker compose --project-name faultcode-demo --env-file runtime.env --env-file current.env --file compose.yaml "$@"; }
restore() {
  if [[ -n "$previous" ]]; then
    ln -s "$previous" tls/current.restore
    mv -Tf tls/current.restore tls/current
    compose exec -T proxy nginx -s reload || true
  fi
  echo 'Fault Code TLS replacement failed; inspect renewal service journal' >&2
  exit 1
}
if [[ -f current.env ]]; then
  compose exec -T proxy nginx -t && compose exec -T proxy nginx -s reload || restore
  observed=''
  for attempt in 1 2 3 4 5; do
    observed=$(timeout 10 openssl s_client -connect 127.0.0.1:443 -servername faultcodeai.com -verify_hostname faultcodeai.com -verify_return_error </dev/null 2>/dev/null | openssl x509 -noout -fingerprint -sha256 | cut -d= -f2 | tr -d :) || true
    [[ "$observed" != "$generation" ]] || break
    sleep 1
  done
  [[ "$observed" == "$generation" ]] || restore
fi
echo "Fault Code TLS installed fingerprint=$generation; no shared proxy reloaded"
