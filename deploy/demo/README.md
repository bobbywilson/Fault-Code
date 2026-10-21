# Fault Code demo deployment candidate

Part of FAU-23. Prepared code, not an installed or activated deployment. Never run
the bootstrap steps on LALAI's local Homestead VM. Target is the verified existing
`lalai-staging-runner`, `134.209.215.92`. Private main branch protection is not
available under the provider's observed entitlement; no purchase/visibility change.

## Before activation

1. Review this directory and OpenSpec `fau-23-linux-demo-deployment`. Merge approved
   identity PR21 and deployment changes through development and then main only
   after CI. The publisher explicitly rejects missing approved machine-role code.
2. Establish actual LALAI/Simone operational baselines. The observed active
   `lalai-runner.service` and HTTP302 are narrower evidence than functional health.
3. Obtain action-time approval for a new restricted deployment account/key,
   Fault Code-only firewall/routing/public exposure and Entra redirect changes.
   No shared service may be restarted. Check DigitalOcean firewall rules as well
   as UFW and recheck capacity/ports before changing the host.
4. Simulate package installation first; install only the official Compose plugin
   without upgrading/restarting Docker or unrelated packages. Abort on conflicts.
5. Install reviewed files into `/opt/faultcode-demo` owned by root, directory0700.
   Keep `compose.yaml`, `nginx.conf` and `browser-config.json` root-owned and not
   group/world writable. Generate `runtime.env` on the server, root-owned0600,
   containing a fresh `FAULTCODE_QDRANT_API_KEY`. Never commit or print it.
6. Install `deploy.sh` as root-owned0755 `/usr/local/sbin/faultcode-deploy` and
   `ssh-receiver.sh` as root-owned0755 `/usr/local/sbin/faultcode-ssh-receiver`.
   Create `faultcode-deploy` with locked password and no Docker group membership.
   Its sole authorized SSH public key must use
   `restrict,command="/usr/local/sbin/faultcode-ssh-receiver"`.
   Sudoers grants only `/usr/local/sbin/faultcode-deploy` as root, with env_reset,
   no SETENV and no other commands. Validate sudoers with visudo before installation.
   The receiver accepts only `deploy SOURCE_SHA API_DIGEST WEB_DIGEST QDRANT_DIGEST`;
   no shell fragments, paths or arbitrary images are accepted. Test actual allowed
   and rejected SSH commands before registering the private key with Actions.
7. Preserve exported DNS zone. Change only the parked apex A to134.209.215.92;
   retain www CNAME and all NS/TXT/mail records. Obtain valid free ACME certificates
   for apex/www into this project's restricted `tls` directory, with approval for
   the CA agreement. Establish webroot renewal using `acme`; deploy certificate
   replacement atomically, validate only Fault Code proxy configuration and reload
   only that container. No unrelated certificate or shared proxy is touched.
   ACME issuance/renewal is not implemented or verified by these files yet.
8. Register exact HTTPS callback `https://faultcodeai.com/auth/callback` and logout
   `https://faultcodeai.com/` on the existing Fault Code SPA, retaining local entries.
   Same-origin `/api/v1` needs no broad cross-origin CORS. Verify real sign-in,
   expiry/renewal, scope/role acceptance and negative401/403 after deployment.
9. Configure environment `demo-production` secrets `DEMO_SSH_KEY` and
   `DEMO_SSH_KNOWN_HOSTS` using verified host keys, never trust-on-first-use or
   disabled host verification. Short-lived read-only job registry tokens arrive
   via SSH stdin, are kept only in a private temporary Docker config and removed.
10. Set repository variable `DEMO_DEPLOY_ENABLED=true` only after bootstrap,
    local/CI verification, target review and approvals. Until then no workflow
    build/publication/deployment job is eligible. A subsequent main push and all
    six successful same-source checks are required; feature/development never deploy.

## Operational boundaries

The fixed Compose project has bounded API/web/Qdrant/proxy resources, independent
networks/volumes and no LALAI mounts. It reserves two of four CPU cores at configured
limits and at most2304MiB container RAM, leaving observed runner/OS headroom.
Docker log size is bounded. API container health checks actual Qdrant readiness;
HTTP/API/authentication probes and image source labels are independently checked.
The receiver checks unchanged runner PID/restart counter but cannot prove every
Simone behavior. Capture external application/agent baselines separately.

Do not mistake healthy containers for durable workflows: canonical stores still
return503 and no relational provider is selected. No test stores/auth bypass are
enabled. Public API origin is `https://faultcodeai.com/api/v1/` and protected
operations remain protected. Keep demo limitations explicit until acceptance passes.

Source manifests contain only image digests. The last accepted current.env is
retained while a candidate starts. Failure restores its containers and verifies
health without touching LALAI; a failed first candidate has no prior release and
is torn down without volumes. Root-only manifests record actual accepted source.
Candidate/rollback deadlines are bounded. Live rollback remains unverified.

Machine-client credentials differ from TLS certificates. API JWT validation and
Angular PKCE do not require a private machine-client certificate. If a production
machine consumer is configured, generate its own Linux key separately from local
development, and implement/verify the approved self-rotation service with overlap,
failure alerts and cleanup of only its own key IDs. That service is not implemented
here; never copy the local private key to production or claim rotation is active.

See `docs/deployment/teardown-plan.md` for scoped recovery/removal. Destructive
persistent-data deletion requires separate approval; never prune Docker globally.
