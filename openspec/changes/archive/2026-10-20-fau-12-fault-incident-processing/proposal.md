# FAU-12: Fault incident processing
## Why
Fault events need stable identity, retained context and observable retryable processing before agent investigation is introduced. Part of FAU-12; FAU-10/11 are merged prerequisites.
## What Changes
- Add provider-independent intake, list/detail/history and lifecycle/failure/retry API operations with exact permissions and immutable provenance.
- Require atomic incident/source identity/history/audit/pending-work storage and consistent authoritative fleet/approved knowledge reads; fail503 without an approved adapter.
- Test replay/concurrency, association/source validation, transitions, failure/rollback and ownership using test-only transactional adapters.
## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `incident-lifecycle`: validated idempotent intake, explicit transitions, visible failure/retry and storage boundary.
- `vehicle-driver-management`: immutable incident association snapshots and historical ownership.
- `api-contracts`: protected incident operations and conditional/replay contracts.
## Impact
Domain/Application/API/Infrastructure and tests, OpenAPI/image inventory and runbooks. No package/provider selection, database migrations, external dispatch, cloud, Entra provisioning, RAG or vehicle deployment. DEC-001 remains unresolved; restart/durable outbox acceptance requires a future approved provider.
