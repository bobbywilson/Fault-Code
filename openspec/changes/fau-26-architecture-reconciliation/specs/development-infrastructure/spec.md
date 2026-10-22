## ADDED Requirements

### Requirement: Isolated approved relational deployment
A relational engine/edition/configuration SHALL require approval after persistence/data/migrations, licensing and actual shared-host resources are inspected. SQL Server is preferred, Qdrant is non-transactional, and no new server or paid subscription is implicit. If approved, Fault Code SHALL use its own database container/network/volume/credentials, resource limits, migrations/backup/outbox and restart tests; no LALAI resource may be repurposed.

#### Scenario: Proposed SQL Server decision
- **WHEN** SQL Server Express is recommended for the public evaluation
- **THEN** licensing/headroom/trade-offs and concrete configuration are presented first, with no selection/provisioning until approval


### Requirement: Approved SQL Server fleet persistence
Fault Code SHALL use SQL Server 2025 Express and EF Core SQL Server migrations for fleet persistence following the user's explicit approval. Drivers, vehicles, many-to-many associations, audit and replay metadata SHALL use normalized tenant-bound tables. No application startup may silently migrate, seed, fall back to memory or provision a database. Other stores SHALL remain explicitly incomplete until their adapters are implemented and verified.

#### Scenario: Concurrent writes and duplicate active pair
- **WHEN** multiple processes mutate a tenant fleet
- **THEN** database transactions and tenant application locks serialize writes, filtered uniqueness rejects duplicate active pairs, foreign keys preserve tenant integrity and failed mutations roll back

#### Scenario: Explicit migration and seed
- **WHEN** an authorized operator invokes the separate migrate or seed command with server-side connection secrets
- **THEN** versioned migrations or repeatable seeds run without starting the API; normal application credentials need no schema-management privilege

#### Scenario: Insufficient local capacity
- **WHEN** the existing local Linux VM cannot meet SQL Server memory requirements alongside its services
- **THEN** live database tests are blocked pending approved capacity, no shared service is restarted and skipped integration tests are not reported as passing
