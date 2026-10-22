---
title: "Release and rollback"
description: "Inspect exact-source Linux artifacts and pending production acceptance."
---

## Evidence required
All exact-source Linux checks must pass: backend build/tests/specs/REST/MCP, documentation validation/drift/anonymous browser checks, administration unit/browser/production-image checks, isolated container lifecycle, authenticated real Qdrant and release image rollback. Cancelled or missing checks are not passing evidence.

The release workflow builds SHA-tagged API and administration image archives with immutable source labels, image IDs, manifest and SHA-256 checksums. Artifacts stay in the private repository's Actions storage; nothing is published to a registry or hosting provider. The manifest records source and platform. Reproducible commands/inputs do not promise bit-identical Docker layer output.

## Disposable rollback acceptance
CI builds the prior base source and candidate API images, runs each with a distinct non-secret application name, asserts image identity, health and protected 401, restarts the candidate, then restores the prior image/configuration. The unique disposable network and containers are removed afterward. No developer volume or deployment target is touched. This proves image replacement/restoration; it does not prove database/schema rollback.

## Operator procedure
After target, configuration and persistence approval: verify artifact checksums, load archives, inspect source labels/image IDs and preserve the known-good image plus its compatible configuration. Promote by immutable identity; probe process, dependencies and authorized API/browser workflows. Stop promotion on any failure. Restore the known-good image and configuration, then repeat all health and business checks. Never automatically roll back an irreversible schema change; use the approved migration/backup plan.

## Outstanding live acceptance
| Boundary | Required before claiming production delivery |
| --- | --- |
| Entra | Approved isolated tenant/registrations; actual PKCE and machine tokens; issuer/audience/expiry/401/403 tests |
| Canonical persistence | Approved DEC-001 engine/adapters/outbox; atomicity, replay, crash/restart and migrations |
| Notifications | Approved DEC-002 channel, contact and consent; actual delivery/reconciliation |
| Hosting/storage | Approved DEC-003 target, TLS, retention, backups, restore and durable rollback |
| Models/embeddings | Approved DEC-004 versions/dimensions and any separately authorized live calls |
| Plans/cost | Verify DEC-005 Mintlify/Datadog/Entra current entitlements before activation or spending |

No account, paid plan, service, credential, hosting target or live deployment is created by the repository workflow. Public reading has no login gate; hosted public availability still requires an approved documentation deployment.

