## ADDED Requirements

### Requirement: Provider independent lifecycle boundary
Vehicle and driver APIs SHALL validate normalized demo identifiers, canonical owner mappings, minimal demo contact preferences, stable IDs and soft deactivation. Production SHALL return 503 until an approved durable adapter exists; test doubles SHALL NOT be described as production persistence.
#### Scenario: Validated lifecycle
- **WHEN** authorized callers create, update, list or deactivate demo records
- **THEN** valid records retain stable IDs, duplicate normalized identifiers conflict, invalid fields fail validation and deactivation preserves records and ends active associations
#### Scenario: Missing persistence
- **WHEN** an authorized request uses the unconfigured production store
- **THEN** return correlated 503 without a successful mutation or fabricated data
### Requirement: Effective association integrity
Associations SHALL use server UTC effective intervals, require active same-tenant vehicle and driver, permit at most one current driver per vehicle, retain ended history and reject stale writes. Deactivated entities SHALL reject new associations.
#### Scenario: Association changes
- **WHEN** an association is ended or an entity deactivated
- **THEN** retain prior IDs and effective history and allow no new intake against a closed or inactive association
