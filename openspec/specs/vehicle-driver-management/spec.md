# vehicle-driver-management Specification

## Purpose
Define observable Fault Code vehicle driver management requirements and acceptance scenarios for authorized future development.

## Requirements

### Requirement: Validated vehicle and driver records
Authorized clients SHALL create, read, update, list and deactivate vehicles/drivers with stable IDs, validated identifiers and minimal contact data.

#### Scenario: Validated vehicle and driver records acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** an authorized operator submits a duplicate or invalid vehicle/driver
- **THEN** validation or conflict is returned; valid paginated CRUD remains queryable with ownership restrictions

### Requirement: Association history
Vehicle-to-driver associations SHALL retain effective history and incident referential integrity, with explicitly validated active-association rules.

#### Scenario: Association history acceptance
- **GIVEN** synthetic authorized Fault Code test context with the stated preconditions
- **WHEN** an association changes or a driver is deactivated
- **THEN** existing incident history remains intact and new intake uses the applicable valid association; concurrent edits cannot silently overwrite state

### Requirement: Provider independent lifecycle boundary
Vehicle and driver APIs SHALL validate normalized synthetic identifiers, canonical owner mappings, minimal synthetic contact preferences, stable IDs and soft deactivation. Production SHALL return 503 until an approved durable adapter exists; test doubles SHALL NOT be described as production persistence.
#### Scenario: Validated lifecycle
- **WHEN** authorized callers create, update, list or deactivate synthetic records
- **THEN** valid records retain stable IDs, duplicate normalized identifiers conflict, invalid fields fail validation and deactivation preserves records and ends active associations
#### Scenario: Missing persistence
- **WHEN** an authorized request uses the unconfigured production store
- **THEN** return correlated 503 without a successful mutation or fabricated data

### Requirement: Effective association integrity
Associations SHALL use server UTC effective intervals, require active same-tenant vehicle and driver, permit at most one current driver per vehicle, retain ended history and reject stale writes. Deactivated entities SHALL reject new associations.
#### Scenario: Association changes
- **WHEN** an association is ended or an entity deactivated
- **THEN** retain prior IDs and effective history and allow no new intake against a closed or inactive association
