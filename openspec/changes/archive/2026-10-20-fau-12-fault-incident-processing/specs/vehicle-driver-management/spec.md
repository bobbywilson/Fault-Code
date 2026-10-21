## ADDED Requirements

### Requirement: Immutable incident fleet snapshot
Incident intake SHALL preserve vehicle/driver identifiers and versions, association identity/effective interval and owner mapping from authoritative active same-tenant records. Intake SHALL reject closed associations and occurrences outside the current half-open interval; later fleet changes SHALL NOT rewrite incident context or ownership.
#### Scenario: Intake and later reassignment
- **WHEN** an event is accepted then its association ends or driver deactivates
- **THEN** retained incident context/history and original access remain unchanged; new intake against the closed association fails
