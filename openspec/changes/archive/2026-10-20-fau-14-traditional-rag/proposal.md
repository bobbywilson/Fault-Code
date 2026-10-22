# Proposal

## Why
Part of FAU-14. Investigations need authorized, approved document evidence with reproducible citations. Existing Qdrant readiness supplies no ingestion or retrieval behavior.

## What Changes
- Add bounded demo document lifecycle, server approval, deterministic page chunks, checksums and original-storage abstraction.
- Add configurable OpenAI embeddings and compatible Qdrant derived collections, replacement/deletion/reindex and approved-only retrieval with authoritative revalidation.
- Add exact knowledge.publish management and knowledge.read retrieval APIs, DTOs, examples and outage/security evaluations.
- Keep DEC-001/003 canonical durable storage unavailable and DEC-004 model selection explicit; no paid calls, provisioning or production memory fallback.

## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `knowledge-retrieval`: document lifecycle, rebuildable index and filtered citation contracts.
- `api-contracts`: versioned document management and retrieval operations.

## Impact
Domain, Application, Infrastructure, API, tests, Docker acceptance and documentation. Depends on FAU-6/7/9; preserves FAU-11/13 approval boundaries and prepares FAU-16/17. No new provider selection, GraphRAG, generation, notification or vehicle operations.
