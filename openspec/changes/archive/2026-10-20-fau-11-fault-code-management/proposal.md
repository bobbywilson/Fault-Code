# Proposal
## Why
Part of FAU-11. Incident processing needs authoritative synthetic fault knowledge with immutable approval provenance and safe advisory remedies.
## What Changes
- Add tenant-scoped fault CRUD/search, revision history, engineering publication and approved applicable lookup.
- Add provider-independent knowledge transaction and ontology validation contracts; production adapters return503 pending DEC-001/FAU-13.
- Document and test exact permissions, validation, replay, concurrency and guidance exclusion.
## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `fault-knowledge`: concrete lifecycle and approved lookup behavior.
- `automotive-ontology`: fault taxonomy validation boundary.
- `api-contracts`: fault routes and storage-unavailable behavior.
## Impact
Domain/Application/Infrastructure/API, contract/integration tests and documentation. Depends on FAU-9; FAU-13 supplies approved ontology adapter. No new vendor dependency, persistence selection, provisioning or software deployment.
