## ADDED Requirements

### Requirement: Vehicle and driver mutation contracts
Versioned vehicle, driver and association APIs SHALL document schemas, examples, exact security, bounded pagination and safe errors including unavailable storage. PUT/DELETE and association changes SHALL require a strong If-Match; creates SHALL require Idempotency-Key scoped to tenant, actor, client, operation and normalized payload. The store contract SHALL atomically persist state, association history, replay result and actor/client/correlation audit.
#### Scenario: Concurrent and retried mutations
- **WHEN** a create repeats with the same key/payload or a stale update races
- **THEN** replay returns the original result without duplicate audit, changed payload conflicts and stale versions return 409 without partial changes
#### Scenario: Invalid concurrency input
- **WHEN** mutation headers are missing, weak or malformed
- **THEN** return correlated 400 before mutation
