# Linux release and rollback runbook

Part of FAU-23. No deployment target is provisioned. This procedure verifies private Linux candidates and disposable image rollback; durable authenticated deployment remains outstanding.

## Build and verify candidate

Use a clean regular checkout and Linux Docker, with Git, Python3, curl, tar and gzip. Preserve unrelated work. CI uses the PR head and base, without registry/provider credentials:

```sh
RELEASE_BASE_SHA=<approved-prior-40-character-commit> bash scripts/release-linux.sh
```

The script builds prior API and candidate API/admin from pinned Dockerfiles. Images carry immutable source labels and SHA tags. A unique disposable network/container publishes an ephemeral loopback API port. It verifies prior image/configuration/health/401/non-root, candidate replacement, candidate restart, prior restoration and identical prior public fixtures. Blank required application configuration must exit nonzero. Cleanup removes only its own containers/network and temporary source, without developer volumes. Existing container/Qdrant jobs separately test their boundaries.

`artifacts/release/` contains source-tagged Docker archives, manifest with source/platform/image IDs, rollback stages, synthetic responses and SHA256SUMS. All exact-source jobs must pass. Private Actions artifacts expire after7days; preserve intentionally. No registry publication or hosting promotion occurs. Pinned inputs/reproducible commands do not promise bit-identical Docker layers.

## Download and load after approval

Download the exact-source Actions artifact into a new directory. Before loading:

```sh
sha256sum --check SHA256SUMS
docker load --input api-<source-sha>.tar.gz
docker load --input administration-<source-sha>.tar.gz
docker image inspect faultcode-api:<source-sha>
docker image inspect faultcode-admin:<source-sha>
```

Match Linux platform, source label and manifest image IDs. Checksums verify downloaded bytes; image IDs identify loaded content. Preserve the known-good immutable image, compatible configuration/secret **version references**, source and evidence separately from ephemeral retention. Never archive credentials or use mutable latest for promotion.

## Configuration, migrations and health

Use docker-development.md, entra-authentication.md, authorization.md, administration.md and observability.md. Required `FaultCode__ApplicationName` is nonblank and<=100characters. Entra defaults disabled; browser config defaults empty; model/embedding and OTLP default disabled. There is no production fake-auth or in-memory-store switch. Secrets stay in approved runtime secret storage, never logs, archives, docs or model context.

`/health/live` checks the process; `/health/ready` optionally checks Qdrant. Neither proves canonical storage, outbox, live identity, model or notification readiness. Production stores return503. DEC-001 has selected no relational engine/schema/migration tool; no valid migration command exists. DEC-003 has selected no host, canonical object storage, retention or backup service. Do not invent SQL or equate an empty migration with acceptance.

Before durable promotion, approve adapters and require migration checksums/order, compatibility policy, atomic transactions/outbox/replay, crash/restart recovery and tested backups/restores. Image rollback cannot undo data changes. Irreversible schema changes require an approved forward-recovery or backup/restore plan.

## Promotion and rollback after target approval

1. Record the approved target, exact source/image IDs, compatible configuration references and all passing evidence. Verify schema compatibility and actual restore acceptance first.
2. Load verified images through the target's reviewed deployment procedure, retaining prior images/configuration and backups. This document does not choose or provision a host.
3. Probe process/dependencies, anonymous deployed docs, actual Entra issuer/audience/expiry and401/403, authorized REST/MCP, PKCE and responsive administration. Check tenant/owner policies, correlation and redaction.
4. Execute synthetic A/B/C against the approved isolated tenant and durable services. Retain incident/result/evidence/approval/notification IDs and observed outcomes. A: approved guidance and notification. B: approved applicable dealership referral, zero vehicle actions. C: confirmed absence→engineering→approved publication→driver notification. Outage must fail retryably, never imply unknown. Check duplicate/replay/concurrency/restart and delivery reconciliation.
5. Stop promotion on failure. Restore the known-good immutable image **and compatible configuration** through that target's procedure. Repeat health/auth/workflow checks and retain results. Never blindly roll back an irreversible migration.

## Outstanding live acceptance

| Boundary | Current evidence | Still required |
| --- | --- | --- |
| DEC-001 canonical storage/outbox | Test-only transactional adapters | Approved engine; migration, durable replay, crash/restart/restore |
| DEC-002 notifications | Local synthetic sink acknowledgements | Approved channel/contact/consent; actual delivery/reconciliation |
| DEC-003 host/object storage | No provisioned target | Approved host/TLS/retention/backups and durable rollback |
| Entra | Synthetic signed JWT/discovery tests | Actual isolated PKCE/machine tokens; negative issuer/audience/expiry/permissions |
| DEC-004 models/embeddings | Mocked provider, strict actual SDK/schema tests | Approved snapshots/dimensions; separately authorized live calls |
| DEC-005 plans | No purchase/activation | Verify current Mintlify/Datadog/Entra entitlements and budget |
| Public docs | Anonymous actual local/CI Mintlify renderer | Approved docs hosting and actual anonymous deployed URL |
| Mint CLI dependencies | Upstream development audit retained | Vendor remediation before hosted/public preview-service use |

Free-plan availability does not verify an account entitlement. Datadog receiver tests do not establish live account ingestion/retention. No vehicle software deployment capability exists.
