# Tasks — Part of FAU-23

- [x] Inspect existing repository, OpenSpec, active identity work and CI delivery boundary.
- [x] Locate existing provider host and verify console access, OS, runtime services, ports and capacity read-only.
- [x] Update existing FAU-23 title/scope and retain project/milestone; do not duplicate.
- [x] Export/inspect GoDaddy DNS and public existing answers; preserve unrelated records (no DNS edits).
- [ ] Confirm LALAI/Simone operational baselines and network/firewall inventory.
- [x] Validate scoped specification before implementation (scoped and all strict:21 passed).
- [ ] Resolve DEC-001/demo scope and active identity PR dependency before functional acceptance claims.
- [x] Prepare isolated Compose, immutable version/deploy/preflight/rollback and scoped teardown implementation (live rollback remains a separate acceptance task).
- [ ] Activate development/main PR gates and CI; main-only serialized GHCR/SSH delivery. Development branch/environment exist and disabled deployment workflow is implemented; private branch protection is blocked by GitHub entitlement.
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
- Four local security tests, Bash syntax, local API/Angular Docker builds and all21 strict OpenSpec validations passed. All six GitHub checks passed on implementation b426397, including disposable production Compose/HTTPS/readiness/API401 packaging. Subsequent inventory-only commits require their own CI result; no live behavior is inferred.
- Authenticated LALAI dashboard baseline: queue/scheduler, development runner, model access and memory storage have fresh Healthy evidence. Windows session is Unknown with evidence dated2026-10-18; full Simone runtime verification remains incomplete.
- Dedicated HTTPS renewal service/timer and atomic certificate installation hook are prepared; verify actual issuance, webroot renewal and reload on the host before claiming operation. Production machine-certificate rotation remains unimplemented; the API/SPA do not themselves need a private machine certificate.

## Authorized development deployment follow-up
- [x] Reconcile user approval for current-development deployment and same-host isolated containers, static Mintlify /docs, Swagger and session-protected Mailpit /mailpit with capture-only delivery.
- [x] Prepare seven-service configuration, immutable docs image, required SQL CI gate, explicit migration/seed/index sequence and private server configuration without LALAI service operations.
- [ ] Push and self-review all scoped code, record actual PR approval constraints and merge only after successful checks.
- [ ] Verify automatic development deployment, authenticated app/API/token/tickets, known/unknown/resolution email capture, TLS/docs/Mailpit and retained persistence on the public domain.
