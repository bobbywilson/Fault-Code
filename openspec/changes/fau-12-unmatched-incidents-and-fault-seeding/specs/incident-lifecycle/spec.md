## ADDED Requirements

### Requirement: Unmatched vehicle incident queue
The incident API SHALL support an optional unmatchedOnly filter for reports with no approved fault knowledge match at vehicle intake. It SHALL filter before accessible totals/pagination, preserve tenant/ownership checks and immutable historical intake provenance, and return503 for unavailable authoritative storage rather than claim unknown or empty. Existing unfiltered historical API behavior SHALL remain compatible.

#### Scenario: Known and unmatched vehicle reports
- **WHEN** accessible known and unmatched reports exist and unmatchedOnly is requested
- **THEN** only unmatched-at-intake reports appear in rows and totals, with no cross-tenant or other-owner reports

#### Scenario: Lookup failure
- **WHEN** authoritative fault storage is unavailable during intake
- **THEN** return503 without persisting a false unknown incident
