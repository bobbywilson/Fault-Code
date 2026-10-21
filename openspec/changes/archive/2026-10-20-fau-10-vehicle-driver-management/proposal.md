# Proposal
## Why
Part of FAU-10. Vehicle and driver operations need trusted ownership, lifecycle and association history before incident intake. FAU-9 provides the permission boundary.
## What Changes
- Add provider-independent vehicle/driver records, normalized synthetic identifiers, owner mapping, CRUD/list and association APIs.
- Require exact Operator write permissions, same-tenant records, own/all read filtering before pagination, strong ETags and atomic idempotent create/audit contracts.
- Use test-only storage for executable contract verification. Production storage remains unavailable (503) pending DEC-001; no database, durable outbox, tenant or cloud provisioning.
## Capabilities
### New Capabilities
None.
### Modified Capabilities
- `vehicle-driver-management`: explicit lifecycle, association and persistence-boundary scenarios.
- `authorization`: collection filtering and operation-specific trusted mutation context.
- `api-contracts`: vehicle/driver DTOs, concurrency and replay contract.
## Impact
Domain, Application, Infrastructure unavailable adapter, API, tests, Docker inputs, OpenAPI and documentation. No new dependencies. ADR-001/003/009/013; FAU-9 dependency verified merged. DEC-001 remains unresolved.
