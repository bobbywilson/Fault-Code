# Design

## Context
See proposal.md. Existing Linux jobs cover full .NET tests, REST/MCP compatibility, real Qdrant, production Angular and container lifecycle. Canonical stores deliberately fail503. No active changes precede this ticket.

## Goals / Non-Goals
Provide locally previewable actual Mintlify sources, exact contract generation/checks and downloadable Linux candidates. No production deployment, service provisioning, paid model execution, identity changes or durable-provider implementation.

## Decisions
- Pin official npm `mint`4.2.1085 and lock dependencies in a separate documentation workspace. Use docs.json navigation and generated OpenAPI, not a custom docs renderer or Swagger UI. Validate with official CLI and inspect actual anonymous preview.
- Copy generated OpenAPI bytes unchanged into documentation. A generator checks byte drift against Release output and validates all local page links/navigation and security-critical settings. Preserve original permissions/schemas rather than rewriting bearer into an invented OAuth registration.
- Set playground proxy=false. No origin is supplied until approved: generated servers remain authoritative; docs explain simple/non-sending mode when no target exists. Do not route credentials through hosted proxies or add CORS/network changes.
- Existing Linux jobs remain authoritative. Add documentation verification and a release-artifact workflow with SHA-tagged API/admin Docker archives and checksums. Use only contents:read and Actions artifacts, no registry publication/credentials or host selection.
- Exercise rollback in a uniquely named disposable Docker network: build previous merge base and candidate, compare immutable labels and effective public configuration, replace/restart/restore and poll liveness plus protected401. This demonstrates executable image rollback only; no schema rollback or durable data claim.
- Reuse real A/B/C service integration tests and deterministic agent/middleware negative tests, export TRX plus a machine-readable case summary that rejects missing tests. No fabricated test or metric totals.

## Risks / Trade-offs
- Mint preview may fetch vendor runtime dependencies → pinned CLI/lock and actual preview evidence; no hosting claim.
- No live identity/canonical provider → retain explicit release blockers and documented isolated-tenant checklist, never enable production fixtures.
- Rollback without migrations cannot establish durable data compatibility → runbook prohibits irreversible migrations and requires approved backup/restore acceptance after DEC-001/003.
- Mintlify plan entitlements can change → record current primary sources and require account/plan verification before paid feature activation.

## Migration Plan
No application data migration. Build/test source artifacts, review exact-source Linux checks and archive specs. Operators may later load checksummed images after target/configuration approval. Keep known-good digest and configuration; restore it on failed probes. Production rollback still requires approved durable schema/backup plan.
