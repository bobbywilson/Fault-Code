# Part of FAU-23: Linux demo deployment

## Why
Linux artifacts and disposable rollback exist, but no automatic deployment, public Fault Code origin or live shared-host acceptance exists. User authorized reuse of the existing LALAI Linux server and existing GoDaddy domain, with strict LALAI/Simone protection.

## What Changes
- Record verified server and DNS inventory, isolated Compose architecture and reversible deployment plan.
- Add feature -> development -> main delivery policy; main-only successful-CI deployment through GHCR and scoped SSH.
- Configure exact domain HTTPS routing and existing Entra production callbacks after access/shared-impact approval.
- Add version/health/rollback/teardown evidence, separate environment certificates and automated production machine-key rotation.
- Keep unimplemented persistence/notifications/model adapters explicit; no production test stores or fabricated workflow completion.

## Capabilities
### New Capabilities
None; extend existing infrastructure, lifecycle, documentation-delivery and identity specifications.
### Modified Capabilities
- development-infrastructure: shared-server isolation and resource protection.
- development-lifecycle: development/main PR gates and serialized deployment.
- documentation-delivery: observable deployment, rollback and teardown.
- identity: distinct environment credentials and production callback/rotation.

## Impact
Existing bobbywilson/Fault-Code repository only; new Fault Code resources on existing server, only faultcodeai.com DNS. No new server or paid subscription. Existing FAU-23 retained in Documentation and Delivery milestone. Authentication PR21 must be integrated before production activation. DEC-001 remains unresolved; user clarification requested before claiming functional business demo.
