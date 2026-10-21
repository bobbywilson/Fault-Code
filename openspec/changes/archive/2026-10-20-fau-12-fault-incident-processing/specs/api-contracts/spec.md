## ADDED Requirements

### Requirement: Incident API contracts
Versioned incident intake/list/detail/history/status/failure/retry operations SHALL use exact policies, correlated safe Problem Details, bounded pagination, strong ETags and source/key replay protection. Creation SHALL return201 with Location and original replay ID/version. Missing/malformed preconditions SHALL return400 and conflicts409.
#### Scenario: Incident contract acceptance
- **WHEN** authorized consumers use production incident controllers
- **THEN** generated examples and security metadata match actual policies; invalid input400, invalid identity401, insufficient permission403, missing/other-owner404, conflicts409 and unavailable persistence503 remain distinct
