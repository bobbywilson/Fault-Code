# FAU-23 demo deployment inventory

Observed2026-10-21. Investigation is not a deployed environment.

| Item | Verified observation |
|---|---|
| Existing host | DigitalOcean lalai-staging-runner, droplet609987985 |
| Public IPv4 | 134.209.215.92 |
| OS | Ubuntu24.04.5LTS |
| Resources | 4CPU;7941MiB RAM,7287MiB available;150GiB disk free at observation |
| Access | Provider root Web Console works; local root SSH rejected publickey |
| Docker | Installed, no running containers; bridge/host/none networks only |
| Compose | docker compose absent |
| Existing runner | lalai-runner.service,User=lalai-runner,active/running,NRestarts=0,ExecMainStatus=0 |
| Listening ports | SSH22,local DNS53,loopback containerd44693; no80/443 listener |
| Firewall | UFW active,default deny incoming; only SSH explicitly allowed plus existing Fail2Ban reject |
| Proxy | No existing running HTTP reverse proxy identified on this host |
| Other environment | Local Homestead development VM192.168.1.56 is separate; do not deploy there accidentally |
| Planned directory/project | /opt/faultcode-demo / faultcode-demo; not created |
| Planned URLs | https://faultcodeai.com/ ; same-origin /api/v1/ ; not deployed |

## GoDaddy baseline
Existing account recognizes the purchased domain, active. Seven records observed:

| Type | Name | Data | TTL |
|---|---|---|---|
| A | @ | Parked (public answers15.197.148.33 and3.33.130.190) |600seconds|
| NS | @ | ns31.domaincontrol.com. |3600|
| NS | @ | ns32.domaincontrol.com. |3600|
| CNAME | www | faultcodeai.com. |3600|
| CNAME | _domainconnect | _domainconnect.gd.domaincontrol.com. |3600|
| SOA | @ | Primary nameserver ns31.domaincontrol.com. |3600|
| TXT | _dmarc | v=DMARC1;p=quarantine;adkim=r;aspf=r;rua=mailto:dmarc_rua@onsecureserver.net; (spacing as original zone) |3600|

No MX record was shown and public MX query returned no answer. Never infer permission to add/remove mail records. Proposed only apex A change:Parked ->134.209.215.92; existing www CNAME retained. No nameserver or DNS edit has occurred. Original zone was exported through GoDaddy and retained as docs/deployment/faultcodeai-before.zone. It is an informational backup, not authorization to import or replace a whole live zone.

## Explicit incomplete acceptance
Full Simone operational behavior and LALAI authenticated application health are not yet independently verified; a running runner process is narrower evidence. Local SSH deployment credential, Compose installation,80/443 access, approved proxy/TLS, GHCR/deployer, production Entra callbacks and certificate rotation remain unconfigured. No Fault Code containers or server resources were created.

DEC-001 canonical storage remains unimplemented and business production endpoints503. User clarification requested on whether to deploy existing behavior with limitations or complete persistence before functional-demo acceptance. Do not use test stores to hide this. Identity PR21 is a separate pending integration dependency.

## Separate web application baseline
Public DNS for lalai.io and staging.lalai.io returned103.133.1.1, distinct from the runner. HTTPS GET /login on staging returned302 (routing response only, not full authenticated/Simone health). Do not describe the runner as hosting the public LALAI web application. Its active systemd development-runner process is the verified shared dependency. No LALAI record or service was changed.

## GitHub delivery configuration
Existing private repository retained. The development branch was created from existing main; feature and development CI push triggers are prepared on the feature branch. GitHub environment demo-production was created without secrets or deployment. GitHub branch-protection GET returned403 with Upgrade to GitHub Pro or make this repository public to enable this feature. No upgrade or visibility change was performed. PR/CI workflow discipline can be followed, and deployment can independently enforce successful same-source CI; platform-enforced protected merges cannot be claimed under this observed entitlement. Authentication PR21 is retargeted to development, still draft and unmerged.
