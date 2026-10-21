#!/usr/bin/env bash
# Part of FAU-23. Dedicated Certbot storage and webroot; never stop a service.
set -euo pipefail
export PATH=/usr/sbin:/usr/bin:/sbin:/bin
[[ $EUID == 0 && $(hostname -s) == lalai-staging-runner ]] || exit 65
root=/opt/faultcode-demo
certbot renew --non-interactive --cert-name faultcode-demo \
  --config-dir "$root/letsencrypt" --work-dir "$root/certbot-work" \
  --logs-dir "$root/certbot-logs" --webroot --webroot-path "$root/acme" \
  --deploy-hook /usr/local/sbin/faultcode-tls-install --no-random-sleep-on-renew
openssl x509 -in "$root/tls/fullchain.pem" -noout -checkend 1209600 >/dev/null || {
  echo 'ALERT: Fault Code TLS expires within14days; renewal needs attention' >&2; exit 1;
}
