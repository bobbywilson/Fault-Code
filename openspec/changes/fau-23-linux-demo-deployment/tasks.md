# Tasks — Part of FAU-23

- [x] Inspect existing repository, OpenSpec, active identity work and CI delivery boundary.
- [x] Locate existing provider host and verify console access, OS, runtime services, ports and capacity read-only.
- [x] Update existing FAU-23 title/scope and retain project/milestone; do not duplicate.
- [x] Export/inspect GoDaddy DNS and public existing answers; preserve unrelated records (no DNS edits).
- [ ] Confirm LALAI/Simone operational baselines and network/firewall inventory.
- [x] Validate scoped specification before implementation (scoped and all strict:21 passed).
- [ ] Resolve DEC-001/demo scope and active identity PR dependency before functional acceptance claims.
- [ ] Prepare isolated Compose, immutable version/deploy/preflight/rollback and scoped teardown implementation.
- [ ] Establish development/main PR gates and CI; main-only serialized GHCR/SSH delivery. Development branch created and existing CI push triggers extended; deployment workflow is not implemented.
- [ ] Review concrete bootstrap/access/shared-resource changes and obtain required action-time approval.
- [ ] Configure approved deployment access, Compose, secrets and production environment credentials.
- [ ] Configure Fault Code DNS/TLS/routing and exact existing Entra callbacks.
- [ ] Verify local Docker/build/tests, CI, deployed version, DNS/TLS/API/real login and allowed/denied access.
- [ ] Verify certificate rotation, failure retries and expiry alerts with distinct local/production keys.
- [ ] Verify Fault Code rollback and no LALAI/Simone disruption; dry-run teardown without data deletion.
- [ ] Commit/push approved files, record deployment and blockers in existing issue; archive only after acceptance.

## Prepared implementation evidence
- Main-only workflow and all-six-check source gate are written but disabled until DEMO_DEPLOY_ENABLED=true and reviewed bootstrap is complete. No deployment credentials stored.
- Isolated Compose/proxy, bounded forced SSH receiver, root-only manifest/deployment/rollback candidate and bootstrap runbook are written. No live install or rollback claimed.
- Four local security tests and Bash syntax checks passed. Container configuration/build/CI and live behavior still require verification.
- Production machine-certificate rotation and ACME automation remain unimplemented; the API/SPA do not themselves need a private machine certificate.
