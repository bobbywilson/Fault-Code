# Fault Code demo rollback and teardown plan

Part of FAU-23. This is a scoped procedure, not an executed deployment/rollback. No live Fault Code deployment exists yet.

## Before removal or rollback
- Record accepted source SHA, exact image digests and environment/configuration references. Export only Fault Code data to an approved location. Capture lalai-runner.service state/restart counter and application/Simone operational checks.
- Inspect Compose labels com.docker.compose.project=faultcode-demo and exact services/volumes/networks; never infer ownership from a substring. Abort if unexpected ownership/shared mounts appear.
- Verify working directory is the approved /opt/faultcode-demo, not a LALAI directory. Use its explicit --project-name and --file on every Compose command.

## Rollback
The eventual deployment receiver must retain an accepted prior release manifest and configuration reference. Restore its API/web/Qdrant digests using only the Fault Code Compose project, wait for container health and verify exact source version, HTTPS/API/Entra401/403 and authorized requests. If no previous release exists, report that limitation and remove only failed candidate containers while preserving data. Do not restart Docker, the host, lalai-runner, LALAI, Simone or a shared reverse proxy. Data/schema rollback is not implied by image rollback; no database migration strategy is approved yet.

## Reversible teardown
1. Obtain the requested teardown instruction and confirm scope and backup location. This document is not deletion authorization.
2. Capture DNS zone and existing service baselines. Stop only the fixed Fault Code deployment/rotation timers after those exist.
3. From /opt/faultcode-demo, after confirming accepted current.env and runtime.env exist, run `docker compose --project-name faultcode-demo --env-file runtime.env --env-file current.env --file compose.yaml down` WITHOUT --volumes. The explicit environment files are required for image/secret interpolation even for down. Inspect every project-owned resource first. This retains persistent volumes. If no accepted manifest exists, stop and inspect the failed candidate's exact resources instead of guessing configuration. Do not use global prune or broad container/volume deletion.
4. Disable/remove only Fault Code-owned port80/443 routing and firewall rules created by this deployment, based on recorded exact rule identifiers. Never remove SSH or change defaults/shared policies. Leave shared proxy infrastructure and unrelated TLS certificates intact.
5. Revert only the apex A record changed for faultcodeai.com to its approved previous parked configuration or reviewed original public answers; keep www CNAME, nameservers, DMARC and unrelated records/domains untouched. Do not bulk-import the exported zone. Verify authoritative and public resolution.
6. Disable only the GitHub demo-production deployment path and revoke only Fault Code deployment credentials/production certificate registrations. Preserve the dedicated tenant and separate local-development credentials unless explicitly requested otherwise.
7. Move the Fault Code deployment directory/configuration to an approved restricted quarantine/backup location for reversibility. Remove project networks only after checking no unrelated attachment. Retain image/release records and volumes pending data-deletion approval.
8. Verify LALAI web routing, authenticated application checks, lalai-runner.service/restart counter and Simone behavior. Report each actual result.

## Destructive data removal
Separate explicit approval is required before deleting Fault Code persistent volumes, backups, private keys or retained application data. List exact volume IDs, labels, backup evidence and recovery implications first. Never delete LALAI volumes/databases, shared networks, Docker infrastructure or unrelated certificates.
