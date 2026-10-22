## ADDED Requirements

### Requirement: Incident queue entry view
The authenticated Incidents route SHALL open a server-backed paginated table of unmatched vehicle reports, showing fault code, vehicle, driver, reported time and status. It SHALL automatically load only with required permissions, show honest empty/loading/unavailable/denied states, and provide review navigation. The page SHALL NOT default to a generic action form or offer manual incident creation.

#### Scenario: Read and review vehicle incidents
- **WHEN** an authorized user enters Incidents
- **THEN** the unmatched queue is requested and displayed using supplied Spartan components; explicit review loads that incident through its protected API

#### Scenario: Unavailable queue
- **WHEN** the API returns503
- **THEN** show unavailable with no fabricated rows or empty-success claim
