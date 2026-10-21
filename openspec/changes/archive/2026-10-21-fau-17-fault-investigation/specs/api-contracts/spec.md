## ADDED Requirements

### Requirement: Versioned investigation operations
POST and GET /api/v1/incidents/{id}/investigations SHALL create conditional replayable investigations and list bounded historical results. POST SHALL require faults.write, faults.read, vehicles.access.read and knowledge.read; GET SHALL require all three read policies. Requests SHALL accept only an empty object plus strong If-Match and Idempotency-Key on POST; trusted identity and curated evidence SHALL NOT be supplied by callers.

#### Scenario: Authorized conditional request and history
- **WHEN** a properly permitted signed human or machine investigates an accessible incident
- **THEN** return typed result200 with immutable provenance and pending proposal; GET returns paginated original-owner history; missing identity401, insufficient grant403, hidden resource404, invalid input400, stale/replay mismatch409, unavailable503, timeout504 and safe model/budget errors remain explicit
