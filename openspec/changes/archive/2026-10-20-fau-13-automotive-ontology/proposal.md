## Why
Part of FAU-13. Fault knowledge needs authoritative approved classification versions and investigations need deterministic steering rules with retained explanations.
## What Changes
- Add tenant-local typed ontology draft, publication, history and version-pinned evaluation APIs.
- Validate typed relationships, identity stability, cycles, applicability and conflicting rule precedence.
- Connect IFaultTaxonomy to immutable approved ontology publications; keep production unavailable pending DEC-001.
## Capabilities
### New Capabilities
None.
### Modified Capabilities
- automotive-ontology: specify management, deterministic evaluation, provenance and storage boundaries.
- api-contracts: additive ontology endpoints and exact knowledge policies.
## Impact
Domain/Application/Infrastructure/API, tests, OpenAPI and explicit Docker build inputs. Depends on FAU-5/9 and integrates FAU-11. No new packages, providers, GraphRAG or vehicle deployment.
